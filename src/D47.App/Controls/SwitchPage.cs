using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>Assigning HOTAS switches (Phase 21, items 2 and 4).</summary>
public sealed class SwitchPage : DialogPage
{
    public override string Crumb => "HOTAS switches";

    /// <summary>Fast enough that a flip is never missed between polls.</summary>
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);

    private static readonly string[] States = ["On", "Off"];

    /// <summary>Marks the cyan bar on the row for the position the switch is in.</summary>
    public const string HeldBarName = "SwitchHeldBar";

    /// <summary>Marks the word beside the button label of the position the switch is in.</summary>
    public const string HeldWordName = "SwitchHeldWord";

    /// <summary>Marks the ON / OFF control of a position.</summary>
    public const string StateName = "SwitchPositionState";

    /// <summary>Marks the button that opens the page picker of a position.</summary>
    public const string PageButtonName = "SwitchPositionPage";

    /// <summary>The grey line under a sub-page that is not always there, by the sub-page's key.</summary>
    private static readonly IReadOnlyDictionary<string, string> Availability = new Dictionary<string, string>
    {
        [Panel.MessagesView.Crumb.Key] = "Only while a story is running",
        [Panel.ChecklistPage.Suggestions.Key] = "Only while there are suggestions",
        [Panel.AdventuresPage.AskCrumb.Key] = "Only while D47's model and galaxy search are on",
    };

    /// <summary>The picker value for no page.</summary>
    private const string NoPage = "(nothing)";

    private const string NoAction = "";

    private const string NothingWord = "Nothing";

    private const double ColumnGap = 8;

    /// <summary>The columns of the column heads and of every position row: bar, button, action, state, page.</summary>
    private const string Columns = "3,140,*,150,*";

    /// <summary>Only the actions Elite reports the state of, by the name a list shows.</summary>
    private static readonly IReadOnlyList<(string Id, string Name)> Assignable =
        [.. SwitchValidation.Assignable.Select(action => (action.Id, action.DisplayName ?? action.Label))];

    /// <summary>
    /// The pages a position may name instead (Phase 46): every root any surface registered, as the host
    /// collected them, so the list here and the spoken route read one vocabulary.
    /// </summary>
    private readonly List<string> _pageKeys = [];
    private readonly Dictionary<string, string> _pageWords = [];
    private readonly Dictionary<string, PanelDestination> _destinations = [];
    private int _pickSerial;

    private readonly SwitchStore _store;
    private readonly IHotasReader _reader;
    private readonly SwitchReconciler _reconciler;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _exportPath;

    private readonly List<MutableSwitch> _switches;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly DispatcherTimer _timer;

    private readonly Notice _problems = new() { IsVisible = false };

    private readonly Border _walkCard;
    private readonly TextBlock _walkSays = new() { TextWrapping = TextWrapping.Wrap, FontSize = TypeScale.Body };
    private readonly Button _finish;
    private readonly Button _export;

    private SwitchCapture? _capture;

    public SwitchPage(
        SwitchStore store,
        IHotasReader reader,
        SwitchReconciler reconciler,
        Func<DateTimeOffset> now,
        string exportPath,
        IReadOnlyList<PanelDestination> destinations)
    {
        _store = store;
        _reader = reader;
        _reconciler = reconciler;
        _now = now;
        _exportPath = exportPath;
        _switches = [.. store.Switches.Select(MutableSwitch.From)];

        foreach (var page in destinations)
        {
            _pageKeys.Add(page.Key);
            _destinations[page.Key] = page;
            _pageWords[page.Key] = page.SubPage is null ? page.Describe() : $"{page.Root.Word} › {page.Word}";
        }

        // A saved destination no surface offers right now is shown as its key rather than silently reset to
        // nothing: the row says it is not answered to, and the Commander decides, which is the same treatment
        // a switch whose device has gone gets.
        foreach (var key in _switches.SelectMany(mapping => mapping.Positions).Select(position => position.Destination))
        {
            if (key.Length > 0 && !_pageKeys.Contains(key))
            {
                _pageKeys.Add(key);
                _pageWords[key] = key;
            }
        }

        Title = "HOTAS switches";

        var assign = new Button { Content = "Assign a switch" };
        assign.Click += (_, _) => StartWalk();

        var save = new Button { Content = "Save", MinWidth = 110 };
        save.Click += (_, _) => Save();

        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        _finish = new Button { Content = "Finish", IsVisible = false };
        _finish.Click += (_, _) => FinishWalk();

        _export = new Button { Content = "Export the capture report", IsVisible = false };
        _export.Click += (_, _) => Export();

        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => EndWalk();

        _walkCard = new Border
        {
            Padding = new Thickness(14, 12),
            IsVisible = false,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _walkSays,
                    new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _finish, _export, cancel } },
                },
            },
        };

        Themed(_walkCard, Border.BackgroundProperty, ThemeManager.SlabKey);
        Themed(_walkSays, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        var header = new TextBlock
        {
            Text = $"Saved to {store.Path}. This is the same file you can edit by hand.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(header, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var root = new StackPanel { Spacing = 12, Children = { header, _problems, _walkCard, _list } };

        Modal.Apply(this, "HOTAS", Title, root, [assign, save, close]);

        _timer = new DispatcherTimer { Interval = Period };
        _timer.Tick += (_, _) => Sample();

        Rebuild();
        ShowProblems();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void StartWalk()
    {
        _capture = new SwitchCapture();
        _walkCard.IsVisible = true;
        _finish.IsVisible = true;
        _export.IsVisible = false;
        _walkSays.Text = "Move the switch to each position in turn, and pause at each one.";

        if (_reader.Unavailable is { Length: > 0 } why)
        {
            _walkSays.Text = why;
        }
    }

    private void EndWalk()
    {
        _capture = null;
        _walkCard.IsVisible = false;
        _finish.IsVisible = false;
        _export.IsVisible = false;
    }

    private void FinishWalk()
    {
        if (_capture is not { } capture)
        {
            return;
        }

        var result = capture.Finish(_now());
        _walkSays.Text = result.Says;
        _finish.IsVisible = false;

        // The report is offered on a decline and not on a success, because a walk that worked is not evidence
        // of anything.
        _export.IsVisible = result.IsDeclined;

        if (!result.IsCaptured)
        {
            return;
        }

        _switches.Add(new MutableSwitch
        {
            Name = Unused("new switch"),
            DeviceId = result.DeviceId,
            Device = result.Device,
            Positions = [.. result.Positions.Select(position => new MutablePosition { Button = position.Button })],
        });

        EndWalk();
        Rebuild();
    }

    private void Export()
    {
        if (_capture is not { } capture)
        {
            return;
        }

        try
        {
            _store.Files.WriteText(_exportPath, capture.Export());
            _walkSays.Text = $"Written to {_exportPath}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _walkSays.Text = $"The report could not be written: {ex.Message}";
        }

        _export.IsVisible = false;
    }

    /// <summary>One poll of the hardware.</summary>
    internal void Sample()
    {
        var readings = _reader.Poll();

        if (_capture is { } capture)
        {
            var result = capture.Poll(_now(), readings);
            _walkSays.Text = result.Says;

            if (result.IsDeclined)
            {
                _finish.IsVisible = false;
                _export.IsVisible = true;
                _capture = capture;
            }
        }

        foreach (var control in _list.Children.OfType<Border>())
        {
            if (control.Tag is Action refresh)
            {
                refresh();
            }
        }
    }

    private void Save()
    {
        _store.Save([.. _switches.Select(mapping => mapping.ToMapping())]);

        // Re-read rather than trusting what was just written, so what the panel shows next is the validated
        // set — the same rule the macro editor follows, and the same reason.
        _store.Poll();

        _switches.Clear();
        _switches.AddRange(_store.Switches.Select(MutableSwitch.From));

        Rebuild();
        ShowProblems();
    }

    private void ShowProblems()
    {
        if (_store.Problems.Count == 0)
        {
            _problems.IsVisible = false;
            return;
        }

        _problems.IsVisible = true;
        _problems.Text = string.Join(
            "\n", _store.Problems.Select(problem => $"{problem.Name}: {problem.Reason}"));
    }

    private void Rebuild()
    {
        _list.Children.Clear();

        if (_switches.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No switches yet. Press \"Assign a switch\" and walk one through its positions — "
                       + "D47 has no way to know what your stick looks like until you show it.",
                FontSize = TypeScale.Body,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            _list.Children.Add(empty);
            return;
        }

        foreach (var mapping in _switches)
        {
            _list.Children.Add(BuildSwitch(mapping));
        }
    }

    private Control BuildSwitch(MutableSwitch mapping)
    {
        var name = new TextBox { Text = mapping.Name, Width = 260 };
        name.TextChanged += (_, _) => mapping.Name = name.Text ?? string.Empty;

        var remove = new Button { Content = "Remove", Classes = { "destructive" } };
        remove.Click += (_, _) =>
        {
            _switches.Remove(mapping);
            Rebuild();
        };

        var resume = new Button
        {
            Content = "Resume",
            IsVisible = false,
        };

        resume.Click += (_, _) => _reconciler.Resume(mapping.Name);

        var device = new TextBlock
        {
            Text = mapping.Device.Length == 0 ? mapping.DeviceId : mapping.Device,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(device, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var health = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(health, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var collided = false;

        var positions = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { BuildHeads() } };
        var holds = new List<Action<SwitchPosition?>>();

        foreach (var position in mapping.Positions)
        {
            var (row, held) = BuildPosition(mapping, position);
            positions.Children.Add(row);
            holds.Add(held);
        }

        // Ruled rather than a list row: the box and the choices are Tile, and vanish on a Tile row.
        var card = new Border
        {
            Padding = new Thickness(0, 12),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { name, remove, resume } },
                    device,
                    positions,
                    health,
                },
            },
        };

        Themed(card, Border.BorderBrushProperty, ThemeManager.LineKey);

        // Refreshed on every sample, so a device that has just gone away — a 4x32 mode change, an unplugged
        // throttle — says so on this card while the page is still open.
        card.Tag = new Action(() =>
        {
            var state = _reconciler.States.FirstOrDefault(s =>
                string.Equals(s.Name, mapping.Name, StringComparison.OrdinalIgnoreCase));

            resume.IsVisible = state?.Health == SwitchHealth.Paused;

            foreach (var held in holds)
            {
                held(state?.Position);
            }

            // The collision first, because it is the one line that explains a switch doing the opposite of
            // what it was asked — and unlike the other two it is a fault in the setup rather than a state of
            // the flight (#147).
            health.Text = state is null
                ? "Not being reconciled yet — save to start."
                : state.Collides is { } collides
                    ? collides
                    : state.Disagrees is { } disagrees
                        ? $"{state.Note} Sitting against the game: {disagrees}.".TrimStart()
                        : state.Note;

            health.IsVisible = health.Text is { Length: > 0 };

            // A collision is a fault in the setup, so it is Red; every other note is Grey.
            if (state?.Collides is not null != collided)
            {
                collided = !collided;
                Themed(health, TextBlock.ForegroundProperty, collided ? ThemeManager.RedKey : ThemeManager.GreyKey);
            }
        });

        return card;
    }

    /// <summary>The column heads over a switch's position rows.</summary>
    private static Control BuildHeads()
    {
        var heads = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = ColumnGap };

        string[] words = ["", "Position", "In Elite", "Means", "Or opens a D47 page"];

        for (var column = 1; column < words.Length; column++)
        {
            var head = new TextBlock
            {
                Text = words[column].ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.Caption,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(head, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            Grid.SetColumn(head, column);
            heads.Children.Add(head);
        }

        return heads;
    }

    /// <summary>One position: the button, what it means, ON or OFF, or the page it opens.</summary>
    private (Control Row, Action<SwitchPosition?> Held) BuildPosition(MutableSwitch mapping, MutablePosition position)
    {
        var bar = new Border
        {
            Name = HeldBarName,
            Height = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Top,
            IsVisible = false,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.CyanKey);

        var label = new TextBlock
        {
            Text = (position.Button is { } button ? $"button {button}" : "nothing held").ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var heldWord = new TextBlock
        {
            Name = HeldWordName,
            Text = "HELD",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        Themed(heldWord, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        var buttonCell = new Grid
        {
            Height = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new WrapPanel
                {
                    ItemSpacing = 8,
                    LineSpacing = 2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { label, heldWord },
                },
            },
        };

        var action = new InlinePicker { Label = "Action", VerticalAlignment = VerticalAlignment.Top };
        var state = new Segment
        {
            Name = StateName,
            ItemsSource = States,
            SelectedIndex = position.State == "off" ? 1 : 0,
            Height = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var pageWord = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chevron = new TextBlock
        {
            Text = "›",
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.AKey);
        DockPanel.SetDock(chevron, Dock.Right);

        var pageButton = new Button
        {
            Name = PageButtonName,
            Content = new DockPanel { Children = { chevron, pageWord } },
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 4),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        pageButton.Classes.Add(ListRow.Class);
        AutomationProperties.SetName(pageButton, "Page");

        void Draw()
        {
            var named = Assignable.FirstOrDefault(known => known.Id == position.Action);
            var entries = new List<InlinePickerEntry> { new InlinePickerOption(NoAction, NothingWord, ThemeManager.GreyKey) };
            entries.AddRange(Assignable.Select(known => new InlinePickerOption(known.Id, known.Name, ThemeManager.WhiteKey)));

            action.Show(
                entries,
                position.Action,
                named.Name ?? NothingWord,
                named.Name is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
                null);

            state.IsEnabled = position.Action.Length > 0;
            state.SelectedIndex = position.State == "off" ? 1 : 0;

            pageWord.Text = position.Destination.Length > 0
                ? _pageWords.GetValueOrDefault(position.Destination, position.Destination)
                : NothingWord;

            Themed(pageWord, TextBlock.ForegroundProperty, position.Destination.Length > 0 ? ThemeManager.WhiteKey : ThemeManager.GreyKey);
        }

        action.Picked += (_, id) =>
        {
            position.Action = id;

            if (id.Length > 0)
            {
                position.Destination = string.Empty;
                position.View = null;
            }

            Draw();
        };

        state.SelectionChanged += (_, _) => position.State = state.SelectedIndex == 1 ? "off" : "on";

        var key = $"switch-page:{++_pickSerial}";

        pageButton.Click += (_, _) =>
        {
            if (this.FindAncestorOfType<Panel.PanelView>()?.Prompts is not { } prompts)
            {
                return;
            }

            prompts.Pick(
                key,
                PickerWord(mapping, position),
                new PickerRequest
                {
                    Prompt = PickerWord(mapping, position),
                    Help = "Flipping the switch to this position opens the page on D47's panel.",
                    Choices = [NoPage, .. _pageKeys],
                    Describe = PickedWord,
                    GroupOf = GroupOf,
                    IsNested = value => _destinations.TryGetValue(value, out var page) && page.SubPage is not null,
                    DetailOf = value => Availability.GetValueOrDefault(value),
                    Current = position.Destination.Length > 0 ? position.Destination : NoPage,
                    ChoiceNoun = "pages",
                },
                result => TakePage(position, result.Value, Draw));
        };

        Draw();

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = ColumnGap };

        foreach (var (cell, column) in new (Control, int)[] { (bar, 0), (buttonCell, 1), (action, 2), (state, 3), (pageButton, 4) })
        {
            Grid.SetColumn(cell, column);
            row.Children.Add(cell);
        }

        bool? wasHeld = null;

        void Held(SwitchPosition? at)
        {
            var held = at is not null && at.Button == position.Button;

            if (held == wasHeld)
            {
                return;
            }

            wasHeld = held;
            bar.IsVisible = held;
            heldWord.IsVisible = held;
            Themed(label, TextBlock.ForegroundProperty, held ? ThemeManager.CyanKey : ThemeManager.GreyKey);
        }

        Held(null);

        return (row, Held);
    }

    private static string PickerWord(MutableSwitch mapping, MutablePosition position) =>
        $"{mapping.Name} · {(position.Button is { } button ? $"Button {button}" : "Position")} D47 page";

    /// <summary>A page's word alone; its group head names the tab.</summary>
    private string PickedWord(string value) =>
        value == NoPage ? NothingWord
        : _destinations.TryGetValue(value, out var page) ? page.Word
        : value;

    /// <summary>The tab a page sits under, as the bar draws it; null for nothing and for a key no surface offers.</summary>
    private string? GroupOf(string value) =>
        _destinations.TryGetValue(value, out var page) ? TabWord(page.Tab) : null;

    private static string TabWord(PanelTab tab) => tab switch
    {
        PanelTab.Assets => "ASSET MGMT",
        _ => tab.ToString().ToUpperInvariant(),
    };

    /// <summary>Applies a page chosen in the picker; naming a page clears the action.</summary>
    private static void TakePage(MutablePosition position, string? chosen, Action redraw)
    {
        if (chosen is null)
        {
            return;
        }

        position.Destination = chosen == NoPage ? string.Empty : chosen;

        if (position.Destination.Length > 0)
        {
            position.Action = string.Empty;
            position.View = null;
        }

        redraw();
    }

    /// <summary>A name nothing else has, so a second capture does not collide with the first.</summary>
    private string Unused(string wanted)
    {
        if (_switches.All(mapping => !string.Equals(mapping.Name, wanted, StringComparison.OrdinalIgnoreCase)))
        {
            return wanted;
        }

        for (var index = 2; index < SwitchValidation.MaxSwitches + 2; index++)
        {
            var candidate = $"{wanted} {index}";

            if (_switches.All(mapping => !string.Equals(mapping.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return wanted;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));

    /// <summary>The editor's working copy.</summary>
    private sealed class MutableSwitch
    {
        public string Name { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public string Device { get; set; } = string.Empty;

        public List<MutablePosition> Positions { get; init; } = [];

        public static MutableSwitch From(SwitchMapping mapping) => new()
        {
            Name = mapping.Name,
            DeviceId = mapping.DeviceId,
            Device = mapping.Device,
            Positions =
            [
                .. mapping.Positions.Select(position => new MutablePosition
                {
                    Button = position.Button,
                    Action = position.Action ?? string.Empty,
                    State = position.State == DesiredState.Off ? "off" : "on",
                    Destination = position.Destination ?? string.Empty,
                    View = position.View,
                }),
            ],
        };

        public SwitchMapping ToMapping() => new()
        {
            Name = Name.Trim(),
            DeviceId = DeviceId,
            Device = Device,
            Positions =
            [
                .. Positions.Select(position => new SwitchPosition(
                    position.Button,
                    position.Action.Length == 0 ? null : position.Action,
                    position.State == "off" ? DesiredState.Off : DesiredState.On,
                    position.Destination.Length == 0 ? null : position.Destination,
                    position.View)),
            ],
        };
    }

    private sealed class MutablePosition
    {
        public int? Button { get; set; }

        public string Action { get; set; } = string.Empty;

        public string State { get; set; } = "on";

        /// <summary>The root key of a page, or empty.</summary>
        public string Destination { get; set; } = string.Empty;

        public D47.Core.Vr.HeadsetView? View { get; set; }
    }
}
