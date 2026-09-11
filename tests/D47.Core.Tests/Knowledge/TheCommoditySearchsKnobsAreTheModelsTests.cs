using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Every knob the commodity search has is one the model can turn.</summary>
public class TheCommoditySearchsKnobsAreTheModelsTests
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

    /// <summary>One market, so the positive answer is a fixed string rather than an empty one.</summary>
    private static MarketSnapshot Coleman => new()
    {
        Station = "Coleman Relay",
        System = "Enayex",
        X = 51.40625,
        Y = -54.40625,
        Z = -19.5,
        HasLargePad = true,
        Type = "Coriolis Starport",
        Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
        {
            ["Landmines"] = new("Landmines") { BuyPrice = 444, SellPrice = 430, Supply = 5229 },
        },
    };

    private static async Task<(string Said, CommoditySearch Search)> AskAsync(
        TempInstall install,
        CommodityAnswer answer,
        params (string Key, string Value)[] arguments)
    {
        var surface = TestSurface.For(install);

        surface.Settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var trade = new Capturing(answer);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(new SilentGalaxy(), () => "Eurybia", surface.Settings, trade),
        ]);

        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["commodity"] = "Landmines" };

        foreach (var (key, value) in arguments)
        {
            values[key] = value;
        }

        var result = await registry.InvokeAsync(
            "find_nearest_station", new ToolArguments(values), TestContext.Current.CancellationToken);

        Assert.NotNull(trade.Last);

        return (result.Content, trade.Last);
    }

    /// <summary>The sentence that started this.</summary>
    [Fact]
    public async Task FiveHundredLightYearsMeansFiveHundred()
    {
        using var install = new TempInstall();

        var (said, search) = await AskAsync(
            install,
            new CommodityAnswer([], 30, 0, true),
            ("max_distance", "500"));

        Assert.Equal(500, search.Query.MaxDistance);
        Assert.Contains("out to 500 ly", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("250", said, StringComparison.Ordinal);
    }

    /// <summary>And nothing above it either.</summary>
    [Fact]
    public async Task ThereIsNoCeilingOnTheDistance()
    {
        using var install = new TempInstall();

        var (_, search) = await AskAsync(
            install,
            new CommodityAnswer([], 30, 0, true),
            ("max_distance", "5000"));

        Assert.Equal(5000, search.Query.MaxDistance);
    }

    /// <summary>
    /// "Expand your staleness filter" now has a road from the sentence to the search, and the widened
    /// bound is said back in days rather than in hours.
    /// </summary>
    [Fact]
    public async Task TwoMonthsOfPricesIsAskableAndIsSaidBack()
    {
        using var install = new TempInstall();

        var (said, search) = await AskAsync(
            install,
            new CommodityAnswer([], 30, 0, true),
            ("max_price_age_hours", "1440"));

        Assert.Equal(1440, search.MaxPriceAge);
        Assert.Contains("prices up to 60 days old", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Carriers are askable rather than hard-coded out.</summary>
    [Fact]
    public async Task CarriersAreAskableAndAreSaidBack()
    {
        using var install = new TempInstall();

        var (said, search) = await AskAsync(
            install,
            new CommodityAnswer([], 30, 0, true),
            ("include_carriers", "true"));

        Assert.True(search.Query.IncludeCarriers);
        Assert.Contains("fleet carriers included", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The knob and the ceiling, never a bare refusal. "That's as far as I search" is the sentence this
    /// issue exists to delete, so a bound that bites names the parameter it belongs to and the number
    /// it stops at.
    /// </summary>
    [Fact]
    public async Task ARefusedWideningNamesTheKnobAndTheCeiling()
    {
        using var install = new TempInstall();

        var (said, search) = await AskAsync(
            install,
            new CommodityAnswer([], 30, 0, true),
            ("max_price_age_hours", "17520"));

        Assert.Equal(8_760, search.MaxPriceAge);
        Assert.Contains("max_price_age_hours", said, StringComparison.Ordinal);
        Assert.Contains("8,760 hours", said, StringComparison.Ordinal);
        Assert.Contains("a year", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnqualifiedSearchIsUnchanged()
    {
        using var install = new TempInstall();

        var (said, search) = await AskAsync(
            install,
            new CommodityAnswer([new CommodityOffer(Coleman, 444, 5229, 11.0)], 30, 0, true));

        Assert.Equal(50, search.Query.MaxDistance);
        Assert.Equal(720, search.MaxPriceAge);
        Assert.False(search.Query.IncludeCarriers);
        Assert.False(search.Query.LargePadOnly);
        Assert.Null(search.Query.Tonnes);

        Assert.DoesNotContain("Searched", said, StringComparison.Ordinal);

        Assert.Equal(
            "Best for buying Landmines within 50 ly of Eurybia: Coleman Relay (Enayex), 11 ly, "
            + "444 cr a tonne, 5,229 in stock — undated. Prices are reported by other Commanders "
            + "and can be out of date; supply moves fastest.",
            said);
    }

    /// <summary>The negative answer keeps the same silence.</summary>
    [Fact]
    public async Task AnUnqualifiedEmptyAnswerIsUnchanged()
    {
        using var install = new TempInstall();

        var (said, _) = await AskAsync(install, new CommodityAnswer([], 30, 0, true));

        Assert.Equal("Nothing within 50 light years of Eurybia is buying Landmines.", said);
    }
}
