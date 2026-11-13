using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>The First discovered layer marks each system where the Commander scanned an undiscovered body, as a star up close and a blue square wider.</summary>
public class TheFirstDiscoveredLayerMarksEachSystemYouDiscoveredABodyInTests
{
    private static readonly DateTimeOffset Origin = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero).AddDays(-600);

    private const string Load = "{\"timestamp\":\"2025-01-01T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Kestrel Vane\"}";

    private const long Bubble5 = 1005;
    private const long Bubble10 = 1010;
    private const long Wregoe = 1122;

    private static string Scan(DateTimeOffset at, string system, long address, int body, bool discovered) =>
        "{\"timestamp\":\"" + at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) + "\",\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\""
        + system + " " + body + "\",\"BodyID\":" + body + ",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address
        + ",\"WasDiscovered\":" + (discovered ? "true" : "false") + ",\"WasMapped\":false}";

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static GalaxyMapPage Shown(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static GalaxyMapLayout Layout(GalaxyMapPage page) =>
        (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

    private static IReadOnlyList<GalaxyMapMark> Placed(GalaxyMapPage page)
    {
        var scene = (GalaxyMapScene)typeof(GalaxyMapView).GetField("_scene", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. (scene.Marks ?? []).Where(mark => mark.Kind == GalaxyMapMarkKind.FirstDiscovery)];
    }

    private static IReadOnlyList<GalaxyMapLayout.Mark> DiscoveryMarks(GalaxyMapPage page) =>
        [.. Layout(page).Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.FirstDiscovery)];

    private static Game WithScans()
    {
        var game = Played();

        game.Travel.Fold(
        [
            Load,
            Scan(Origin.AddHours(15.5), "Bubble 5", Bubble5, 1, discovered: false),
            Scan(Origin.AddHours(15.6), "Bubble 5", Bubble5, 2, discovered: false),
            Scan(Origin.AddHours(30.5), "Bubble 10", Bubble10, 1, discovered: true),
        ]);

        return game;
    }

    private static GalaxyMapPage Opened(Game game)
    {
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.FirstDiscovered));
        Dispatcher.UIThread.RunJobs();

        return Shown(panel);
    }

    private static void ViewAcross(GalaxyMapPage page, StarPosition at, double across)
    {
        page.Map().View(GalaxyViewport.Around(at) with { LightYearsAcross = across });
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ScansInTwoSystemsMarkOnlyTheOneWithUndiscoveredBodiesAndItsStripCountsThem()
    {
        var page = Opened(WithScans());
        var at = Assert.Single(Placed(page)).At;

        ViewAcross(page, at, GalaxyMapView.DetailAcross);

        var mark = Assert.Single(DiscoveryMarks(page));
        Assert.Equal("Bubble 5", mark.Star.Name);
        Assert.True(page.Switch("First discovered").IsChecked);
        Assert.Contains("FIRST DISCOVERY", page.LegendEntries());

        Assert.Equal("Bubble 5", page.Map().TapAt(mark.At)?.Name);
        Assert.Equal(["FIRST DISCOVERY: 2 bodies"], page.StripKinds());
    }

    [AvaloniaFact]
    public void WiderThanTheDetailViewTheSystemIsABlueSquareAndNoStar()
    {
        var page = Opened(WithScans());
        var at = Assert.Single(Placed(page)).At;

        ViewAcross(page, at, GalaxyMapView.DetailAcross * 2);

        Assert.Empty(DiscoveryMarks(page));
        var square = Assert.Single(Layout(page).Discovered);
        Assert.Equal("Bubble 5", square.Star.Name);
        Assert.True(page.Map().Drawn.DiscoveredSquares);
        Assert.Contains("FIRST DISCOVERY", page.LegendEntries());

        Assert.Equal("Bubble 5", page.Map().TapAt(square.At)?.Name);
        Assert.Equal(["FIRST DISCOVERY: 2 bodies"], page.StripKinds());
    }

    [AvaloniaFact]
    public void ALiveScanOfAnUndiscoveredBodyAddsTheMarkOnTheNextTick()
    {
        var game = Played();
        var page = Opened(game);

        Assert.Empty(Placed(page));

        game.Travel.Apply([Event(Load), Event(Scan(new DateTimeOffset(2026, 10, 21, 12, 30, 0, TimeSpan.Zero), "Wregoe TC-X b29-0", Wregoe, 4, discovered: false))]);

        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Wregoe TC-X b29-0"], Placed(page).Select(mark => mark.System));
    }
}
