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

/// <summary>The Missions layer draws a mark for each system an active mission is bound for, and follows the mission board.</summary>
public class TheMissionDestinationsLayerDrawsAMarkForEachDestinationSystemTests
{
    private const string Visited = "Bubble 5";
    private const string Unvisited = "Nowhere Known";

    private static GalaxyMapPage Shown(Control panel) => panel.GetVisualDescendants().OfType<GalaxyMapPage>().Single();

    private static IReadOnlyList<GalaxyMapLayout.Mark> MissionMarks(GalaxyMapPage page)
    {
        var layout = (GalaxyMapLayout)typeof(GalaxyMapView).GetField("_layout", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page.Map())!;

        return [.. layout.Marks.Where(mark => mark.Kind == GalaxyMapMarkKind.Mission)];
    }

    private static void Apply(Game game, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        game.Commander.Apply(parsed!);
    }

    private static void Accept(Game game, long id, string system, string? station = null) =>
        Apply(
            game,
            "{\"timestamp\":\"2026-10-21T11:00:00Z\",\"event\":\"MissionAccepted\",\"MissionID\":" + id
            + ",\"Name\":\"Mission_Courier\",\"LocalisedName\":\"Courier " + id + "\",\"DestinationSystem\":\"" + system + "\""
            + (station is null ? string.Empty : ",\"DestinationStation\":\"" + station + "\"") + "}");

    private static string UnplacedLine(GalaxyMapPage page) =>
        page.Lines().SingleOrDefault(line => line.Contains(" in a system ", StringComparison.Ordinal)) ?? string.Empty;

    private static GalaxyMapPage Opened(Game game)
    {
        game.Layers = SavedLayers();
        var (_, panel, _) = InWindow(game);
        var routing = new PanelRouting(() => false, () => []);
        routing.RouteNavigation(panel.Nav, action => action(), leads: true, openChart: panel.OpenChart);

        routing.Open(PageRef.StarChart(StarChartKind.MissionDestinations));
        Dispatcher.UIThread.RunJobs();

        return Shown(panel);
    }

    [AvaloniaFact]
    public void TwoMissionsToTwoSystemsDrawOneMarkAndCountOneUnplaced()
    {
        var game = Played();
        Accept(game, 1, Visited);
        Accept(game, 2, Unvisited);

        var page = Opened(game);

        Assert.Equal([Visited], MissionMarks(page).Select(mark => mark.Star.Name));
        Assert.True(page.Switch("Missions").IsChecked);
        Assert.Contains("MISSION DESTINATION", page.LegendEntries());
        Assert.Equal("1 mission destination is in a system you have not visited.", UnplacedLine(page));
    }

    [AvaloniaFact]
    public void CompletingAMissionRemovesItsMarkOnTheNextTick()
    {
        var game = Played();
        Accept(game, 1, Visited);
        var page = Opened(game);

        Assert.Single(MissionMarks(page));

        Apply(game, "{\"timestamp\":\"2026-10-21T12:00:00Z\",\"event\":\"MissionCompleted\",\"MissionID\":1}");

        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(MissionMarks(page));
    }

    [AvaloniaFact]
    public void TheStripListsEachMissionBoundForTheSystemWithItsStation()
    {
        var game = Played();
        Accept(game, 1, Visited, "Hub");
        Accept(game, 2, Visited);

        var page = Opened(game);
        var picked = page.Map().TapAt(MissionMarks(page).Single().At);

        Assert.Equal(Visited, picked?.Name);
        Assert.Equal(["MISSION: Courier 1 at Hub", "MISSION: Courier 2"], page.StripKinds());
    }
}
