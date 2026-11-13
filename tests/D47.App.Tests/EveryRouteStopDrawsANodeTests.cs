using Avalonia;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>A route map draws a node on every stop, by scoopability (#2393).</summary>
public class EveryRouteStopDrawsANodeTests
{
    private static GalaxyMapLayout Lay(IReadOnlyList<RouteMapStop> stops, double across = 400) =>
        GalaxyMapLayout.Of(
            new GalaxyMapScene(false, null, stops, -1, null, null),
            GalaxyViewport.Around(new StarPosition(0, 0, 0)) with { LightYearsAcross = across },
            new Size(600, 600),
            fitInset: 20);

    private static IReadOnlyList<RouteMapStop> NineSystems()
    {
        var route = new NavRoute
        {
            Hops =
            [
                .. new[] { "K", "G", "M", "K", "G", "M", "L", null, "G" }.Select((starClass, index) =>
                    new RouteHop($"S{index}", starClass) { Position = (index * 10, 0, index % 2 * 5) }),
            ],
        };

        return RouteMapStop.From(route);
    }

    [Fact]
    public void ANineSystemRouteDrawsOneNodePerStopByScoopability()
    {
        var layout = Lay(NineSystems());

        Assert.Equal(
            [
                RouteStar.Scoopable, RouteStar.Scoopable, RouteStar.Scoopable, RouteStar.Scoopable,
                RouteStar.Scoopable, RouteStar.Scoopable, RouteStar.NoFuel, RouteStar.Unknown, RouteStar.Scoopable,
            ],
            layout.Stars.OrderBy(star => star.At.X).Select(star => star.Kind));
    }

    [Fact]
    public void ACarrierRouteDrawsAPlainNodeAndTheRestockMark()
    {
        var stops = RouteMapStop.From(new CarrierRoute(
        [
            new CarrierWaypoint("A", 0, 0, 0, 0, false, 0, 0, false, false, false) { Position = new StarPosition(0, 0, 0) },
            new CarrierWaypoint("B", 0, 0, 0, 0, true, 0, 0, false, false, false) { Position = new StarPosition(10, 0, 0) },
        ]));

        Assert.Equal([RouteStar.Plain, RouteStar.Restock], Lay(stops).Stars.OrderBy(star => star.At.X).Select(star => star.Kind));
    }

    [Fact]
    public void AnUnclassedPlanDrawsTheUnknownLookOnEveryStop()
    {
        var stops = new[]
        {
            new RouteMapStop("A", new StarPosition(0, 0, 0), RouteStopMarks.None),
            new RouteMapStop("B", new StarPosition(10, 0, 0), RouteStopMarks.Neutron),
            new RouteMapStop("C", new StarPosition(20, 0, 0), RouteStopMarks.None),
        };

        Assert.Equal(
            [RouteStar.Unknown, RouteStar.Neutron, RouteStar.Unknown],
            Lay(stops).Stars.OrderBy(star => star.At.X).Select(star => star.Kind));
    }

    [Fact]
    public void NodesThatTouchAreAllDrawn()
    {
        var stops = Enumerable.Range(0, 5).Select(index => new RouteMapStop($"S{index}", new StarPosition(index * 0.01, 0, 0), RouteStopMarks.Scoopable))
            .Append(new RouteMapStop("End", new StarPosition(500, 0, 0), RouteStopMarks.Scoopable))
            .ToList();

        Assert.Equal(6, Lay(stops).Stars.Count);
    }

    [Fact]
    public void ANodeDrawsBeforeAHazardMarkSoTheMarkSitsOverIt()
    {
        var stops = new[]
        {
            new RouteMapStop("A", new StarPosition(0, 0, 0), RouteStopMarks.Neutron | RouteStopMarks.NotScoopable),
            new RouteMapStop("B", new StarPosition(10, 0, 0), RouteStopMarks.Scoopable),
        };

        Assert.Equal([RouteStar.Scoopable, RouteStar.Neutron], Lay(stops).Stars.Select(star => star.Kind));
    }

    [Fact]
    public void ZoomedOutPastTheDetailNoStopIsDrawn()
    {
        var layout = GalaxyMapLayout.Of(
            new GalaxyMapScene(false, null, NineSystems(), -1, null, null),
            GalaxyViewport.WholeGalaxy,
            new Size(600, 600));

        Assert.Empty(layout.Stars);
        Assert.True(layout.Drawn.StopsHidden);
    }
}
