using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheFarmingRouteIsOrderedByDistanceTests
{
    private static CommanderGameState At(string location)
    {
        var store = new GameStateStore();

        foreach (var json in new[]
        {
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            location,
        })
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    [Fact]
    public void WithNoPositionTheRouteIsTheTablesOrder()
    {
        var route = FarmingRoute.For(null);

        Assert.False(route.PositionKnown);
        Assert.Equal(
            FarmingSites.All.Where(site => !site.IsAlternate).Select(site => site.Body),
            route.Stops.Select(stop => stop.Body));
        Assert.All(route.Stops, stop => Assert.Null(stop.Distance));
    }

    [Fact]
    public void WithAPositionTheNearestSiteComesFirst()
    {
        var state = At(
            """{"timestamp":"3311-01-01T00:00:30Z","event":"Location","StarSystem":"HIP 12099","StarPos":[-100.0,-95.0,-165.0]}""");

        var route = FarmingRoute.For(state);

        Assert.True(route.PositionKnown);
        Assert.Equal("HIP 12099", route.Stops[0].System);
        Assert.True(route.Stops[0].IsHere);
        Assert.Equal(route.Stops.Select(stop => stop.Distance).Order(), route.Stops.Select(stop => stop.Distance));
    }

    [Fact]
    public void TheRawFilterKeepsOnlySitesForRawMaterials()
    {
        var route = FarmingRoute.For(null, "Raw");

        Assert.NotEmpty(route.Stops);
        Assert.All(route.Stops, stop => Assert.Equal("Raw", stop.Kind));
        Assert.True(route.Stops.Count < FarmingRoute.For(null).Stops.Count);
    }

    [Fact]
    public void AGrade4SitesTradeDownsAreListedPerGrade()
    {
        var stop = FarmingRoute.For(null).Stops.Single(s => s.Body == "3 a a");

        Assert.Equal(
            [new FarmingTradeDown(1, 1, 27), new FarmingTradeDown(2, 1, 9), new FarmingTradeDown(3, 1, 3)],
            stop.TradeDowns);
    }
}
