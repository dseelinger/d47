using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A rare good is sold at one station, and how much of it is on offer per visit is set by that
/// station rather than by the ship's hold (#118). The table names the station and the one market's
/// quote gives the quantity, so nothing is left for the model to reason out of the ship report.
/// </summary>
public class ARaresCeilingIsTheStationsOfferTests
{
    private static readonly DateTimeOffset Reported = new(2026, 9, 11, 0, 23, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now = Reported.AddHours(3);

    private sealed class Quoting(StationQuote? quote) : StubTradePlanService
    {
        public long? MarketId { get; private set; }

        public CommoditySearch? Swept { get; private set; }

        public override Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public override Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken)
        {
            Swept = search;

            return Task.FromResult(new CommodityAnswer([], 30, 0, true));
        }

        public override Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public override Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken)
        {
            MarketId = marketId;

            return Task.FromResult(quote);
        }

        public override Task<BestCargoAnswer?> BestCargoAsync(
            BestCargoSearch search,
            CancellationToken cancellationToken) => Task.FromResult<BestCargoAnswer?>(null);
    }

    /// <summary>The index refusing the call, which leaves the station and system still answerable.</summary>
    private sealed class Unreachable : StubTradePlanService
    {
        public override Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public override Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CommodityAnswer([], 30, 0, true));

        public override Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public override Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) =>
            throw new GalaxyUnavailableException("The market search took too long to answer.");

        public override Task<BestCargoAnswer?> BestCargoAsync(
            BestCargoSearch search,
            CancellationToken cancellationToken) =>
            throw new GalaxyUnavailableException("The market search took too long to answer.");
    }

    private sealed class SilentGalaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Ega", 0, []));

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public override Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new StationSearchResult("Ega", 0, []));

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Ega", 0, []));

        public override Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new ColonisationScan("Ega", 0, []));
    }

    private static async Task<string> AskAsync(
        IGalaxyService? galaxy,
        ITradePlanService? trade,
        params (string Key, string Value)[] arguments)
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);


        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy, () => "Ega", surface.Settings, trade, currentStation: null, board: null,
                now: () => Now),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = "Lavian Brandy",
        };

        foreach (var (key, value) in arguments)
        {
            values[key] = value;
        }

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        return result.Content;
    }

    /// <summary>
    /// The one station comes from the table, so it is answered with nothing composed at all — and the
    /// hold is no part of the answer, which is where "that's your ceiling per trip regardless of what's
    /// in stock" came from.
    /// </summary>
    [Fact]
    public async Task TheOneStationIsAnsweredWithNothingComposedAndTheHoldIsNoPartOfIt()
    {
        var said = await AskAsync(galaxy: null, trade: null);

        Assert.Contains("Lave Station", said, StringComparison.Ordinal);
        Assert.Contains("Lave", said, StringComparison.Ordinal);
        Assert.DoesNotContain("tonnes", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hold", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheQuantityOnOfferIsTheOneMarketsLastReportAndItsAge()
    {
        var trade = new Quoting(new StationQuote(24, 3, 3_500, 1, 3_500, Reported));

        var said = await AskAsync(galaxy: null, trade);

        Assert.Equal(128106744, trade.MarketId);
        Assert.Contains("Last reported stock 24, 3 hours ago", said, StringComparison.Ordinal);
        Assert.Contains("ceiling on what can be bought in one visit", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMarketWithNoReportSaysThereIsNoneRatherThanNamingAQuantity()
    {
        var said = await AskAsync(galaxy: null, new Quoting(null));

        Assert.Contains("no recent report of it", said, StringComparison.Ordinal);
        Assert.Contains("Lave Station", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Last reported", said, StringComparison.Ordinal);
    }

    /// <summary>The index being unreachable takes the quantity away, not the station.</summary>
    [Fact]
    public async Task AnIndexThatCannotBeReachedStillLeavesTheStationAnswered()
    {
        var said = await AskAsync(galaxy: null, new Unreachable());

        Assert.Contains("Lave Station", said, StringComparison.Ordinal);
        Assert.Contains("no recent report of it", said, StringComparison.Ordinal);
    }

    /// <summary>Where a rare can be sold is a different question, and the radius sweep answers it.</summary>
    [Fact]
    public async Task SellingOneStillSweepsTheRadiusAfterTheStationIsNamed()
    {
        var trade = new Quoting(new StationQuote(24, 3, 3_500, 1, 3_500, Reported));

        var said = await AskAsync(new SilentGalaxy(), trade, ("selling", "true"));

        Assert.Contains("Lave Station", said, StringComparison.Ordinal);
        Assert.NotNull(trade.Swept);
        Assert.Equal(TradeSide.Selling, trade.Swept.Query.Side);
    }
}
