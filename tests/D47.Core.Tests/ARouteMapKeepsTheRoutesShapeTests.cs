using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class ARouteMapKeepsTheRoutesShapeTests
{
    private static RouteMapStop Stop(string name, double x, double z, double y = 0) =>
        new(name, new StarPosition(x, y, z), RouteStopMarks.None);

    private static RouteMapStop NoPosition(string name) => new(name, null, RouteStopMarks.None);

    private static CarrierWaypoint Waypoint(string name, bool restock) =>
        new(name, 0, 0, 0, 0, restock, 0, 0, false, false, false) { Position = new StarPosition(1, 2, 3) };

    [Fact]
    public void TheLongerSideFillsTheSquareAndTheOtherIsCentred()
    {
        var layout = RouteMapLayout.For([Stop("A", 0, 0), Stop("B", 100, 0), Stop("C", 100, 50)], -1, null)!;

        Assert.Equal((0.0, 0.25), (layout.Points[0].X, layout.Points[0].Y));
        Assert.Equal((1.0, 0.25), (layout.Points[1].X, layout.Points[1].Y));
        Assert.Equal((1.0, 0.75), (layout.Points[2].X, layout.Points[2].Y));
    }

    [Fact]
    public void HeightIsIgnored()
    {
        var flat = RouteMapLayout.For([Stop("A", 0, 0), Stop("B", 40, 20)], -1, null)!;
        var tall = RouteMapLayout.For([Stop("A", 0, 0, y: 500), Stop("B", 40, 20, y: -900)], -1, null)!;

        Assert.Equal(flat.Points, tall.Points);
    }

    [Fact]
    public void AVerticalRouteDrawsAsAShortClusterInTheMiddle()
    {
        var layout = RouteMapLayout.For([Stop("A", 5, 0), Stop("B", 5, 0.5)], -1, null)!;

        Assert.All(layout.Points, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
        Assert.Equal(0.5, layout.Points[0].X);
        Assert.Equal(0.25, layout.Points[0].Y, 6);
        Assert.Equal(0.75, layout.Points[1].Y, 6);
    }

    [Fact]
    public void GalacticPlusZIsUp()
    {
        var layout = RouteMapLayout.For([Stop("A", 0, 0), Stop("B", 0, 100)], -1, null)!;

        Assert.True(layout.Points[1].Y > layout.Points[0].Y);
    }

    [Fact]
    public void LegsFollowTheStopReached()
    {
        var stops = new[] { Stop("A", 0, 0), Stop("B", 10, 0), Stop("C", 20, 0) };

        Assert.Equal([RouteLeg.Behind, RouteLeg.Here, RouteLeg.Ahead], RouteMapLayout.For(stops, 1, null)!.Points.Select(p => p.Leg));
        Assert.Equal([RouteLeg.Behind, RouteLeg.Behind, RouteLeg.Here], RouteMapLayout.For(stops, 2, null)!.Points.Select(p => p.Leg));
        Assert.All(RouteMapLayout.For(stops, -1, null)!.Points, point => Assert.Equal(RouteLeg.Ahead, point.Leg));
    }

    [Fact]
    public void AStopWithoutAPositionIsSkippedButStillCountsForTheLegs()
    {
        var layout = RouteMapLayout.For([Stop("A", 0, 0), NoPosition("B"), Stop("C", 10, 0)], 1, null)!;

        Assert.Equal(["A", "C"], layout.Points.Select(p => p.System));
        Assert.Equal([RouteLeg.Behind, RouteLeg.Ahead], layout.Points.Select(p => p.Leg));
    }

    [Fact]
    public void FewerThanTwoPositionedStopsGiveNoLayout()
    {
        Assert.Null(RouteMapLayout.For([], -1, null));
        Assert.Null(RouteMapLayout.For([Stop("A", 0, 0), NoPosition("B")], -1, null));
    }

    [Fact]
    public void TheCommanderOffTheRouteIsDrawnOnlyInsideItsExtent()
    {
        var stops = new[] { Stop("A", 0, 0), Stop("B", 100, 100) };

        var inside = RouteMapLayout.For(stops, -1, new StarPosition(50, 0, 50))!;
        var outside = RouteMapLayout.For(stops, -1, new StarPosition(150, 0, 50))!;

        Assert.Equal((0.5, 0.5), inside.Commander);
        Assert.Null(outside.Commander);
        Assert.Equal(inside.Points, outside.Points);
    }

    [Fact]
    public void TheCommanderOnTheRouteIsNotDrawnSeparately()
    {
        var layout = RouteMapLayout.For([Stop("A", 0, 0), Stop("B", 100, 100)], 0, new StarPosition(50, 0, 50))!;

        Assert.Null(layout.Commander);
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
        var stops = RouteMapStop.From(new CarrierRoute([Waypoint("A", false), Waypoint("B", true)]));

        Assert.Equal(RouteStopMarks.None, stops[0].Marks);
        Assert.Equal(RouteStopMarks.Restock, stops[1].Marks);
        Assert.Equal(new StarPosition(1, 2, 3), stops[1].Position);
    }
}
