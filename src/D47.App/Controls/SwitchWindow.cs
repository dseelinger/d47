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

/// <summary>
/// Assigning HOTAS switches (Phase 21, items 2 and 4).
/// <para>
/// <b>Reachable from here and from nowhere else.</b> The model reads untrusted text, so a tool
/// that could remap a switch is a tool a hostile in-game message can use to give itself the
/// Commander's controls (architecture.md §7). There is a reporting tool and there is no
/// assigning one.
/// </para>
/// <para>
/// <b>The walk is the only way in.</b> There is no device picker, because there is nothing to
/// pick from: Windows reports <c>HID-compliant game controller</c> for every stick on the bench,
/// so a list would be six identical rows. The Commander moves the switch and d47 watches, which
/// is the same reason there is no per-device profile table (docs/spikes/hotas-switch-read.md,
/// finding 2).
/// </para>
/// <para>
/// It owns a timer, which is allowed here and is not allowed in Core: the capture is a
/// <see cref="SwitchCapture.Poll"/> that has to be called several times a second while a modal
/// window is open, and the tick loop is not running this window's dialogue.
/// </para>
/// </summary>
public sealed class SwitchWindow : Window
{
    /// <summary>
    /// Fast enough that a flip is never missed between polls. The spike sampled at 125 Hz; 20 Hz
    /// is ample for a control a human is moving by hand, and the settle window is 1.5 s.
    /// </summary>
    private static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);

    private static readonly string[] States = ["on", "off"];

    /// <summary>
    /// Only the actions Elite reports the state of. Offering anything else would be offering
    /// something the validator refuses — and, worse, teaching that a switch is a press.
    /// </summary>
    private static readonly string[] Assignable =
        ["(nothing)", .. SwitchValidation.Assignable.Select(action => action.Id)];

    /// <summary>
    /// The pages a position may name instead (Phase 46): every root any surface
    /// registered, as the host collected them, so the list here and the spoken route read one
    /// vocabulary. Keys, and the words shown for them, in parallel.
    /// </summary>
    private readonly List<string> _pageKeys = [string.Empty];
    private readonly List<string> _pageWords = ["(nothing)"];

    private readonly SwitchStore _store;
    private readonly IHotasReader _reader;
    private readonly SwitchReconciler _reconciler;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _exportPath;

    private readonly List<MutableSwitch> _switches;
    private readonly StackPanel _list = new() { Spacing = 12 };
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

        // A saved destination no surface offers right now is shown as its key rather than
        // silently reset to nothing: the row says it is not answered to, and the Commander
        // decides, which is the same treatment a switch whose device has gone gets.
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

        var assign = new Button { Content = "Assign a switch", Padding = new Thickness(10, 4) };
        assign.Click += (_, _) => StartWalk();

        var save = new Button { Content = "Save", Padding = new Thickness(14, 4) };
        save.Click += (_, _) => Save();

        var close = new Button { Content = "Close", Padding = new Thickness(14, 4) };
        close.Click += (_, _) => Close();

        _finish = new Button { Content = "Finish", Padding = new Thickness(10, 4), IsVisible = false };
        _finish.Click += (_, _) => FinishWalk();

        _export = new Button { Content = "Export the capture report", Padding = new Thickness(10, 4), IsVisible = false };
        _export.Click += (_, _) => Export();

        var cancel = new Button { Content = "Cancel", Padding = new Thickness(10, 4) };
        cancel.Click += (_, _) => EndWalk();

        _walkCard = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            IsVisible = false,
            Margin = new Thickness(0, 12, 0, 0),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    _walkSays,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { _finish, _export, cancel },
                    },
                },
            },
        };

        Themed(_walkCard, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var header = new TextBlock
        {
            Text = $"Saved to {store.Path}. This is the same file you can edit by hand.",
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(header, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { assign, save, close },
        };

        var root = new DockPanel { Margin = new Thickness(16) };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);
        DockPanel.SetDock(_walkCard, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);

        root.Children.Add(header);
        root.Children.Add(_problems);
        root.Children.Add(_walkCard);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = _list, Margin = new Thickness(0, 12, 0, 0) });

        Content = root;
        Themed(this, BackgroundProperty, ThemeManager.SurfaceKey);

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

        // The report is offered on a decline and not on a success, because a walk that worked is
        // not evidence of anything. A declined one is the whole of what a maintainer who will
        // never hold this device has to go on.
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

    /// <summary>
    /// One poll of the hardware. Two jobs: driving a walk in progress, and keeping the health
    /// line on each existing switch live — a Commander who has just changed 4x32 mode should see
    /// "needs reassigning" appear without closing the window.
    /// </summary>
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

        // Re-read rather than trusting what was just written, so what the panel shows next is the
        // validated set — the same rule the macro editor follows, and the same reason.
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

            Themed(empty, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
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

        var remove = new Button { Content = "Remove", Padding = new Thickness(10, 2), FontSize = TypeScale.Body };
        remove.Click += (_, _) =>
        {
            _switches.Remove(mapping);
            Rebuild();
        };

        var resume = new Button
        {
            Content = "Resume",
            Padding = new Thickness(10, 2),
            FontSize = TypeScale.Body,
            IsVisible = false,
        };

        resume.Click += (_, _) => _reconciler.Resume(mapping.Name);

        var device = new TextBlock
        {
            Text = mapping.Device.Length == 0 ? mapping.DeviceId : mapping.Device,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(device, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var health = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        var positions = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        foreach (var position in mapping.Positions)
        {
            positions.Children.Add(BuildPosition(position));
        }

        var card = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { name, remove, resume },
                    },
                    device,
                    positions,
                    health,
                },
            },
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        // Refreshed on every sample, so a device that has just gone away — a 4x32 mode change,
        // an unplugged throttle — says so on this card while the window is still open.
        card.Tag = new Action(() =>
        {
            var state = _reconciler.States.FirstOrDefault(s =>
                string.Equals(s.Name, mapping.Name, StringComparison.OrdinalIgnoreCase));

            resume.IsVisible = state?.Health == SwitchHealth.Paused;

            // The collision first, because it is the one line that explains a switch doing the
            // opposite of what it was asked — and unlike the other two it is a fault in the setup
            // rather than a state of the flight (#147).
            health.Text = state is null
                ? "Not being reconciled yet — save to start."
                : state.Collides is { } collides
                    ? collides
                    : state.Disagrees is { } disagrees
                        ? $"{state.Note} Sitting against the game: {disagrees}.".TrimStart()
                        : state.Note;

            health.IsVisible = health.Text is { Length: > 0 };
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

        var action = new ComboBox
        {
            ItemsSource = Assignable,
            SelectedItem = position.Action.Length == 0 ? Assignable[0] : position.Action,
            Width = 200,
        };

        var state = new ComboBox { ItemsSource = States, SelectedItem = position.State, Width = 90 };

        // Or a page of D47's own panel (Phase 46). A second control rather than entries
        // in the first, because what is stored is a declared field and not a prefixed string —
        // and choosing one clears the other, so a position never means two things.
        var pageIndex = Math.Max(0, _pageKeys.IndexOf(position.Destination));
        var page = new ComboBox { ItemsSource = _pageWords, SelectedIndex = pageIndex, Width = 200 };

        // A position that means nothing has no state to mean, and offering one would suggest it
        // does. The centre of a three-position switch is the ordinary case for this — and so is
        // a position that names a page, which has no on or off.
        state.IsEnabled = position.Action.Length > 0;

        action.SelectionChanged += (_, _) =>
        {
            var chosen = action.SelectedItem as string ?? Assignable[0];
            position.Action = chosen == Assignable[0] ? string.Empty : chosen;
            state.IsEnabled = position.Action.Length > 0;

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
                action.SelectedItem = Assignable[0];
                state.IsEnabled = false;
            }
        };

        state.SelectionChanged += (_, _) => position.State = state.SelectedItem as string ?? position.State;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { label, action, state, page },
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

    /// <summary>
    /// The editor's working copy. <see cref="SwitchMapping"/> is immutable, which is right for
    /// something being validated and reconciled, and wrong for something being typed into.
    /// </summary>
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

        /// <summary>The root key of a page, or empty. Never set alongside <see cref="Action"/>.</summary>
        public string Destination { get; set; } = string.Empty;
    }
}
