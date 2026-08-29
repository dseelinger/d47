using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// What d47 is allowed to claim when it found nothing (#156).
/// <para>
/// The reported sentence was <i>"No stock of Land Mines for 200 tonnes within 150 ly of
/// Eurybia"</i>, said about a search that had examined the nearest 150 markets and got nothing
/// like 150 light years out of them. The repository's own honesty rule was already written one
/// function away — <i>"stations dropped for being too old are counted rather than swallowed"</i> —
/// and the horizon had never been given the same treatment.
/// </para>
/// <para>
/// <i>"Nothing in the markets I could check, and they reach fourteen light years"</i> and
/// <i>"nothing within 250 light years"</i> are different claims, and only one of them tells a
/// Commander to widen the search rather than to give up on the commodity.
/// </para>
/// </summary>
public class ANegativeAnswerSaysHowFarItLookedTests
{
    private sealed class Answering(CommodityAnswer answer) : ITradePlanService
    {
        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search,
            CancellationToken cancellationToken) => Task.FromResult(answer);

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search,
            CancellationToken cancellationToken) => Task.FromResult(SourcingAnswer.Empty);
    }

    /// <summary>Nothing this file is about — the commodity fork happens above every one of these.</summary>
    private sealed class SilentGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult("Eurybia", 0, []));

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(null);

        public Task<StationSearchResult> FindStationsAsync(
            StationQuery query,
            CancellationToken cancellationToken) => Task.FromResult(new StationSearchResult("Eurybia", 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult("Eurybia", 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(
            ColonisationQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ColonisationScan("Eurybia", 0, []));
    }

    private static async Task<string> AskingFor(CommodityAnswer answer, TempInstall install)
    {
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                new SilentGalaxy(),
                () => "Eurybia",
                surface.Settings,
                new Answering(answer)),
        ]);

        var result = await registry.InvokeAsync(
            "find_nearest_station",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["commodity"] = "Landmines",
                ["tonnes"] = "200",
                ["max_distance"] = "250",
            }),
            TestContext.Current.CancellationToken);

        return result.Content;
    }

    /// <summary>
    /// A search that ran out of budget before it ran out of galaxy says how far it got, and does
    /// not claim the radius it was asked about.
    /// </summary>
    [Fact]
    public async Task ASearchThatStoppedShortSaysWhereItStopped()
    {
        using var install = new TempInstall();

        var said = await AskingFor(
            new CommodityAnswer([], 150, 0, true) { Horizon = 14.2 },
            install);

        Assert.DoesNotContain("within 250 light years", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("14.2 light years", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("have not looked", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And a search that reached the end of its radius still says so plainly. The caveat is for
    /// the case that earned it, not a hedge bolted onto every answer.
    /// </summary>
    [Fact]
    public async Task ASearchThatReachedTheEndStillClaimsTheRadius()
    {
        using var install = new TempInstall();

        var said = await AskingFor(new CommodityAnswer([], 30, 0, true), install);

        Assert.Contains("within 250 light years of Eurybia", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("have not looked", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The same rule on a positive answer: a best-of is a claim about everything it looked at, so
    /// the heading names the distance actually reached.
    /// </summary>
    [Fact]
    public async Task ABestOfNamesTheDistanceItActuallyReached()
    {
        using var install = new TempInstall();

        var market = new MarketSnapshot
        {
            Station = "Coleman Relay",
            System = "Enayex",
            X = 51.40625,
            Y = -54.40625,
            Z = -19.5,
            HasLargePad = true,
            Type = "Coriolis Starport",
            UpdatedAt = new DateTimeOffset(2026, 8, 28, 9, 0, 0, TimeSpan.Zero),
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Landmines"] = new("Landmines") { BuyPrice = 444, SellPrice = 430, Supply = 5229 },
            },
        };

        var said = await AskingFor(
            new CommodityAnswer([new CommodityOffer(market, 444, 200, 11.0)], 150, 0, true)
            {
                Horizon = 14.2,
            },
            install);

        Assert.Contains("within 14.2 ly of Eurybia", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("within 250 ly", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Coleman Relay", said, StringComparison.Ordinal);
    }
}
