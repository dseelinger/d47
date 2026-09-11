using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <c>find_nearest_station</c>'s <c>limit</c> is bounded out loud rather than reset in silence.
/// </summary>
public class TheStationSearchsLimitIsSaidOutLoudTests
{
    /// <summary>Records the search it was handed, and answers nothing.</summary>
    private sealed class Capturing : IGalaxyService
    {
        public StationQuery? Last { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Eurybia", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken)
        {
            Last = query;
            return Task.FromResult(new StationSearchResult("Eurybia", 0, []));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Eurybia", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Eurybia", 0, []));
    }

    /// <summary>Records the commodity search it was handed, and answers nothing.</summary>
    private sealed class CapturingTrade : ITradePlanService
    {
        public CommoditySearch? Last { get; private set; }

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken)
        {
            Last = search;
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

    /// <summary>The module-and-ship half — a module name, so the commodity fork is not taken.</summary>
    private static async Task<(string Said, StationQuery Query)> ForAModuleAsync(
        TempInstall install,
        params (string Key, string Value)[] arguments)
    {
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var galaxy = new Capturing();

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(galaxy, () => "Eurybia", surface.Settings, new CapturingTrade()),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["module"] = "Frame Shift Drive",
        };

        foreach (var (key, value) in arguments)
        {
            values[key] = value;
        }

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        Assert.NotNull(galaxy.Last);

        return (result.Content, galaxy.Last);
    }

    /// <summary>The commodity half — a commodity name, which forks above everything above.</summary>
    private static async Task<(string Said, CommoditySearch Search)> ForACommodityAsync(
        TempInstall install,
        params (string Key, string Value)[] arguments)
    {
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new CapturingTrade();

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(new Capturing(), () => "Eurybia", surface.Settings, trade),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = "Landmines",
        };

        foreach (var (key, value) in arguments)
        {
            values[key] = value;
        }

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        Assert.NotNull(trade.Last);

        return (result.Content, trade.Last);
    }

    /// <summary>The defect, on the commodity half.</summary>
    [Fact]
    public async Task FiftyIsRefusedAtTwentyRatherThanResetToFive()
    {
        using var install = new TempInstall();

        var (said, search) = await ForACommodityAsync(install, ("limit", "50"));

        Assert.Equal(20, search.Query.Limit);
        Assert.Contains("You asked for 50", said, StringComparison.Ordinal);
        Assert.Contains("limit stops at 20", said, StringComparison.Ordinal);
        Assert.DoesNotContain("up to 20 results", said, StringComparison.Ordinal);
    }

    /// <summary>And on the module half, which is the same argument through a different fork.</summary>
    [Fact]
    public async Task TheModuleHalfRefusesTheSameWay()
    {
        using var install = new TempInstall();

        var (said, query) = await ForAModuleAsync(install, ("limit", "50"));

        Assert.Equal(20, query.Size);
        Assert.Contains("You asked for 50", said, StringComparison.Ordinal);
        Assert.Contains("limit stops at 20", said, StringComparison.Ordinal);
    }

    /// <summary>The other end of the bound.</summary>
    [Fact]
    public async Task NoughtIsRefusedAtOne()
    {
        using var install = new TempInstall();

        var (said, search) = await ForACommodityAsync(install, ("limit", "0"));

        Assert.Equal(1, search.Query.Limit);
        Assert.Contains("You asked for 0", said, StringComparison.Ordinal);
        Assert.Contains("limit starts at 1", said, StringComparison.Ordinal);
    }

    /// <summary>An honoured ask is echoed, the rule #157 wrote for the radius.</summary>
    [Fact]
    public async Task AnHonouredCountIsSaidBack()
    {
        using var install = new TempInstall();

        var (said, search) = await ForACommodityAsync(install, ("limit", "12"));

        Assert.Equal(12, search.Query.Limit);
        Assert.Contains("up to 12 results", said, StringComparison.Ordinal);
        Assert.DoesNotContain("You asked for", said, StringComparison.Ordinal);
    }

    /// <summary>And the plain question keeps the plain answer.</summary>
    [Fact]
    public async Task AnUnqualifiedSearchSaysNothingAboutTheLimit()
    {
        using var install = new TempInstall();

        var (commodity, search) = await ForACommodityAsync(install);

        Assert.Equal(5, search.Query.Limit);
        Assert.Equal("Nothing within 50 light years of Eurybia is buying Landmines.", commodity);

        var (module, query) = await ForAModuleAsync(install);

        Assert.Equal(5, query.Size);
        Assert.DoesNotContain("limit", module, StringComparison.OrdinalIgnoreCase);
    }
}
