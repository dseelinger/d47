using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class SourcingRouteTests
{
    private static SourcingStop Buy(string name, double x) =>
        new(new MarketSnapshot { Station = name, System = name, X = x }, [], 0);

    private static HandIn Hand(string name, double? x) =>
        new(name, name, x is { } at ? (at, 0, 0) : null);

    private static string[] Names(IReadOnlyList<RouteLeg> legs) => [.. legs.Select(leg => leg.Station)];

    [Fact]
    public void OneHandInComesAfterBothBuysAndTheNearerBuyIsFirst()
    {
        var legs = SourcingRoute.Order((0, 0, 0), [Buy("Far", 30), Buy("Near", 10)], [Hand("Site", 35)]);

        Assert.Equal(["Near", "Far", "Site"], Names(legs));
        Assert.Equal([10, 20, 5], legs.Select(leg => leg.Distance!.Value));
        Assert.Equal([false, false, true], legs.Select(leg => leg.IsHandIn));
    }

    [Fact]
    public void TheShorterRouteMayReverseTheOrderHandInsWereGiven()
    {
        var legs = SourcingRoute.Order((0, 0, 0), [Buy("Shop", 10)], [Hand("First", 100), Hand("Second", 20)]);

        Assert.Equal(["Shop", "Second", "First"], Names(legs));
    }

    [Fact]
    public void APlaceWithNoCoordinatesGoesLastWithAnUnknownDistance()
    {
        var legs = SourcingRoute.Order((0, 0, 0), [Buy("Shop", 10)], [Hand("Lost", null), Hand("Site", 20)]);

        Assert.Equal(["Shop", "Site", "Lost"], Names(legs));
        Assert.Null(legs[2].Distance);
        Assert.NotNull(legs[1].Distance);
    }

    [Fact]
    public void NinePlacesAreOrderedNearestFirstWithBuysBeforeHandIns()
    {
        var buys = Enumerable.Range(1, 8).Select(i => Buy($"B{i}", i * 10)).Reverse().ToArray();
        var legs = SourcingRoute.Order((0, 0, 0), buys, [Hand("Site", 0)]);

        Assert.Equal(["B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "Site"], Names(legs));
    }
}
