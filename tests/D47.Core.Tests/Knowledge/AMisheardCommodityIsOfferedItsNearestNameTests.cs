using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A misheard commodity gets the same nearest-name offer ships, engineers and modules already get,
/// rather than the index's flat "no such commodity" (#117).
/// </summary>
public class AMisheardCommodityIsOfferedItsNearestNameTests
{
    private sealed class Recording : ITradePlanService
    {
        public CommoditySearch? Swept { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken)
        {
            Swept = search;

            return Task.FromResult(new CommodityAnswer([], 30, 0, true));
        }

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);
    }

    private sealed class SilentGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Ega", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new StationSearchResult("Ega", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Ega", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new ColonisationScan("Ega", 0, []));
    }

    private static async Task<string> AskAsync(IGalaxyService? galaxy, ITradePlanService? trade, string commodity)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy, () => "Ega", surface.Settings, trade, currentStation: null, board: null,
                now: null),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["commodity"] = commodity };

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        return result.Content ?? string.Empty;
    }

    /// <summary>The near-miss offer needs nothing composed at all — it never reaches the index.</summary>
    [Fact]
    public async Task ANearMissOnARareCargoNameOffersTheRealOne()
    {
        var said = await AskAsync(galaxy: null, trade: null, "Leavian Brandy");

        Assert.Contains("Lavian Brandy", said, StringComparison.Ordinal);
        Assert.Contains("Did you mean", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A material near-miss is not offered from the commodity path — it is not cargo, so it is left for
    /// the index to answer as it always has.
    /// </summary>
    [Fact]
    public async Task ANearMissOnAMaterialNameIsLeftForTheIndex()
    {
        var trade = new Recording();

        var said = await AskAsync(new SilentGalaxy(), trade, "Vanadum");

        Assert.DoesNotContain("Did you mean", said, StringComparison.Ordinal);
        Assert.NotNull(trade.Swept);
        Assert.Equal("Vanadum", trade.Swept!.Query.Commodity);
    }

    /// <summary>Nothing near at all still reaches the index exactly as before this fix.</summary>
    [Fact]
    public async Task ANameNothingIsNearStillReachesTheIndex()
    {
        var trade = new Recording();

        var said = await AskAsync(new SilentGalaxy(), trade, "Zzyzxqplorp");

        Assert.DoesNotContain("Did you mean", said, StringComparison.Ordinal);
        Assert.NotNull(trade.Swept);
        Assert.Equal("Zzyzxqplorp", trade.Swept!.Query.Commodity);
    }
}
