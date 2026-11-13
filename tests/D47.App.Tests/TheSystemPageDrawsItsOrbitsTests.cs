using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>SEARCH › SYSTEM › ORBITS draws one level of LTT 7786 at a time, its list nearest first, and runs simulated time (#2390).</summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageDrawsItsOrbitsTests
{
    private const long Ltt7786 = 633608311522;

    private static readonly DateTimeOffset Start = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static (PanelView Panel, Window Window, AnOrbitMarkerStaysOnItsDrawnPathTests.FakeClock Clock) Shown(StarSystemProfile? profile = null)
    {
        var clock = new AnOrbitMarkerStaysOnItsDrawnPathTests.FakeClock(Start);
        var systems = new TheSystemPageListsItsStationsTests.Systems(profile ?? TheSystemPageListsItsStationsTests.Ltt7786Profile());
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSearch();
        panel.EnableStarSystem(new StarSystemSurface(systems, () => new JournalLocation("LTT 7786", null, false, null) { SystemAddress = Ltt7786 })
        {
            Clock = clock,
        });

        var window = new Window { Content = panel, Width = 1280, Height = 1180 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        Until(() => Texts(panel).Contains("LAST REPORTED"));
        TheSystemPageListsItsStationsTests.Section(panel, "Orbits");

        return (panel, window, clock);
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static List<Control> Rows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Control>().Where(control => control.Classes.Contains(StarSystemPage.OrbitRowClass))];

    private static List<string> RowNames(Control root) => [.. Rows(root).Select(row => AutomationProperties.GetName(row)!)];

    private static List<Button> Crumbs(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains(StarSystemPage.OrbitCrumbClass))];

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetName(button) == name);

    private static string Readout(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Single(block => AutomationProperties.GetName(block) == "Simulated time").Text!;

    private static Stepper Speed(Control root) =>
        root.GetVisualDescendants().OfType<Stepper>().Single(stepper => AutomationProperties.GetName(stepper) == "Speed");

    private static void Frame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheRootListsItsChildrenNearestFirstAndOnlyParentsArePressable()
    {
        var (panel, window, _) = Shown();

        Assert.Equal(
            ["LTT 7786 1", "LTT 7786 2", "LTT 7786 3", "Barycentre 14", "LTT 7786 6", "Barycentre 28", "LTT 7786 9", "LTT 7786 10", "LTT 7786 11", "LTT 7786 12"],
            RowNames(panel));
        Assert.Equal(
            ["Barycentre 14", "LTT 7786 6", "Barycentre 28", "LTT 7786 9", "LTT 7786 10", "LTT 7786 11"],
            Rows(panel).OfType<Button>().Select(row => AutomationProperties.GetName(row)));
        Assert.Equal(["LTT 7786"], Crumbs(panel).Select(crumb => crumb.Content as string));
        Assert.Contains("0.314 AU", Texts(panel));
        Assert.Contains("1,094.04 d", Texts(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingBarycentre14ShowsItsPairAndTheRootCrumbReturns()
    {
        var (panel, window, _) = Shown();

        Press(Named(panel, "Barycentre 14"));

        Assert.Equal(["LTT 7786 4", "LTT 7786 5"], RowNames(panel));
        Assert.Equal(["LTT 7786", "Barycentre 14"], Crumbs(panel).Select(crumb => crumb.Content as string));
        Assert.Contains("Barycentre · 2 in orbit", Texts(panel));

        var nodes = Orrery.Nodes(TheSystemPageListsItsStationsTests.Ltt7786Profile());
        var level = OrbitLevel.Of(nodes, nodes.Single(node => node.BodyId == 14), Start);
        var four = level.Map(Orrery.Offset(level.Placed[0], Start)!.Value, 300);
        var five = level.Map(Orrery.Offset(level.Placed[1], Start)!.Value, 300);
        Assert.True((four.X * five.X) + (four.Y * five.Y) < 0, $"4 at {four} and 5 at {five} are on the same side.");

        Press(Crumbs(panel)[0]);

        Assert.Equal(10, RowNames(panel).Count);
        Assert.Equal(["LTT 7786"], Crumbs(panel).Select(crumb => crumb.Content as string));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRootOpensAtThePresentRunningAtOneDayPerSecond()
    {
        var (panel, window, clock) = Shown();

        Assert.Equal("1 day per second", Speed(panel).SelectedItem);
        Assert.Contains("2 / 5", Texts(panel));
        Assert.Contains("SIMULATED · RUNNING", Texts(panel));
        Assert.Equal("2026-11-13 12:00 UTC", Readout(panel));

        clock.Advance(TimeSpan.FromSeconds(10));
        Frame();
        Assert.Equal("2026-11-23 12:00 UTC", Readout(panel));

        var next = Speed(panel).GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Next");
        Press(next);
        Assert.Equal("1 week per second", Speed(panel).SelectedItem);

        clock.Advance(TimeSpan.FromSeconds(2));
        Frame();
        Assert.Equal("2026-12-07 12:00 UTC", Readout(panel));

        Press(Named(panel, "Pause or run"));
        Assert.Contains("SIMULATED · PAUSED", Texts(panel));
        Assert.Equal("RUN", Named(panel, "Pause or run").Content);

        clock.Advance(TimeSpan.FromSeconds(5));
        Frame();
        Assert.Equal("2026-12-07 12:00 UTC", Readout(panel));

        Press(panel.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "NOW"));
        Assert.Equal("2026-11-13 12:00 UTC", Readout(panel));
        Assert.Contains("SIMULATED · PAUSED", Texts(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void PausedOrHiddenTheViewAsksForNoFrames()
    {
        var (panel, window, clock) = Shown();

        var before = clock.Reads;
        Frame();
        Frame();
        Assert.True(clock.Reads > before, "Running and visible, the view drew no frame.");

        Press(Named(panel, "Pause or run"));
        Frame();
        var paused = clock.Reads;

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            panel.TickSearch();
            Frame();
        }

        Assert.Equal(paused, clock.Reads);

        Press(Named(panel, "Pause or run"));
        panel.Tab = PanelTab.Transcript;
        Frame();
        var hidden = clock.Reads;

        for (var i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            panel.TickSearch();
            Frame();
        }

        Assert.Equal(hidden, clock.Reads);

        panel.Tab = PanelTab.Search;
        panel.TickSearch();
        Frame();
        Frame();
        Assert.True(clock.Reads > hidden, "Shown again, the view drew no frame.");

        window.Close();
    }

    [AvaloniaFact]
    public void AChildWithNoOrbitIsNotDrawnAndSaysSo()
    {
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        profile = profile with
        {
            Bodies = [.. profile.Bodies.Select(body => body.Name == "LTT 7786 12" ? body with { MeanAnomaly = null } : body)],
        };

        var (panel, window, _) = Shown(profile);

        Assert.DoesNotContain("LTT 7786 12", RowNames(panel));
        Assert.Equal(9, RowNames(panel).Count);
        Assert.Contains("No orbit on record for LTT 7786 12.", Texts(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void OrbitsIsCaptured()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = Shown();
        Frame();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("system-orbits.png");
        }

        Press(Named(panel, "Barycentre 14"));
        Frame();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("system-orbits-barycentre-14.png");
        }

        Press(Crumbs(panel)[0]);
        Press(Named(panel, "LTT 7786 9"));
        window.Width = 1024;
        Frame();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("system-orbits-9-1024.png");
        }

        window.Close();
    }
}
