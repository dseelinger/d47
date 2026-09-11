using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>What d47 is allowed to claim when it found nothing.</summary>
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

        public Task<StationQuote?> QuoteAsync(
            long marketId,
            string commodity,
            CancellationToken cancellationToken) => Task.FromResult<StationQuote?>(null);
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
    /// A search that ran out of budget before it ran out of galaxy says how far it got, and does not
    /// claim the radius it was asked about.
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

    [Fact]
    public async Task ASearchThatReachedTheEndStillClaimsTheRadius()
    {
        using var install = new TempInstall();

        var said = await AskingFor(new CommodityAnswer([], 30, 0, true), install);

        Assert.Contains("within 250 light years of Eurybia", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("have not looked", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The one station the positive answers are made of: the 5,229 units eleven light years from
    /// Eurybia that #156 was reported over.
    /// </summary>
    private static MarketSnapshot ColemanRelay() => new()
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

    /// <summary>
    /// The same rule on a positive answer: a best-of is a claim about everything it looked at, so the
    /// heading names the distance actually reached.
    /// </summary>
    [Fact]
    public async Task ABestOfNamesTheDistanceItActuallyReached()
    {
        using var install = new TempInstall();

        var said = await AskingFor(
            new CommodityAnswer([new CommodityOffer(ColemanRelay(), 444, 200, 11.0)], 150, 0, true)
            {
                Horizon = 14.2,
            },
            install);

        Assert.Contains("within 14.2 ly of Eurybia", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("within 250 ly", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Coleman Relay", said, StringComparison.Ordinal);
    }

 /// <summary>And a cut-short answer says the reach is unknown rather than inventing one.</summary>
    [Fact]
    public async Task AnAnswerCutShortSaysSoAndNamesNoDistance()
    {
        using var install = new TempInstall();

        var said = await AskingFor(
            new CommodityAnswer([], 1_000, 0, true) { Complete = false },
            install);

        Assert.DoesNotContain("within 250 light years", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1000 markets I could check", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot say how far out the index looked", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("more I have not seen", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And an index that sent nothing back claims nothing either (#350 review).</summary>
    [Fact]
    public async Task AnIndexThatSentNothingBackClaimsNoRadius()
    {
        using var install = new TempInstall();

        var said = await AskingFor(
            new CommodityAnswer([], 0, 0, false) { Complete = false },
            install);

        Assert.DoesNotContain("within 250 light years", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0 markets", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing the market index sent me", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot say how far out the index looked", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The same on a positive answer, where what a cut list is missing is near stock rather than far —
    /// which is how #350 came to name a station that was not the nearest.
    /// </summary>
    [Fact]
    public async Task ABestOfOutOfACutListSaysThereMayBeNearerStock()
    {
        using var install = new TempInstall();

        var said = await AskingFor(
            new CommodityAnswer([new CommodityOffer(ColemanRelay(), 444, 200, 11.0)], 1_000, 0, true)
            {
                Complete = false,
            },
            install);

        Assert.Contains("nearer stock it did not send", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1000 markets", said, StringComparison.OrdinalIgnoreCase);
    }
}
