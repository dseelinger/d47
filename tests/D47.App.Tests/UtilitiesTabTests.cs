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
/// Clocks, timers and alarms as a tab of the panel (list.md Phase 24, "Utilities").
/// <para>
/// The clock is fed an instant rather than reading one, which is the same rule Core follows and
/// the reason a test can assert what it says: 2026 presents as 3312 beside it, from one instant.
/// </para>
/// </summary>
public class UtilitiesTabTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);

    private static (Window Window, PanelView Panel, Timekeeper Timekeeper) Open(string root)
    {
        var alarms = new AlarmStore(Path.Combine(root, "alarms.json"), NullLogger<AlarmStore>.Instance);
        var timekeeper = new Timekeeper(alarms);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableUtilities(timekeeper, alarms, () => Instant, () => TimeZoneInfo.Utc);

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
