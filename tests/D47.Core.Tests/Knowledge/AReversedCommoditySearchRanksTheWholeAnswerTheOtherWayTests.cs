using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><c>order</c> on <c>find_nearest_station</c> turns the ranking round over the whole answer, not the rows shown (#1113).</summary>
public class AReversedCommoditySearchRanksTheWholeAnswerTheOtherWayTests
{
    private static MarketSnapshot Station(string name, double x, int price) => new()
    {
        Station = name,
        System = name + " system",
        X = x,
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palladium"] = new("Palladium") { BuyPrice = price, SellPrice = price, Supply = 5_000, Demand = 5_000 },
        },
    };

    private static readonly MarketSnapshot[] Markets = [Station("Near", 10, 52_000), Station("Middle", 40, 50_000), Station("Far", 90, 51_000)];

    private static readonly MarketSnapshot Origin = new() { Station = "Ega", System = "Ega" };

    private static IEnumerable<string> Names(CommodityQuery query) =>
        CommodityMarketSearch.Rank(query, Markets, Origin).Select(offer => offer.Market.Station);

    [Fact]
    public void DistanceDescendingIsFarthestFirstAcrossEveryStationFound()
    {
        Assert.Equal(["Near", "Middle", "Far"], Names(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance, Limit: 3)));
        Assert.Equal(["Far", "Middle", "Near"], Names(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance, Limit: 3, Descending: true)));
        Assert.Equal(["Far"], Names(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance, Limit: 1, Descending: true)));
    }

    [Fact]
    public void PriceIsBestFirstForABuyerCheapestAndForASellerHighestUntilReversed()
    {
        Assert.Equal(["Middle", "Far", "Near"], Names(new CommodityQuery("Palladium", MaxDistance: 500, Limit: 3)));
        Assert.Equal(["Near", "Far", "Middle"], Names(new CommodityQuery("Palladium", MaxDistance: 500, Limit: 3, Descending: true)));
        Assert.Equal(["Middle", "Far", "Near"], Names(new CommodityQuery("Palladium", MaxDistance: 500, Limit: 3, Descending: false)));

        Assert.Equal(["Near", "Far", "Middle"], Names(new CommodityQuery("Palladium", TradeSide.Selling, MaxDistance: 500, Limit: 3)));
        Assert.Equal(["Middle", "Far", "Near"], Names(new CommodityQuery("Palladium", TradeSide.Selling, MaxDistance: 500, Limit: 3, Descending: false)));
    }

    [Fact]
    public void OnlyAnAscendingDistanceRankingIsNearestFirst()
    {
        Assert.True(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance).NearestFirst);
        Assert.True(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance, Descending: false).NearestFirst);
        Assert.False(new CommodityQuery("Palladium", MaxDistance: 500, OrderBy: CommodityOrder.Distance, Descending: true).NearestFirst);
        Assert.False(new CommodityQuery("Palladium").NearestFirst);
    }

    private sealed class NoGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Capturing : ITradePlanService
    {
        public CommoditySearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) => Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Last = search;
            return Task.FromResult(new CommodityAnswer([], 0, 0, true));
        }

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(BestCargoSearch search, CancellationToken cancellationToken) => Task.FromResult<BestCargoAnswer?>(null);
    }

    private static async Task<(CommodityQuery? Query, ToolResult Result)> AskAsync(params (string Key, string Value)[] arguments)
    {
        var trade = new Capturing();
        var surface = TestSurface.For(new MemoryInstall());
        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new NoGalaxy(), () => "Ega", surface.Settings, trade)]);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            new ToolArguments(arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        return (trade.Last?.Query, result);
    }

    [Fact]
    public async Task TheOrderArgumentReachesTheQuery()
    {
        var (query, _) = await AskAsync(("commodity", "Palladium"), ("order_by", "distance"), ("order", "desc"));

        Assert.Equal(CommodityOrder.Distance, query!.OrderBy);
        Assert.True(query.Descending);

        (query, _) = await AskAsync(("commodity", "Palladium"));

        Assert.Null(query!.Descending);
    }

    [Fact]
    public async Task AnOrderThatIsNeitherAscNorDescIsRefused()
    {
        var (query, result) = await AskAsync(("commodity", "Palladium"), ("order", "sideways"));

        Assert.Null(query);
        Assert.True(result.IsError);
    }
}
