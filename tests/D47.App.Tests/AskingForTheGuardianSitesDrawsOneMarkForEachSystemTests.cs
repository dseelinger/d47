using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Xunit;
using static D47.App.Tests.TheGalaxyMapDrawsWhereTheCommanderHasBeenTests;

namespace D47.App.Tests;

/// <summary>Asking for the Guardian sites adds their layer ticked and draws one mark for each system in the Guardian sites table.</summary>
public class AskingForTheGuardianSitesDrawsOneMarkForEachSystemTests
{
    private const string Sites = "Guardian sites";

    private const string TwoBodies = "Col 173 Sector HR-M b23-3";

    private static readonly IReadOnlyList<StarChartSystem> Systems = StarCharts.Systems(StarChartKind.GuardianSites);

    private static GalaxyMapPage Shown(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static IReadOnlyList<GalaxyMapLayout.Mark> SiteMarks(GalaxyMapPage page)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.GuardianSite)];
    }

    private static GalaxyMapPage Opened(Game game)
    {
        var (_, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.GuardianSites));
        Dispatcher.UIThread.RunJobs();

        return Shown(panel);
    }

    [AvaloniaFact]
    public void OpeningTheChartTicksTheLayerAndDrawsOneMarkForEachSystemAtEveryZoom()
    {
        var game = Played();
        game.Layers = SavedLayers();

        Assert.DoesNotContain(StarChartKind.GuardianSites, game.Layers.Read().OnMap);

        var page = Opened(game);

        Assert.Contains(StarChartKind.GuardianSites, game.Layers.Read().OnMap);
        Assert.True(page.Switch(Sites).IsChecked);

        page.Map().View(GalaxyViewport.Fitting([.. Systems.Select(system => system.At)])!);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Systems.Count, SiteMarks(page).Count);
        Assert.Equal(Systems.Select(system => system.Name).Order(StringComparer.Ordinal), SiteMarks(page).Select(mark => mark.Star.Name).Order(StringComparer.Ordinal));
        Assert.Contains("GUARDIAN SITE", page.LegendEntries());

        page.Map().View(GalaxyViewport.WholeGalaxy);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Systems.Count, SiteMarks(page).Count);
    }

    [AvaloniaFact]
    public void TheStripNamesTheKindAndCountsTheBodiesWithRuinsAndWithAStructure()
    {
        var game = Played();
        game.Layers = SavedLayers();
        var page = Opened(game);

        var picked = page.Map().TapAt(SiteMarks(page).Single(mark => mark.Star.Name == TwoBodies).At);

        Assert.Equal(TwoBodies, picked?.Name);
        Assert.Contains("GUARDIAN SITE: 1 body with ruins and 1 with a structure", page.StripKinds());
    }
}
