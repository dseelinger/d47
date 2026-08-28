using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Clocks, timers and alarms as a tab of the panel (Phase 24, "Utilities").
/// <para>
/// The clock is fed an instant rather than reading one, which is the same rule Core follows and
/// the reason a test can assert what it says: 2026 presents as 3312 beside it, from one instant.
/// </para>
/// </summary>
public class UtilitiesTabTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);

    /// <summary>A clock a test can move, for the ticks that have to change something.</summary>
    private sealed class Clock
    {
        public DateTimeOffset Now { get; set; } = Instant;
    }

    private static (Window Window, PanelView Panel, Timekeeper Timekeeper) Open(string root) =>
        Open(root, new Clock());

    private static (Window Window, PanelView Panel, Timekeeper Timekeeper) Open(string root, Clock clock)
    {
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableUtilities(timekeeper, alarms, () => clock.Now, () => TimeZoneInfo.Utc);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        Dispatcher.UIThread.RunJobs();

        return (window, panel, timekeeper);
    }

    private static IReadOnlyList<string> Text(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>The tab arrives when the host furnishes it, on both surfaces.</summary>
    [AvaloniaFact]
    public void TheTabIsThereOnceTheHostGivesIt()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        Assert.False(panel.FindControl<Control>("UtilitiesTab")!.IsVisible);

        var root = TempFolders.Create("d47-utilities-tests");
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);

        panel.EnableUtilities(new Timekeeper(alarms), alarms, () => Instant, () => TimeZoneInfo.Utc);

        Assert.True(panel.FindControl<Control>("UtilitiesTab")!.IsVisible);
    }

    /// <summary>
    /// Both clocks, from one instant. They cannot disagree because there is only one of them to
    /// be wrong.
    /// </summary>
    [AvaloniaFact]
    public void BothClocksShowTheSameMoment()
    {
        var (window, panel, _) = Open(TempFolders.Create("d47-utilities-tests"));

        var shown = Text(panel);

        Assert.Contains("17 August 3312", shown);
        Assert.Equal(2, shown.Count(line => line == "21:04"));

        window.Close();
    }

    /// <summary>A countdown shows how long is left; an alarm shows the time it goes off at.</summary>
    [AvaloniaFact]
    public void WhatIsRunningIsListedWithWhenItIsDue()
    {
        var (window, panel, timekeeper) = Open(TempFolders.Create("d47-utilities-tests"));

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);
        timekeeper.SetAlarm("wake up", Instant.AddHours(9), Instant);

        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        var shown = Text(panel);

        Assert.Contains("mining run", shown);
        Assert.Contains("40 min", shown);
        Assert.Contains("wake up", shown);
        Assert.Contains("06:04", shown);

        window.Close();
    }

    /// <summary>
    /// A tick that changes only the countdown does not rebuild the row (remediation.md 17,
    /// item 14).
    /// <para>
    /// Reported from the headset: *"the Utilities tab flickers a lot — other tabs are fine"*. The
    /// running list was cleared and rebuilt on every tick, and the headset rasterises the tree on
    /// its own cadence rather than the window's, so it catches the moment between the clear and
    /// the re-add and draws an empty list. The desktop window never showed it because it composes
    /// the frame after the tick has finished.
    /// </para>
    /// <para>
    /// The assertion is reference identity, because that is the thing that was wrong: the text has
    /// to change on every tick and the controls must not.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ATickWritesTheCountdownWithoutRebuildingTheRow()
    {
        var clock = new Clock();
        var (window, panel, timekeeper) = Open(TempFolders.Create("d47-utilities-tests"), clock);

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);

        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        var rows = panel.GetVisualDescendants().OfType<Border>().ToList();

        Assert.Contains("40 min", Text(panel));

        clock.Now = Instant.AddMinutes(11);

        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        // The countdown moved.
        Assert.Contains("29 min", Text(panel));

        // And every control that was on screen is the same object it was.
        Assert.Equal(rows, panel.GetVisualDescendants().OfType<Border>().ToList());

        window.Close();
    }

    /// <summary>
    /// Cancelling is the Commander's, and the panel is one of the two places that can. The other
    /// is a spoken phrase; the model has neither.
    /// </summary>
    [AvaloniaFact]
    public void CancellingFromThePanelTakesItOff()
    {
        var (window, panel, timekeeper) = Open(TempFolders.Create("d47-utilities-tests"));

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);

        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        var cancel = panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Cancel");

        cancel.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(timekeeper.Running);

        window.Close();
    }

    /// <summary>The page at the size the headset renders it, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheUtilitiesTabRendersToACapture()
    {
        var root = TempFolders.Create("d47-utilities-tests");
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);

        timekeeper.StartTimer("mining run", TimeSpan.FromMinutes(40), Instant);
        timekeeper.SetAlarm("wake up", Instant.AddHours(9), Instant);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableUtilities(timekeeper, alarms, () => Instant, () => TimeZoneInfo.Utc);

        var window = new Window { Content = panel, Width = 1024, Height = 640 };
        window.Show();

        panel.Tab = PanelTab.Utilities;
        panel.TickClocks();
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "utilities-tab.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}
