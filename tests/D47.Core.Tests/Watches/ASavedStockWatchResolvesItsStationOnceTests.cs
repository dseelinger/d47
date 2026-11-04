using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ASavedStockWatchResolvesItsStationOnceTests
{
    private static readonly StationSummary Port = new() { Name = "Sample Port", SystemName = "Sample", MarketId = 128000001 };

    private static WatchStore Store() =>
        new(@"C:\d47\data\watches.json", new MemoryFileSystem(), NullLogger<WatchStore>.Instance);

    private static StockDraft Any(string system = "Sample", string station = "Sample Port", string commodity = "Tritium") =>
        new(string.Empty, true, null, system, station, commodity, StockComparison.Below, "5000");

    private static WatchSetup Setup(WatchStore store, IGalaxyService galaxy, ITradePlanService trade) =>
        new(store, galaxy, trade);

    [Fact]
    public async Task PickingSoontillRelicsStoresItsMarketAndSendsNothing()
    {
        var store = Store();
        var rare = RareCatalogue.Find("Soontill Relics");
        Assert.NotNull(rare);

        var draft = new StockDraft(string.Empty, true, rare, string.Empty, string.Empty, string.Empty, StockComparison.Above, "200");
        var result = await Setup(store, new StubGalaxyService(), new StubTradePlanService())
            .SaveStockAsync(null, draft, TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        var watch = Assert.Single(store.Watches);
        Assert.Equal(3225348096, watch.MarketId);
        Assert.Equal("Soontill Relics", watch.Commodity);
        Assert.Equal("Cheranovsky City", watch.Station);
        Assert.Equal("Soontill Relics at Cheranovsky City", watch.Name);
    }

    [Fact]
    public async Task AStationSpanshDoesNotFindIsRefusedOnTheSystemField()
    {
        var store = Store();
        var galaxy = new Galaxy(new StationSearchResult("Sample", 0, []));

        var result = await Setup(store, galaxy, new Trade()).SaveStockAsync(null, Any(), TestContext.Current.CancellationToken);

        Assert.Equal(WatchField.System, result.Field);
        Assert.Equal("Not saved: Spansh finds no stations in Sample.", result.Message);
        Assert.Empty(store.Watches);
    }

    [Fact]
    public async Task ACommodityArdentDoesNotListIsRefusedOnTheCommodityField()
    {
        var store = Store();
        var galaxy = new Galaxy(new StationSearchResult("Sample", 1, [Port]));
        var trade = new Trade { Throws = new UnknownCommodityException("none") };

        var result = await Setup(store, galaxy, trade).SaveStockAsync(null, Any(commodity: "Soontil Relics"), TestContext.Current.CancellationToken);

        Assert.Equal(WatchField.Commodity, result.Field);
        Assert.Equal("Not saved: Ardent lists no commodity called Soontil Relics. Type it as the market spells it.", result.Message);
        Assert.Empty(store.Watches);
    }

    [Fact]
    public async Task AnOutageIsNotReportedAsAnUnknownCommodity()
    {
        var store = Store();
        var galaxy = new Galaxy(new StationSearchResult("Sample", 1, [Port]));
        var trade = new Trade { Throws = new GalaxyUnavailableException("down") };

        var result = await Setup(store, galaxy, trade).SaveStockAsync(null, Any(), TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.DoesNotContain("lists no commodity", result.Message, StringComparison.Ordinal);
        Assert.Empty(store.Watches);
    }

    [Fact]
    public async Task AFoundStationAndListedCommoditySaveWithTheStoredMarket()
    {
        var store = Store();
        var galaxy = new Galaxy(new StationSearchResult("Sample", 1, [Port]));

        var result = await Setup(store, galaxy, new Trade()).SaveStockAsync(null, Any(), TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        var watch = Assert.Single(store.Watches);
        Assert.Equal(128000001, watch.MarketId);
        Assert.Equal("Tritium at Sample Port", watch.Name);
        Assert.Equal(5000, watch.Threshold);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("2.5")]
    [InlineData("many")]
    public async Task UnitsThatAreNotAWholeNumberOfZeroOrMoreAreRefused(string units)
    {
        var result = await Setup(Store(), new Galaxy(new StationSearchResult("Sample", 1, [Port])), new Trade())
            .SaveStockAsync(null, Any() with { Units = units }, TestContext.Current.CancellationToken);

        Assert.Equal(WatchField.Units, result.Field);
    }

    [Fact]
    public void ACommunityGoalWatchDefaultsItsNameAndKeepsItsFiringWhenEdited()
    {
        var store = Store();
        var setup = Setup(store, new StubGalaxyService(), new StubTradePlanService());

        Assert.True(setup.SaveCommunityGoal(null, " ", true).Saved);
        var saved = Assert.Single(store.Watches);
        Assert.Equal("New Community Goals", saved.Name);

        store.MarkFired(saved.Id, new DateTimeOffset(2026, 11, 3, 14, 7, 0, TimeSpan.Zero));
        Assert.True(setup.SaveCommunityGoal(store.Watches[0], "Goals", false).Saved);

        Assert.NotNull(Assert.Single(store.Watches).FiredForTick);
    }

    [Fact]
    public void TheLastFiringIsWordedInLocalTimeOrNotFiredYet()
    {
        var zone = TimeZoneInfo.Utc;

        Assert.Equal("3 Nov · tick 14:07", WatchFired.Text("2026-11-03T14:07:00.0000000Z", zone));
        Assert.Equal("Not fired yet", WatchFired.Text(null, zone));
    }

    [Fact]
    public void ListWatchesNamesEachConditionAndWhenItFired()
    {
        var stock = new Watch(
            Guid.NewGuid(), "Relics", WatchKind.Stock, MarketId: 1, Station: "Cheranovsky City", System: "Ngurii",
            Commodity: "Soontill Relics", Comparison: StockComparison.Above, Threshold: 200,
            FiredForTick: "2026-11-03T14:07:00.0000000Z");
        var goal = new Watch(Guid.NewGuid(), "Goals", WatchKind.NewCommunityGoal, Enabled: false);

        var said = WatchesCapability.Describe([stock, goal], TimeZoneInfo.Utc);

        Assert.Contains("Relics: Soontill Relics above 200 at Cheranovsky City, Ngurii. 3 Nov · tick 14:07.", said, StringComparison.Ordinal);
        Assert.Contains("Goals: a new Community Goal is announced, off. Not fired yet.", said, StringComparison.Ordinal);
        Assert.StartsWith("2 watches.", said, StringComparison.Ordinal);
    }

    private sealed class Galaxy(StationSearchResult result) : StubGalaxyService
    {
        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class Trade : StubTradePlanService
    {
        public Exception? Throws { get; init; }

        public override Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Throws is { } error ? throw error : Task.FromResult<StationQuote?>(null);
    }
}
