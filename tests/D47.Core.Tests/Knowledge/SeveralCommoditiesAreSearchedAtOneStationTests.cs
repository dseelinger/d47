using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>One search for several commodities answers with the stations that trade every one of them.</summary>
public class SeveralCommoditiesAreSearchedAtOneStationTests
{
    /// <summary>Records the search it was handed, and answers whatever it was given.</summary>
    private sealed class Capturing(CommodityAnswer answer) : ITradePlanService
    {
        public CommoditySearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken)
        {
            Last = search;
            return Task.FromResult(answer);
        }

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);

        public Task<BestCargoAnswer?> BestCargoAsync(
            BestCargoSearch search,
            CancellationToken cancellationToken) => Task.FromResult<BestCargoAnswer?>(null);
    }

    /// <summary>Nothing this file is about — the commodity fork happens above every one of these.</summary>
    private sealed class SilentGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Sol", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new StationSearchResult("Sol", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Sol", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Sol", 0, []));

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static MarketSnapshot Market(string station, double x, params (string Commodity, int Buy, int Supply)[] quotes) =>
        new()
        {
            Station = station,
            System = station + " System",
            X = x,
            HasLargePad = true,
            Type = "Coriolis Starport",
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Quotes = quotes.ToDictionary(
                quote => quote.Commodity,
                quote => new MarketQuote(quote.Commodity) { BuyPrice = quote.Buy, Supply = quote.Supply },
                StringComparer.OrdinalIgnoreCase),
        };

    private static MarketSnapshot Origin() => new() { Station = "Home", System = "Sol" };

    private static CommodityQuery Basket(int? goldTonnes = null, int? painiteTonnes = null, bool? descending = null) =>
        new("Gold", Tonnes: goldTonnes, Descending: descending, More: [new("Painite", painiteTonnes)]);

    private static async Task<(string Said, bool IsError, CommoditySearch? Search)> AskAsync(
        CommodityAnswer answer,
        params (string Key, string Value)[] arguments)
    {
        var surface = TestSurface.For(new MemoryInstall());

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Capturing(answer);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(new SilentGalaxy(), () => "Sol", surface.Settings, trade),
        ]);

        var values = arguments.ToDictionary(argument => argument.Key, argument => argument.Value, StringComparer.Ordinal);

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        return (result.Content, result.IsError, trade.Last);
    }

    [Fact]
    public void NamesWithDigitsKeepThemAndTheTonnageFollowsTheColon()
    {
        var wants = CommodityWant.Parse("Hafnium 178: 40, SAP 8 Core Container", tonnes: null, out var refusal);

        Assert.Null(refusal);
        Assert.Equal([new CommodityWant("Hafnium 178", 40), new CommodityWant("SAP 8 Core Container", null)], wants);
    }

    [Fact]
    public void AnItemWithoutATonnageTakesTheSearchsOwn()
    {
        var wants = CommodityWant.Parse("Painite: 40, Gold", tonnes: 12, out _);

        Assert.Equal([new CommodityWant("Painite", 40), new CommodityWant("Gold", 12)], wants);
    }

    [Fact]
    public void ADuplicateNameIsMergedAndItsTonnagesAdded()
    {
        var wants = CommodityWant.Parse("Gold: 10, Painite, gold: 5", tonnes: null, out _);

        Assert.Equal([new CommodityWant("Gold", 15), new CommodityWant("Painite", null)], wants);
    }

    [Fact]
    public void ASixthCommodityIsRefused()
    {
        var wants = CommodityWant.Parse("Gold, Silver, Painite, Platinum, Palladium, Osmium", tonnes: null, out var refusal);

        Assert.Null(wants);
        Assert.Equal("Five commodities is the most I search for at one station, and that was 6.", refusal);
    }

    [Fact]
    public void AStationMissingOneOfTheCommoditiesIsLeftOut()
    {
        var offers = CommodityMarketSearch.Rank(
            Basket(),
            [
                Market("Both", 1, ("Gold", 9_000, 100), ("Painite", 50_000, 100)),
                Market("Gold only", 1, ("Gold", 8_000, 100)),
            ],
            Origin());

        Assert.Equal("Both", Assert.Single(offers).Market.Station);
    }

    [Fact]
    public void ATonnageIsAFloorOnItsOwnCommodityAlone()
    {
        var offers = CommodityMarketSearch.Rank(
            Basket(painiteTonnes: 40),
            [
                Market("Short of Painite", 1, ("Gold", 9_000, 100), ("Painite", 50_000, 30)),
                Market("Short of Gold", 1, ("Gold", 9_000, 5), ("Painite", 50_000, 40)),
            ],
            Origin());

        Assert.Equal("Short of Gold", Assert.Single(offers).Market.Station);
    }

    [Fact]
    public void TheTotalIsTheSumOfTheLots()
    {
        var offer = Assert.Single(CommodityMarketSearch.Rank(
            Basket(goldTonnes: 10, painiteTonnes: 2),
            [Market("Both", 0, ("Gold", 9_000, 100), ("Painite", 50_000, 100))],
            Origin()));

        Assert.Equal(
            [new CommodityLot("Gold", 9_000, 10), new CommodityLot("Painite", 50_000, 2)],
            offer.Lots);
        Assert.Equal((10 * 9_000) + (2 * 50_000), offer.Total);
    }

    [Fact]
    public void WithoutATonnageEachCommodityCountsOneTonneInThePriceOrder()
    {
        var offers = CommodityMarketSearch.Rank(
            Basket(),
            [
                Market("Cheap gold, dear painite", 0, ("Gold", 1_000, 100), ("Painite", 60_000, 100)),
                Market("Dear gold, cheap painite", 0, ("Gold", 9_000, 100), ("Painite", 50_000, 100)),
            ],
            Origin());

        Assert.Equal("Dear gold, cheap painite", offers[0].Market.Station);
    }

    [Fact]
    public void AReversedSearchReversesTheWholeAnswer()
    {
        MarketSnapshot[] markets =
        [
            Market("A", 0, ("Gold", 1_000, 100), ("Painite", 1_000, 100)),
            Market("B", 0, ("Gold", 2_000, 100), ("Painite", 2_000, 100)),
            Market("C", 0, ("Gold", 3_000, 100), ("Painite", 3_000, 100)),
        ];

        var usual = CommodityMarketSearch.Rank(Basket(), markets, Origin()).Select(offer => offer.Market.Station);
        var reversed = CommodityMarketSearch.Rank(Basket(descending: true), markets, Origin())
            .Select(offer => offer.Market.Station);

        Assert.Equal(["A", "B", "C"], usual);
        Assert.Equal(["C", "B", "A"], reversed);
    }

    [Fact]
    public void EachCommodityCountsTheStationsThatHaveItAlone()
    {
        MarketSnapshot[] markets =
        [
            Market("Gold one", 1, ("Gold", 9_000, 100)),
            Market("Gold two", 1, ("Gold", 9_000, 100)),
            Market("Painite", 1, ("Painite", 50_000, 100)),
            Market("Too far", 500, ("Gold", 9_000, 100)),
        ];

        var query = Basket();

        Assert.Equal(2, CommodityMarketSearch.Stocking(query, query.Wants[0], markets, Origin()));
        Assert.Equal(1, CommodityMarketSearch.Stocking(query, query.Wants[1], markets, Origin()));
    }

    [Fact]
    public async Task TheListReachesTheSearchAsEveryCommodity()
    {
        var (_, _, search) = await AskAsync(
            new CommodityAnswer([], 0, 0, true),
            ("commodity", "Painite: 40, Platinum: 12, Gold"));

        Assert.NotNull(search);
        Assert.Equal(
            [new CommodityWant("Painite", 40), new CommodityWant("Platinum", 12), new CommodityWant("Gold", null)],
            search.Query.Wants);
    }

    [Fact]
    public async Task AnEmptyAnswerSaysHowManyStationsHadEachCommodity()
    {
        var (said, _, _) = await AskAsync(
            new CommodityAnswer([], 15, 0, true) { Stocked = [("Gold", 12), ("Painite", 3)] },
            ("commodity", "Gold, Painite"));

        Assert.StartsWith(
            "12 stations sell Gold and 3 sell Painite; none sells both within 50 light years of Sol.",
            said,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachStationIsReadOutWithEveryCommoditysPrice()
    {
        var market = Market("Both", 3, ("Gold", 9_000, 100), ("Painite", 50_000, 100));

        var offer = new CommodityOffer(market, 9_000, 100, 3)
        {
            Lots = [new("Gold", 9_000, 1), new("Painite", 50_000, 1)],
        };

        var (said, _, _) = await AskAsync(
            new CommodityAnswer([offer], 1, 0, true),
            ("commodity", "Gold, Painite"));

        Assert.Contains("Best for buying Gold and Painite within 50 ly of Sol: Both (Both System), 3 ly", said, StringComparison.Ordinal);
        Assert.Contains($"Gold {9_000:N0} cr, Painite {50_000:N0} cr a tonne", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiveCommoditiesAreAllPricedAtEachStation()
    {
        string[] names = ["Gold", "Silver", "Painite", "Platinum", "Palladium"];

        var market = Market("All five", 3, [.. names.Select((name, at) => (name, 1_000 * (at + 1), 100))]);

        var offer = new CommodityOffer(market, 1_000, 100, 3)
        {
            Lots = [.. names.Select((name, at) => new CommodityLot(name, 1_000 * (at + 1), 1))],
        };

        var (said, _, _) = await AskAsync(
            new CommodityAnswer([offer], 1, 0, true),
            ("commodity", string.Join(", ", names)));

        Assert.Contains(
            $"Gold {1_000:N0} cr, Silver {2_000:N0} cr, Painite {3_000:N0} cr, Platinum {4_000:N0} cr, Palladium {5_000:N0} cr a tonne",
            said,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARareGoodInAListIsRefusedWhenBuying()
    {
        var rare = RareCatalogue.All.First();

        var (said, _, search) = await AskAsync(
            new CommodityAnswer([], 0, 0, true),
            ("commodity", $"Gold, {rare.Name}"));

        Assert.Null(search);
        Assert.Contains(rare.Station, said, StringComparison.Ordinal);
        Assert.Contains("Ask for it on its own.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARareGoodInAListIsSearchedWhenSelling()
    {
        var rare = RareCatalogue.All.First();

        var (_, _, search) = await AskAsync(
            new CommodityAnswer([], 0, 0, true),
            ("commodity", $"Gold, {rare.Name}"),
            ("selling", "true"));

        Assert.NotNull(search);
        Assert.Equal(["Gold", rare.Name], search.Query.Wants.Select(want => want.Commodity));
    }

    /// <summary>"Gold" is a unique fragment of Sothis Crystalline Gold, and is still the ordinary commodity.</summary>
    [Fact]
    public async Task GoldOnItsOwnIsSearchedRatherThanAnsweredAsARareGood()
    {
        var (_, _, search) = await AskAsync(new CommodityAnswer([], 0, 0, true), ("commodity", "Gold"));

        Assert.NotNull(search);
        Assert.Equal("Gold", search.Query.Commodity);
    }

    [Fact]
    public async Task ASixthCommodityIsRefusedByTheTool()
    {
        var (said, isError, search) = await AskAsync(
            new CommodityAnswer([], 0, 0, true),
            ("commodity", "Gold, Silver, Painite, Platinum, Palladium, Osmium"));

        Assert.True(isError);
        Assert.Null(search);
        Assert.Contains("Five commodities is the most", said, StringComparison.Ordinal);
    }
}
