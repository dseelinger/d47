using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Utilities;

namespace D47.App.Panel;

/// <summary>Clocks, timers and alarms (Phase 24, "Utilities").</summary>
public sealed class UtilitiesPage : UserControl
{
    private readonly Timekeeper _timekeeper;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeZoneInfo> _zone;
    private readonly PanelPrompts _prompts;

    private readonly TextBlock _galactic;
    private readonly TextBlock _galacticDate = Dated();
    private readonly TextBlock _local;
    private readonly TextBlock _localDate = Dated();

    private readonly StackPanel _running = new() { Spacing = 2 };
    private readonly TextBlock _problems = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = TypeScale.Secondary,
        IsVisible = false,
    };

    public UtilitiesPage(
        Timekeeper timekeeper,
        AlarmStore alarms,
        Func<DateTimeOffset> now,
        Func<TimeZoneInfo> zone,
        PanelPrompts prompts)
    {
        _timekeeper = timekeeper;
        _now = now;
        _zone = zone;
        _prompts = prompts;

        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.RedKey);

        var timer = new Button { Content = "New timer", VerticalAlignment = VerticalAlignment.Top };
        timer.Click += (_, _) => AddTimer();

        var alarm = new Button { Content = "New alarm", VerticalAlignment = VerticalAlignment.Top };
        alarm.Click += (_, _) => AddAlarm();

        var (galactic, galacticTime) = Clock("Galactic", _galacticDate);
        var (local, localTime) = Clock("Local", _localDate);

        _galactic = galacticTime;
        _local = localTime;

        var clocks = StatTile.Grid([galactic, local], maxColumns: 2);
        clocks.Margin = new Thickness(0, 0, 0, 16);

        var actions = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { timer, alarm },
        };

        var (title, _) = RoutingKit.Title("Utilities");
        _title = title;

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(clocks, Dock.Top);
        DockPanel.SetDock(actions, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);

        _problems.Margin = new Thickness(0, 0, 0, 10);

        root.Children.Add(title);
        root.Children.Add(clocks);
        root.Children.Add(actions);
        root.Children.Add(_problems);
        root.Children.Add(new ScrollViewer
        {
            Content = _running,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        Content = root;

        alarms.Changed += () => Dispatcher.UIThread.Post(() => Refresh());

        Refresh();
    }

    /// <summary>The screen title, left out on mini so the running list keeps the height.</summary>
    private readonly Control _title;

    private IDisposable? _mode;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _mode = this.GetSelfAndVisualAncestors()
            .OfType<PanelView>()
            .FirstOrDefault()
            ?.GetObservable(PanelView.ModeProperty)
            .Subscribe(new AnonymousObserver<PanelMode>(mode => _title.IsVisible = mode != PanelMode.Mini));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _mode?.Dispose();
        _mode = null;
    }

    /// <summary>Redraws the clocks and the running list.</summary>
    public bool Refresh()
    {
        var now = _now();
        var zone = _zone();
        var clocks = GalacticTime.Read(now, zone);

        var moved = Show(_galactic, clocks.GalacticTimeOfDay);
        moved |= Show(_galacticDate, clocks.GalacticDate);
        moved |= Show(_local, clocks.LocalTimeOfDay);
        moved |= Show(_localDate, clocks.LocalDate);

        var running = _timekeeper.Running;

        // **Rebuilt only when the list itself changes** (remediation.md 17, item 14).
        var wanted = string.Join('|', running.Select(reminder => reminder.Id));

        if (!string.Equals(wanted, _showing, StringComparison.Ordinal))
        {
            _showing = wanted;
            _due.Clear();
            _running.Children.Clear();

            if (running.Count == 0)
            {
                _running.Children.Add(Muted(
                    "Nothing running. Say \"set a timer for forty minutes\", or press New timer."));

                return true;
            }

            foreach (var reminder in running)
            {
                _running.Children.Add(Line(reminder, now, zone));
            }

            return true;
        }

        foreach (var reminder in running)
        {
            if (_due.TryGetValue(reminder.Id, out var due))
            {
                moved |= Show(due, reminder.Describe(now, zone));
            }
        }

        return moved;
    }

    /// <summary>Writes a string into a block and says whether that changed what is on screen.</summary>
    private static bool Show(TextBlock block, string? text)
    {
        if (string.Equals(block.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        block.Text = text;

        return true;
    }

    /// <summary>What is currently drawn, so a tick that changes nothing draws nothing.</summary>
    private string _showing = string.Empty;

    /// <summary>Each running reminder's countdown, so it can be written without rebuilding it.</summary>
    private readonly Dictionary<string, TextBlock> _due = [];

    /// <summary>Shows what a store or a refusal had to say.</summary>
    public void Say(string message)
    {
        _problems.IsVisible = true;
        _problems.Text = message;
    }

    private Control Line(Reminder reminder, DateTimeOffset now, TimeZoneInfo zone)
    {
        var name = ListRow.Name(new TextBlock
        {
            Text = reminder.Name,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var due = ListRow.Secondary(new TextBlock
        {
            Text = reminder.Describe(now, zone),
            FontSize = TypeScale.Body,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        });

        // Kept, so the next tick writes the countdown rather than building this row again.
        _due[reminder.Id] = due;

        // Protected, and this is the affordance that makes that mean something: cancelling is reachable from
        // here and from a phrase, and from nothing the model can call.
        var cancel = new Button
        {
            Content = "Cancel",
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        cancel.Click += (_, _) =>
        {
            _timekeeper.Cancel(reminder.Id);
            Refresh();
        };

        var row = new DockPanel();

        DockPanel.SetDock(cancel, Dock.Right);
        DockPanel.SetDock(due, Dock.Right);

        row.Children.Add(cancel);
        row.Children.Add(due);
        row.Children.Add(name);

        return ListRow.Dress(new Border { Padding = new Thickness(12, 6), Child = row });
    }

    /// <summary>A name, then a length.</summary>
    private void AddTimer()
    {
        _prompts.Enter(
            new EntryRequest(
                "utilities.timer.name",
                "Timer",
                "What is the timer for?",
                "D47 says this name back when it finishes, so it can be anything you will recognise.",
                string.Empty,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("A timer needs a name, so I can say which one finished.")
                    : EntryVerdict.Ok),
            name => _prompts.Enter(
                new EntryRequest(
                    "utilities.timer.minutes",
                    "Minutes",
                    $"How long for \"{name}\"?",
                    "Minutes, from now.",
                    string.Empty,
                    EntrySurface.Voice,
                    value => Minutes(value) is null
                        ? EntryVerdict.No("That is not a number of minutes.")
                        : EntryVerdict.Ok),
                minutes =>
                {
                    if (Minutes(minutes) is not { } length
                        || _timekeeper.StartTimer(name, length, _now()) is null)
                    {
                        Say("That is not a length of time I can count down.");
                        return;
                    }

                    Refresh();
                }));
    }

    /// <summary>A name, then a moment.</summary>
    private void AddAlarm()
    {
        _prompts.Enter(
            new EntryRequest(
                "utilities.alarm.name",
                "Alarm",
                "What is the alarm for?",
                "D47 says this name back when it goes off.",
                string.Empty,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("An alarm needs a name, so I can say which one went off.")
                    : EntryVerdict.Ok),
            name => _prompts.Enter(
                new EntryRequest(
                    "utilities.alarm.at",
                    "Time",
                    $"When should \"{name}\" go off?",
                    "Your own clock, as 24-hour HH:mm.",
                    string.Empty,
                    EntrySurface.Voice,
                    value => At(value) is null
                        ? EntryVerdict.No("I need a time as 24-hour HH:mm.")
                        : EntryVerdict.Ok),
                at =>
                {
                    if (At(at) is not { } when)
                    {
                        Say("I need a time as 24-hour HH:mm.");
                        return;
                    }

                    var now = _now();
                    var zone = _zone();
                    var local = GalacticTime.Local(now, zone);
                    var today = new DateTimeOffset(local.Date.Add(when.ToTimeSpan()), local.Offset);
                    var due = today > local ? today : today.AddDays(1);

                    if (_timekeeper.SetAlarm(name, due, now) is null)
                    {
                        Say("That moment has already gone.");
                        return;
                    }

                    Refresh();
                }));
    }

    private static TimeSpan? Minutes(string value) =>
        double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : null;

    private static TimeOnly? At(string value) =>
        TimeOnly.TryParseExact(value.Trim(), "HH\\:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var at)
            ? at
            : null;

    /// <summary>A clock's stat tile, with its date under the time; returns the tile and its time.</summary>
    private static (Border Tile, TextBlock Time) Clock(string caption, TextBlock date)
    {
        var tile = StatTile.Build(caption, string.Empty);
        var lines = (StackPanel)tile.Child!;

        lines.Children.Add(date);

        return (tile, (TextBlock)lines.Children[1]);
    }

    private static TextBlock Dated()
    {
        var block = new TextBlock { FontSize = TypeScale.Secondary, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
