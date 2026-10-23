using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class ARouteMapDrawsTheStartAndEndOfAPlanTests
{
    private static RouteEndpoint Endpoint(string name, double x) => new(name, new StarPosition(x, 0, 0));

    private static RouteWaypoint Waypoint(string name, double x, bool neutron = false) =>
        new(name, 1, null, neutron) { Position = new StarPosition(x, 0, 0) };

    private static RichesStop Riches(string name, double x) =>
        new(name, 1, []) { Position = new StarPosition(x, 0, 0) };

    private static ExobiologyStop Exo(string name, double x) =>
        new(name, 1, []) { Position = new StarPosition(x, 0, 0) };

    private static CarrierWaypoint CarrierStop(string name, bool restock) =>
        new(name, 0, 0, 0, 0, restock, 0, 0, false, false, false) { Position = new StarPosition(1, 2, 3) };

    private static PlottedRoute Jump(RouteEndpoint? start) =>
        new("Sol", "Colonia", 100, 2, [Waypoint("A", 1, neutron: true), Waypoint("B", 2)]) { Start = start };

    [Fact]
    public void AJumpPlanListsItsStartThenEveryWaypoint()
    {
        var stops = RouteMapStop.From(Jump(Endpoint("Sol", 0)));

        Assert.Equal(["Sol", "A", "B"], stops.Select(stop => stop.System));
    }

    [Fact]
    public void AJumpPlanWithNoStartListsOnlyItsWaypoints()
    {
        Assert.Equal(["A", "B"], RouteMapStop.From(Jump(null)).Select(stop => stop.System));
    }

    [Fact]
    public void AJumpPlanMarksItsNeutronWaypointsOnly()
    {
        var stops = RouteMapStop.From(Jump(Endpoint("Sol", 0)));

        Assert.Equal([RouteStopMarks.None, RouteStopMarks.Neutron, RouteStopMarks.None], stops.Select(stop => stop.Marks));
    }

    [Fact]
    public void ARichesLoopListsStartStopsThenEnd()
    {
        var route = new RichesRoute([Riches("A", 1), Riches("B", 2)]) { Start = Endpoint("Home", 0), End = Endpoint("Home", 0) };

        Assert.Equal(["Home", "A", "B", "Home"], RouteMapStop.From(route).Select(stop => stop.System));
    }

    [Fact]
    public void ARichesRouteWithNeitherEndListsOnlyItsStops()
    {
        Assert.Equal(["A", "B"], RouteMapStop.From(new RichesRoute([Riches("A", 1), Riches("B", 2)])).Select(stop => stop.System));
    }

    [Fact]
    public void AnExobiologyLoopListsStartStopsThenEnd()
    {
        var route = new ExobiologyRoute([Exo("A", 1), Exo("B", 2)]) { Start = Endpoint("Home", 0), End = Endpoint("Home", 0) };

        Assert.Equal(["Home", "A", "B", "Home"], RouteMapStop.From(route).Select(stop => stop.System));
    }

    [Fact]
    public void AnExobiologyRouteWithOnlyAnEndListsItLast()
    {
        var route = new ExobiologyRoute([Exo("A", 1)]) { End = Endpoint("Away", 5) };

        Assert.Equal(["A", "Away"], RouteMapStop.From(route).Select(stop => stop.System));
    }

    [Fact]
    public void ReachedShiftsByOneWhereThePlanHasAStart()
    {
        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Jump, Jump = Jump(Endpoint("Sol", 0)), Reached = 1 };

        Assert.Equal(2, RouteMapStop.ReachedIndex(plan));
    }

    [Fact]
    public void ReachedIsUnchangedWhereThePlanHasNoStart()
    {
        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Jump, Jump = Jump(null), Reached = 1 };

        Assert.Equal(1, RouteMapStop.ReachedIndex(plan));
    }

    [Fact]
    public void NothingReachedIsMinusOne()
    {
        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Jump, Jump = Jump(Endpoint("Sol", 0)), Reached = null };

        Assert.Equal(-1, RouteMapStop.ReachedIndex(plan));
    }

    [Fact]
    public void ReachedOnACarrierPlanIsUnchanged()
    {
        var plan = new StoredRoutePlan { Kind = RoutePlanKind.Carrier, Carrier = new CarrierRoute([]), Reached = 3 };

        Assert.Equal(3, RouteMapStop.ReachedIndex(plan));
    }

    [Fact]
    public void ARouteSavedWithoutPositionsDrawsNoMap()
    {
        var route = new RichesRoute([new RichesStop("A", 1, []), new RichesStop("B", 1, [])]);

        var positions = RouteMapStop.From(route).Where(stop => stop.Position is not null).Select(stop => stop.Position!.Value).ToList();

        Assert.Null(GalaxyViewport.Fitted(positions, 360, 20));
    }

    [Fact]
    public void ANavRouteMarksItsStarClasses()
    {
        var route = new NavRoute
        {
            Hops =
            [
                new RouteHop("N", "N"),
                new RouteHop("D", "DA"),
                new RouteHop("H", "H"),
                new RouteHop("W", "W"),
                new RouteHop("G", "G"),
                new RouteHop("U", null),
                new RouteHop("Q", "Nonsense") { Position = (1, 2, 3) },
            ],
        };

        var stops = RouteMapStop.From(route);

        Assert.Equal(RouteStopMarks.Neutron | RouteStopMarks.NotScoopable, stops[0].Marks);
        Assert.Equal(RouteStopMarks.WhiteDwarf | RouteStopMarks.NotScoopable, stops[1].Marks);
        Assert.Equal(RouteStopMarks.BlackHole | RouteStopMarks.NotScoopable, stops[2].Marks);
        Assert.Equal(RouteStopMarks.NotScoopable, stops[3].Marks);
        Assert.Equal(RouteStopMarks.None, stops[4].Marks);
        Assert.Equal(RouteStopMarks.None, stops[5].Marks);
        Assert.Equal(RouteStopMarks.None, stops[6].Marks);
        Assert.Equal(new StarPosition(1, 2, 3), stops[6].Position);
        Assert.Null(stops[0].Position);
    }

    [Fact]
    public void ACarrierRouteMarksOnlyItsRestocks()
    {
        var stops = RouteMapStop.From(new CarrierRoute([CarrierStop("A", false), CarrierStop("B", true)]));

        Assert.Equal(RouteStopMarks.None, stops[0].Marks);
        Assert.Equal(RouteStopMarks.Restock, stops[1].Marks);
        Assert.Equal(new StarPosition(1, 2, 3), stops[1].Position);
    }
}
