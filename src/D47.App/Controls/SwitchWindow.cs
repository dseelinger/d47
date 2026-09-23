using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;

namespace D47.App.Controls;

/// <summary>Assigning HOTAS switches (Phase 21, items 2 and 4).</summary>
public sealed class SwitchWindow : Window
{
    /// <summary>Fast enough that a flip is never missed between polls.</summary>
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);

    private static readonly string[] States = ["on", "off"];

    /// <summary>Only the actions Elite reports the state of.</summary>
    private static readonly string[] Assignable =
        ["(nothing)", .. SwitchValidation.Assignable.Select(action => action.Id)];

    /// <summary>
    /// The pages a position may name instead (Phase 46): every root any surface registered, as the host
    /// collected them, so the list here and the spoken route read one vocabulary.
    /// </summary>
    private readonly List<string> _pageKeys = [string.Empty];
    private readonly List<string> _pageWords = ["(nothing)"];

    private readonly SwitchStore _store;
    private readonly IHotasReader _reader;
    private readonly SwitchReconciler _reconciler;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _exportPath;

    private readonly List<MutableSwitch> _switches;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly DispatcherTimer _timer;

    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    private readonly Border _walkCard;
    private readonly TextBlock _walkSays = new() { TextWrapping = TextWrapping.Wrap, FontSize = TypeScale.Body };
    private readonly Button _finish;
    private readonly Button _export;

    private SwitchCapture? _capture;

    public SwitchWindow(
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
            _pageKeys.Add(page.Root.Key);
            _pageWords.Add(page.Describe());
        }

        // A saved destination no surface offers right now is shown as its key rather than silently reset to
        // nothing: the row says it is not answered to, and the Commander decides, which is the same treatment
        // a switch whose device has gone gets.
        foreach (var key in _switches.SelectMany(mapping => mapping.Positions).Select(position => position.Destination))
        {
            if (key.Length > 0 && !_pageKeys.Contains(key))
            {
                _pageKeys.Add(key);
                _pageWords.Add(key);
            }
        }

        Title = "HOTAS switches";
        Width = 720;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

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
                    new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { _finish, _export, cancel } },
                },
            },
        };

        Themed(_walkCard, Border.BackgroundProperty, ThemeManager.SlabKey);
        Themed(_walkSays, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.RedKey);

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
            var directory = Path.GetDirectoryName(_exportPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_exportPath, capture.Export());
            _walkSays.Text = $"Written to {_exportPath}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _walkSays.Text = $"The report could not be written: {ex.Message}";
        }

        _export.IsVisible = false;
    }

    /// <summary>One poll of the hardware.</summary>
    private void Sample()
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

        var positions = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        foreach (var position in mapping.Positions)
        {
            positions.Children.Add(BuildPosition(position));
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
                    new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { name, remove, resume } },
                    device,
                    positions,
                    health,
                },
            },
        };

        Themed(card, Border.BorderBrushProperty, ThemeManager.LineKey);

        // Refreshed on every sample, so a device that has just gone away — a 4x32 mode change, an unplugged
        // throttle — says so on this card while the window is still open.
        card.Tag = new Action(() =>
        {
            var state = _reconciler.States.FirstOrDefault(s =>
                string.Equals(s.Name, mapping.Name, StringComparison.OrdinalIgnoreCase));

            resume.IsVisible = state?.Health == SwitchHealth.Paused;

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

    private Control BuildPosition(MutablePosition position)
    {
        var label = new TextBlock
        {
            Text = position.Button is { } button ? $"button {button}" : "nothing held",
            Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = TypeScale.Body,
        };

        Themed(label, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var actionIndex = position.Action.Length == 0 ? 0 : Array.IndexOf(Assignable, position.Action);
        var (actionView, action) = Choice.Build(Assignable, actionIndex < 0 ? 0 : actionIndex);
        actionView.Width = 200;

        var (stateView, state) = Choice.Build(States, Array.IndexOf(States, position.State));
        stateView.Width = 90;

        // Or a page of D47's own panel (Phase 46).
        var pageIndex = Math.Max(0, _pageKeys.IndexOf(position.Destination));
        var (pageView, page) = Choice.Build(_pageWords, pageIndex);
        pageView.Width = 200;

        // A position that means nothing has no state to mean, and offering one would suggest it does.
        stateView.IsEnabled = position.Action.Length > 0;

        action.SelectionChanged += (_, _) =>
        {
            var chosen = action.SelectedItem ?? Assignable[0];
            position.Action = chosen == Assignable[0] ? string.Empty : chosen;
            stateView.IsEnabled = position.Action.Length > 0;

            if (position.Action.Length > 0)
            {
                position.Destination = string.Empty;
                page.SelectedIndex = 0;
            }
        };

        page.SelectionChanged += (_, _) =>
        {
            position.Destination = page.SelectedIndex > 0 ? _pageKeys[page.SelectedIndex] : string.Empty;

            if (position.Destination.Length > 0)
            {
                position.Action = string.Empty;
                action.SelectedIndex = 0;
                stateView.IsEnabled = false;
            }
        };

        state.SelectionChanged += (_, _) => position.State = state.SelectedItem ?? position.State;

        return new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6,
            Children = { label, actionView, stateView, pageView },
        };
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
                    position.Destination.Length == 0 ? null : position.Destination)),
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
    }
}
