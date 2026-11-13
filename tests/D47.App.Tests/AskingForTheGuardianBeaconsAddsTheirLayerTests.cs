using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>Opening the Guardian beacons chart puts its layer on the Galaxy map ticked and fits the view to it (#2356).</summary>
public class AskingForTheGuardianBeaconsAddsTheirLayerTests
{
    private const string Beacons = "Guardian beacons";

    private static readonly IReadOnlyList<StarChartSystem> BeaconSystems = StarCharts.Systems(StarChartKind.GuardianBeacons);

    private static PageRef Chart => PageRef.StarChart(StarChartKind.GuardianBeacons);

    private static GalaxyMapPage Page(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    /// <summary>A routing with <paramref name="panel"/> as its only surface, opening a chart through the panel's own hook.</summary>
    private static PanelRouting Routed(PanelView panel)
    {
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        return routing;
    }

    private static Window Windowed(PanelView panel)
    {
        var window = new Window
        {
            Content = panel,
            Width = 960,
            Height = 930,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IReadOnlyList<GalaxyMapLayout.Mark> BeaconMarks(GalaxyMapPage page)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.GuardianBeacon)];
    }

    private static IReadOnlyList<string> RailTitles(GalaxyMapPage page) =>
        [.. page.GetVisualDescendants().OfType<CheckBox>().Select(AutomationProperties.GetName).OfType<string>()];

    private static void AssertTheBeaconsAreDrawnAndInView(GalaxyMapPage page)
    {
        var map = page.Map();
        var bounds = new Rect(map.Bounds.Size);

        Assert.True(page.Switch(Beacons).IsChecked);
        Assert.Equal(28, BeaconMarks(page).Count);
        Assert.Equal(28, BeaconMarks(page).Select(mark => mark.Star.Name).Distinct().Count());
        Assert.All(BeaconSystems, system => Assert.True(bounds.Contains(map.Projected(system.At)), system.Name));
    }

    [AvaloniaFact]
    public void OpeningTheChartShowsTheGalaxyMapWithTheBeaconsAddedAndTheOtherSwitchesAsTheyWere()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var panel = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Panel(game, headset: false, root: RoutingPages.PlanRoot);
        Windowed(panel);
        var routing = Routed(panel);

        panel.Nav.SelectRoot(PanelTab.Astrometrics, RoutingPages.GalaxyMapRoot);
        Dispatcher.UIThread.RunJobs();
        Page(panel).Switch("Bookmarks").IsChecked = false;
        panel.Nav.SelectRoot(PanelTab.Astrometrics, RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();

        var page = Page(panel);

        Assert.Equal(RoutingPages.GalaxyMapRoot, panel.Nav.Root.Key);
        AssertTheBeaconsAreDrawnAndInView(page);
        Assert.False(game.Layers.Read().Ticked(StarChartKind.Bookmarks));
        Assert.All(
            StarCharts.Default.Where(kind => kind != StarChartKind.Bookmarks),
            kind => Assert.True(game.Layers.Read().Ticked(kind), StarCharts.Info(kind).Title));
        Assert.Contains(StarChartKind.GuardianBeacons, game.Layers.Read().OnMap);
        Assert.True(game.Layers.Read().Ticked(StarChartKind.GuardianBeacons));
        Assert.Contains("GUARDIAN BEACON", page.LegendEntries());
    }

    [AvaloniaFact]
    public void TheViewKeepsItsHeadingAndTilt()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, page) = InWindow(game);
        var routing = Routed(panel);

        page.Map().View(page.Map().Viewport.Turned(1).Tilted(1));
        Dispatcher.UIThread.RunJobs();
        var (heading, tilt) = (page.Map().Viewport.Heading, page.Map().Viewport.Tilt);

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(heading, page.Map().Viewport.Heading);
        Assert.Equal(tilt, page.Map().Viewport.Tilt);
        AssertTheBeaconsAreDrawnAndInView(page);
    }

    [AvaloniaFact]
    public void AChartOpenedBeforeThePageWasBuiltIsShownWhenItIs()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var panel = TheGalaxyMapDrawsWhereTheCommanderHasBeenTests.Panel(game, headset: false, root: RoutingPages.PlanRoot);
        Windowed(panel);

        Assert.Empty(panel.GetVisualDescendants().OfType<GalaxyMapPage>());

        panel.OpenChart(StarChartKind.GuardianBeacons);
        panel.Nav.SelectRoot(PanelTab.Astrometrics, RoutingPages.GalaxyMapRoot);
        Dispatcher.UIThread.RunJobs();

        AssertTheBeaconsAreDrawnAndInView(Page(panel));
    }

    [AvaloniaFact]
    public void TheBigPanelShowsTheSameLayer()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (surface, panel, page) = OnThePanel(game);
        var routing = Routed(panel);

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();
        surface.Render();

        AssertTheBeaconsAreDrawnAndInView(page);
    }

    [AvaloniaFact]
    public void WithNineLayersTheRailTurnsToThePageHoldingTheBeaconsAndPagesBackToSevenRows()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, page) = InWindow(game);
        var routing = Routed(panel);

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("LAYERS · 9", AutomationProperties.GetName(Tile(page, "LAYERS · 9")));
        Assert.Equal(["Unvisited", Beacons], RailTitles(page));
        Assert.Contains("8–9 of 9", page.GetVisualDescendants().OfType<TextBlock>().Select(AutomationProperties.GetName));

        Tile(page, "Previous layers").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StarCharts.Default.Take(7).Select(kind => StarCharts.Info(kind).Title), RailTitles(page));
        Assert.Contains("1–7 of 9", page.GetVisualDescendants().OfType<TextBlock>().Select(AutomationProperties.GetName));
    }

    [AvaloniaFact]
    public void ABeaconSystemIsSelectableAndItsCentreAndBookmarkButtonsWork()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, page) = InWindow(game);
        var routing = Routed(panel);
        var beacon = BeaconSystems.Single(system => system.Name == "HIP 36823");

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();

        var picked = page.Map().TapAt(BeaconMarks(page).Single(mark => mark.Star.Name == beacon.Name).At);

        Assert.Equal(beacon.Name, picked?.Name);
        Assert.Equal(["GUARDIAN BEACON"], page.StripKinds().Select(kind => kind[..kind.IndexOf(':', StringComparison.Ordinal)]));

        Tile(page, GalaxyMapPage.CentreHere).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(beacon.At.X, page.Map().Viewport.CentreX, 3);
        Assert.Equal(beacon.At.Z, page.Map().Viewport.CentreZ, 3);

        Tile(page, GalaxyMapPage.Bookmark).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(game.Bookmarks.For("F1"), bookmark => bookmark.System == beacon.Name);
    }

    [AvaloniaFact]
    public void AnUntickedBeaconsLayerIsTickedAgainByTheRequest()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, page) = InWindow(game);
        var routing = Routed(panel);

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();
        page.Switch(Beacons).IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(BeaconMarks(page));

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();

        AssertTheBeaconsAreDrawnAndInView(page);
    }

    [AvaloniaFact]
    public void TheLayerIsKeptByTheNextPageBuiltAndAMapWithNothingSavedLeavesItOff()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);

        Assert.DoesNotContain(StarChartKind.GuardianBeacons, game.Layers.Read().OnMap);

        Routed(panel).Open(Chart);
        Dispatcher.UIThread.RunJobs();

        var (_, _, second) = InWindow(game);

        Assert.Equal("LAYERS · 9", AutomationProperties.GetName(Tile(second, "LAYERS · 9")));
        second.Map().View(GalaxyViewport.Fitting([.. BeaconSystems.Select(system => system.At)])!);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(28, BeaconMarks(second).Count);
    }

    [AvaloniaFact]
    public void ASwitchMovedInTheWindowStaysMovedWhenTheBigPanelAddsTheChartToo()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var (_, window, windowPage) = InWindow(game);
        var (surface, headset, headsetPage) = OnThePanel(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(window.Nav, action => action(), leads: true, openChart: window.OpenChart);
        routing.RouteNavigation(headset.Nav, action => action(), openChart: headset.OpenChart);

        windowPage.Switch("Bookmarks").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        routing.Open(Chart);
        Dispatcher.UIThread.RunJobs();
        surface.Render();

        Assert.False(game.Layers.Read().Ticked(StarChartKind.Bookmarks));
        Assert.True(game.Layers.Read().Ticked(StarChartKind.GuardianBeacons));
        AssertTheBeaconsAreDrawnAndInView(headsetPage);
    }
}
