using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>One tool for "how do I get X", answering the method first and searching at most once (#179).</summary>
public class HowToGetAsksWhatThenSearchesOnceTests
{
    /// <summary>A service that records what it was asked and answers from a script.</summary>
    private sealed class FakeGalaxy : IGalaxyService
    {
        public StationQuery? LastStationQuery { get; private set; }

        public BodyQuery? LastBodyQuery { get; private set; }

        public StationSearchResult Stations { get; set; } = new("Sol", 0, []);

        public BodySearchResult Bodies { get; set; } = new("Sol", 0, []);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Sol", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            LastStationQuery = query;
            return Task.FromResult(Stations);
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            LastBodyQuery = query;
            return Task.FromResult(Bodies);
        }

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Sol", 0, []));

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>Records the commodity search it was handed, and answers whatever it was given.</summary>
    private sealed class FakeTrade(CommodityAnswer answer) : ITradePlanService
    {
        public CommoditySearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken)
        {
            Last = search;
            return Task.FromResult(answer);
        }

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);

        public Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken) =>
            Task.FromResult<StationQuote?>(null);
    }

    private static (CapabilityRegistry Registry, FakeGalaxy Galaxy) Build(
        TempInstall install, ITradePlanService? trade = null, bool enabled = true, string? currentSystem = "Sol")
    {
        var galaxy = new FakeGalaxy();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(GalaxyCapability.EnabledKey, enabled ? "true" : "false", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => currentSystem, settings, trade)]);

        return (registry, galaxy);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task AnAnacondaNamesAShipyardAndSearchesForOne()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Anaconda")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Anaconda", galaxy.LastStationQuery?.Ship);
        Assert.Null(galaxy.LastStationQuery?.Module);
    }

    [Fact]
    public async Task AGuardianFsdBoosterNamesATechBrokerAndSearchesNothing()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Guardian FSD Booster")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Guardian tech broker", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastStationQuery);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task LowTemperatureDiamondsNameRingMiningAndSearchTheBodiesForTheHotspot()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Low Temperature Diamonds")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Low Temperature Diamonds", galaxy.LastBodyQuery?.RingSignal);
        Assert.Null(galaxy.LastStationQuery);
    }

    [Fact]
    public async Task EdenApplesOfAerialNameAndradeLegacyAndSearchNothing()
    {
        using var install = new TempInstall();
        var (registry, galaxy) = Build(install);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Eden Apples of Aerial")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Andrade Legacy", result.Content, StringComparison.Ordinal);
        Assert.Null(galaxy.LastStationQuery);
        Assert.Null(galaxy.LastBodyQuery);
    }

    [Fact]
    public async Task AMarketCommodityWithNoOtherMethodSaysSoWhenNothingSellsIt()
    {
        using var install = new TempInstall();
        var trade = new FakeTrade(new CommodityAnswer([], 0, 0, true));
        var (registry, _) = Build(install, trade);

        // Aluminium is a plain market commodity: the table names no method for it at all.
        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Aluminium")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.NotNull(trade.Last);
        Assert.Contains("no other sourcing", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoGalaxyServiceTheMethodStillAnswers()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(null, () => "Sol", settings)]);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Anaconda")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Bought at a shipyard", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownNameOffersTheNearestNames()
    {
        using var install = new TempInstall();
        var (registry, _) = Build(install);

        var result = await registry.InvokeAsync(
            "how_to_get", Args(("item", "Anacondo")), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Anaconda", result.Content, StringComparison.Ordinal);
    }
}
