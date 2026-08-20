using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Utilities;

namespace D47.App.Panel;

/// <summary>
/// Clocks, timers and alarms (list.md Phase 24, "Utilities").
/// <para>
/// <b>One page, no drill.</b> Everything here is a fact or a short list, and a stack would be
/// three levels to reach a countdown.
/// </para>
/// <para>
/// The two clocks are <b>one instant presented twice</b> — Elite runs 1286 years ahead — so they
/// cannot disagree. The panel does not compute the offset itself: <see cref="GalacticTime"/> does,
/// and the same arithmetic answers the spoken question and fills the game-state block, so a
/// Commander who reads it and one who asks for it get the same sentence.
/// </para>
/// </summary>
public sealed class UtilitiesPage : UserControl
{
    private readonly Timekeeper _timekeeper;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<TimeZoneInfo> _zone;
    private readonly PanelPrompts _prompts;

    private readonly TextBlock _galactic = new()
    {
        FontSize = TypeScale.Heading,
        FontWeight = FontWeight.SemiBold,
    };

    private readonly TextBlock _galacticDate = new() { FontSize = TypeScale.Body };
    private readonly TextBlock _local = new() { FontSize = TypeScale.Heading };
    private readonly TextBlock _localDate = new() { FontSize = TypeScale.Body };

    private readonly StackPanel _running = new() { Spacing = 4 };
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

        Themed(_galactic, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        Themed(_galacticDate, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_local, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        Themed(_localDate, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        Themed(_problems, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var timer = new Button { Content = "New timer", Padding = new Thickness(12, 4), MinHeight = 30 };
        timer.Click += (_, _) => AddTimer();

        var alarm = new Button { Content = "New alarm", Padding = new Thickness(12, 4), MinHeight = 30 };
        alarm.Click += (_, _) => AddAlarm();

        var clocks = new StackPanel
        {
            Spacing = 16,
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16),
            Children =
            {
                Clock("Galactic", _galactic, _galacticDate),
                Clock("Local", _local, _localDate),
            },
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { timer, alarm },
        };

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(clocks, Dock.Top);
        DockPanel.SetDock(actions, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);

        _problems.Margin = new Thickness(0, 0, 0, 10);

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

        alarms.Changed += () => Dispatcher.UIThread.Post(Refresh);

        Refresh();
    }

    /// <summary>
    /// Redraws the clocks and the running list. Called by the host on every tick, because a clock
    /// that only redraws when something changed is a clock that stopped.
    /// </summary>
    public void Refresh()
    {
        var now = _now();
        var zone = _zone();
        var clocks = GalacticTime.Read(now, zone);

        _galactic.Text = clocks.GalacticTimeOfDay;
        _galacticDate.Text = clocks.GalacticDate;
        _local.Text = clocks.LocalTimeOfDay;
        _localDate.Text = clocks.LocalDate;

        var running = _timekeeper.Running;

        // **Rebuilt only when the list itself changes** (remediation.md 17, item 14). Reported
        // from the headset: *"the Utilities tab flickers a lot — other tabs are fine"*. This
        // cleared the panel and built every row again on every tick, and the headset rasterises
        // the tree on its own cadence rather than the window's: it catches the moment between the
        // clear and the re-add, and draws an empty list. The desktop window never showed it
        // because it composes the whole frame after the tick has finished.
        //
        // So a running timer's countdown is written into the row it already has, and controls are
        // only made when the set of reminders is different from the set on screen. A clock that
        // only redraws when something changed is a clock that stopped — but the thing that changed
        // is a string, not a row.
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

                return;
            }

            foreach (var reminder in running)
            {
                _running.Children.Add(Line(reminder, now, zone));
            }

            return;
        }

        foreach (var reminder in running)
        {
            if (_due.TryGetValue(reminder.Id, out var due))
            {
                due.Text = reminder.Describe(now, zone);
            }
        }
    }

    /// <summary>What is currently drawn, so a tick that changes nothing draws nothing.</summary>
    private string _showing = string.Empty;

    /// <summary>Each running reminder's countdown, so it can be written without rebuilding it.</summary>
    private readonly Dictionary<string, TextBlock> _due = [];

    /// <summary>Shows what a store or a refusal had to say. Cleared by the next refresh.</summary>
    public void Say(string message)
    {
        _problems.IsVisible = true;
        _problems.Text = message;
    }

    private Control Line(Reminder reminder, DateTimeOffset now, TimeZoneInfo zone)
    {
        var name = new TextBlock
        {
            Text = reminder.Name,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var due = new TextBlock
        {
            Text = reminder.Describe(now, zone),
            FontSize = TypeScale.Body,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        Themed(due, TextBlock.ForegroundProperty, ThemeManager.AccentKey);

        // Kept, so the next tick writes the countdown rather than building this row again.
        _due[reminder.Id] = due;

        // Protected, and this is the affordance that makes that mean something: cancelling is
        // reachable from here and from a phrase, and from nothing the model can call.
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
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

        var card = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(4),
            Child = row,
            MinHeight = 34,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return card;
    }

    /// <summary>
    /// A name, then a length. Voice first for the name — a name is a phrase — and the keyboard
    /// first for the number, which is exactly the per-call-site declaration Phase 25 asks for.
    /// </summary>
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
                    EntrySurface.Keyboard,
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

    /// <summary>
    /// A name, then a moment. The keyboard opens first for the time for the same reason it does
    /// for a timer's length: a number is easier typed than said.
    /// </summary>
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
                    EntrySurface.Keyboard,
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

    private static Control Clock(string caption, TextBlock time, TextBlock date)
    {
        var label = new TextBlock { Text = caption, FontSize = TypeScale.Small };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var card = new Border
        {
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(4),
            MinWidth = 180,
            Child = new StackPanel { Spacing = 2, Children = { label, time, date } },
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return card;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        return block;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
