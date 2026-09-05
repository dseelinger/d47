using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The four parameters #296 added to <c>find_nearest_station</c> reach the query, and a
/// nearest-first answer is one sentence with the rest counted.
/// </summary>
public class TheFourInaraKnobsReachTheSearchTests
{
    private sealed class Capturing(CommodityAnswer answer) : ITradePlanService
    {
        public CommoditySearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Last = search;
            return Task.FromResult(answer);
        }

        public Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(SourcingAnswer.Empty);
    }

    private sealed class SilentGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Ega", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult("Ega", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Ega", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Ega", 0, []));
    }

    private static MarketSnapshot Station(string name, double x, int supply, double arrival) => new()
    {
        Station = name,
        System = name + " system",
        X = x,
        Type = "Orbis Starport",
        HasLargePad = true,
        DistanceToArrival = arrival,
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Palladium"] = new("Palladium") { BuyPrice = 51_000, Supply = supply },
        },
    };

    private static async Task<(string Said, CommoditySearch Search)> AskAsync(
        CommodityAnswer answer,
        params (string Key, string Value)[] arguments)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Capturing(answer);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(new SilentGalaxy(), () => "Ega", surface.Settings, trade),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["commodity"] = "Palladium" };

        foreach (var (key, value) in arguments)
        {
            values[key] = value;
        }

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        Assert.NotNull(trade.Last);

        return (result.Content, trade.Last);
    }

    [Fact]
    public async Task EachKnobReachesTheQueryAndIsSaidBack()
    {
        var (said, search) = await AskAsync(
            new CommodityAnswer([], 30, 0, true),
            ("max_station_distance", "50000"),
            ("min_supply", "10000"),
            ("surface_stations", "true"),
            ("order_by", "distance"));

        Assert.Equal(50_000, search.Query.MaxStationDistance);
        Assert.Equal(10_000, search.Query.MinAvailable);
        Assert.True(search.Query.SurfaceStations);
        Assert.Equal(CommodityOrder.Distance, search.Query.OrderBy);

        Assert.Contains("pads within 50,000 Ls", said, StringComparison.Ordinal);
        Assert.Contains("at least 10,000 in stock", said, StringComparison.Ordinal);
        Assert.Contains("surface stations included", said, StringComparison.Ordinal);
        Assert.Contains("nearest first", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeftOutTheyMeanWhatTheyAlwaysMeant()
    {
        var (said, search) = await AskAsync(new CommodityAnswer([], 30, 0, true));

        Assert.Null(search.Query.MaxStationDistance);
        Assert.Null(search.Query.MinAvailable);
        Assert.False(search.Query.SurfaceStations);
        Assert.Equal(CommodityOrder.Price, search.Query.OrderBy);
        Assert.DoesNotContain("Searched", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NearestFirstSpeaksOneStationAndCountsTheRest()
    {
        var query = new CommunityGoalSearch().Arguments();
        var typed = new CommodityQuery("Palladium", MaxDistance: 250, OrderBy: CommodityOrder.Distance, Limit: 10);

        var origin = new MarketSnapshot { Station = "Ega", System = "Ega" };
        var offers = CommodityMarketSearch.Rank(
            typed,
            [Station("Near", 12, 12_000, 210), Station("Middle", 40, 30_000, 900), Station("Far", 90, 50_000, 40)],
            origin);

        var (said, _) = await AskAsync(
            new CommodityAnswer(offers, 3, 0, true),
            [.. query.Values.Where(pair => pair.Key != "commodity").Select(pair => (pair.Key, pair.Value))]);

        Assert.StartsWith("Nearest for buying Palladium", said, StringComparison.Ordinal);
        Assert.Contains("Near (Near system), 12 ly, 51,000 cr a tonne, 12,000 in stock, 210 Ls from the star", said, StringComparison.Ordinal);
        Assert.Contains("2 more, further out, are on the Routing tab", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Middle", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Far (", said, StringComparison.Ordinal);
    }
}
