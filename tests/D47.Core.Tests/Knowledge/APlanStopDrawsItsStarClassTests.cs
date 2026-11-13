using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class APlanStopDrawsItsStarClassTests
{
    private static readonly StarPosition At = new(1, 2, 3);

    private static RouteStopMarks[] Marks(IReadOnlyList<RouteMapStop> stops) => [.. stops.Select(stop => stop.Marks)];

    [Fact]
    public void ARichesStopTakesItsMarksFromItsClass()
    {
        var route = new RichesRoute(
        [
            new RichesStop("A", 1, []) { StarClass = "K" },
            new RichesStop("B", 1, []) { StarClass = "L" },
            new RichesStop("C", 1, []),
        ]);

        Assert.Equal(
            [RouteStopMarks.Scoopable, RouteStopMarks.NotScoopable, RouteStopMarks.None],
            Marks(RouteMapStop.From(route)));
    }

    [Fact]
    public void AnExobiologyStopTakesItsMarksFromItsClass()
    {
        var route = new ExobiologyRoute(
        [
            new ExobiologyStop("A", 1, []) { StarClass = "K" },
            new ExobiologyStop("B", 1, []) { StarClass = "L" },
            new ExobiologyStop("C", 1, []),
        ]);

        Assert.Equal(
            [RouteStopMarks.Scoopable, RouteStopMarks.NotScoopable, RouteStopMarks.None],
            Marks(RouteMapStop.From(route)));
    }

    [Fact]
    public void ANeutronPlotKeepsNeutronAndMarksTheRestByClass()
    {
        var route = new PlottedRoute(
            "Sol",
            "Colonia",
            100,
            4,
            [
                new RouteWaypoint("K", 1, 50, false) { StarClass = "K" },
                new RouteWaypoint("L", 1, 40, false) { StarClass = "L" },
                new RouteWaypoint("Unknown", 1, 30, false),
                new RouteWaypoint("PSR", 1, 20, true),
            ]);

        Assert.Equal(
            [RouteStopMarks.Scoopable, RouteStopMarks.NotScoopable, RouteStopMarks.None, RouteStopMarks.Neutron],
            Marks(RouteMapStop.From(route)));
    }

    [Fact]
    public void ThePlansEndsAreMarkedByTheirClass()
    {
        var route = new RichesRoute([new RichesStop("B", 1, [])])
        {
            Start = new RouteEndpoint("A", At) { StarClass = "K" },
            End = new RouteEndpoint("C", At) { StarClass = "L" },
        };

        Assert.Equal(
            [RouteStopMarks.Scoopable, RouteStopMarks.None, RouteStopMarks.NotScoopable],
            Marks(RouteMapStop.From(route)));
    }
}
