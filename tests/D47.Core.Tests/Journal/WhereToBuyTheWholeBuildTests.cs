using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Everything one build still needs, and where to buy it (Phase 50).
/// <para>
/// <b>A parameter on the tool whose sentence already covers the subject</b>, not a tool of its own:
/// the surface had 136 bytes spare after Phase 49, a fresh tool description costs hundreds, and the
/// widening was paid for by trimming redundancy inside this same capability — the trade Phase 49
/// made, for the same reason.
/// </para>
/// <para>
/// The covering arithmetic itself is <c>ColonisationSourcingTests</c>; this is about the answer the
/// Commander actually hears.
/// </para>
/// </summary>
public class WhereToBuyTheWholeBuildTests
{
    private sealed class FakeTrade : ITradePlanService
    {
        public SourcingSearch? Last { get; private set; }

        public SourcingAnswer Answer { get; set; } = SourcingAnswer.Empty;

        public Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<TradeRoute?>(null);

        public Task<CommodityAnswer> FindCommodityAsync(
            CommoditySearch search, CancellationToken cancellationToken) =>
            Task.FromResult(CommodityAnswer.Empty);

        public Task<SourcingAnswer> SourceConstructionAsync(
            SourcingSearch search, CancellationToken cancellationToken)
        {
            Last = search;

            return Task.FromResult(Answer);
        }
    }

    private const string Docked =
        """
        {"timestamp":"2026-08-25T09:30:00Z","event":"Docked","StationName":"Ratraii Construction Site",
         "StarSystem":"Ratraii","MarketID":3960809986}
        """;

    private const string Depot =
        """
        { "timestamp":"2026-08-25T10:00:00Z", "event":"ColonisationConstructionDepot",
          "MarketID":3960809986, "ConstructionProgress":0.25,
          "ConstructionComplete":false, "ConstructionFailed":false,
          "ResourcesRequired":[
            { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
              "RequiredAmount":500, "ProvidedAmount":100, "Payment":3239 },
            { "Name":"$steel_name;", "Name_Localised":"Steel",
              "RequiredAmount":300, "ProvidedAmount":0, "Payment":5000 } ] }
        """;

    private static GameStateStore Store()
    {
        var gameState = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-25T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                     Docked.ReplaceLineEndings(" "),
                     Depot.ReplaceLineEndings(" "),
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        return gameState;
    }

    private static MarketSnapshot Market(string station, params (string Commodity, int Buy, int Supply)[] quotes) =>
        new()
        {
            Station = station,
            System = station,
            HasLargePad = true,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Quotes = quotes.ToDictionary(
                quote => quote.Commodity,
                quote => new MarketQuote(quote.Commodity) { BuyPrice = quote.Buy, Supply = quote.Supply },
                StringComparer.OrdinalIgnoreCase),
        };

    private static SettingsService Settings(TempInstall install)
    {
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        return new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);
    }

    private static CarrierManifest Manifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "d47-where-to-buy", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        return new CarrierManifest(
            Path.Combine(root, "carrier.json"), NullLogger<CarrierManifest>.Instance);
    }

    private static async Task<(string Said, FakeTrade Trade, SourcingBoard Board)> AskAsync(
        TempInstall install,
        CarrierManifest? carrier = null,
        bool lookups = true,
        SourcingAnswer? answer = null)
    {
        var gameState = Store();
        var trade = new FakeTrade { Answer = answer ?? SourcingAnswer.Empty };
        var board = new SourcingBoard();

        var settings = Settings(install);

        settings.Replace(
            "the test",
            current => current with { Knowledge = current.Knowledge with { GalaxySearch = lookups } });

        var registry = CapabilityRegistry.Build(
        [
            ColonisationCapability.Create(
                () => gameState.Active,
                null,
                settings,
                trade,
                carrier,
                board,
                () => new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)),
        ]);

        var result = await registry.InvokeAsync(
            "get_construction_needs",
            ToolArguments.FromJson("""{"where_to_buy":true}"""),
            TestContext.Current.CancellationToken);

        return (result.Content, trade, board);
    }

    /// <summary>
    /// The unit is <b>this station covers six of your twenty</b>, because that is the sentence a
    /// Commander acts on.
    /// </summary>
    [Fact]
    public async Task TheAnswerNamesTheStationsAndWhatEachOneCovers()
    {
        using var install = new TempInstall();

        var market = Market("Hutton Orbital", ("Aluminium", 300, 1000), ("Steel", 200, 1000));

        var (said, _, _) = await AskAsync(
            install,
            answer: new SourcingAnswer(
                new SourcingPlan(
                    [
                        new SourcingStop(
                            market,
                            [
                                new SourcingLot("Aluminium", "aluminium", 400, 300),
                                new SourcingLot("Steel", "steel", 300, 200),
                            ],
                            14.5),
                    ],
                    [],
                    new Dictionary<string, int>(StringComparer.Ordinal)),
                12,
                0,
                true));

        Assert.Contains("Hutton Orbital", said, StringComparison.Ordinal);
        Assert.Contains("covers 2", said, StringComparison.Ordinal);
        Assert.Contains("400 tonnes Aluminium", said, StringComparison.Ordinal);
        Assert.Contains("14.5 ly", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing is dropped in silence.</b> Every outstanding row either resolves to a station or
    /// is named as one d47 could not price — and found-but-short is said separately from
    /// never-found, because "widen the search" is right for one and useless for the other.
    /// </summary>
    [Fact]
    public async Task WhatCouldNotBePricedAndWhatRanShortAreSaidSeparately()
    {
        using var install = new TempInstall();

        var (said, _, _) = await AskAsync(
            install,
            answer: new SourcingAnswer(
                new SourcingPlan(
                    [],
                    ["Aluminium"],
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["Steel"] = 120 }),
                12,
                3,
                true));

        Assert.Contains("Nothing in range prices: Aluminium", said, StringComparison.Ordinal);
        Assert.Contains("Stocked but not enough: Steel by 120 tonnes", said, StringComparison.Ordinal);

        // And markets left out for age are counted rather than swallowed.
        Assert.Contains("3 markets were left out", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Commander's own carrier figure comes off the shopping list, is named, and is dated —
    /// because it is the one number in the answer d47 has no way of checking.
    /// </summary>
    [Fact]
    public async Task WhatTheCommanderSaysIsOnTheCarrierComesOffTheList()
    {
        using var install = new TempInstall();

        var carrier = Manifest();

        carrier.Set("F1", "Steel", 100, new DateTimeOffset(2026, 8, 25, 8, 0, 0, TimeSpan.Zero));

        var (said, trade, _) = await AskAsync(install, carrier);

        Assert.Contains("on the carrier", said, StringComparison.Ordinal);
        Assert.Contains("100 tonnes Steel", said, StringComparison.Ordinal);

        // And the search was asked for what is left after it, not for the depot's own figure.
        Assert.NotNull(trade.Last);
        Assert.Equal(200, trade.Last.Outstanding.Single(row => row.Name == "Steel").Remaining);

        // The site's own outstanding list is untouched by it: the depot event is a snapshot rather
        // than a delta, and recomputing what a site owes is the trap that caught two other folds.
        Assert.Contains("400 tonnes left", said, StringComparison.Ordinal);
        Assert.Contains("300 tonnes left", said, StringComparison.Ordinal);
    }

    /// <summary>A carrier that covers the whole list means there is nothing to go and buy.</summary>
    [Fact]
    public async Task ACarrierThatCoversItAllIsSaidAndNothingIsSearched()
    {
        using var install = new TempInstall();

        var carrier = Manifest();
        var when = new DateTimeOffset(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);

        carrier.Set("F1", "Aluminium", 400, when);
        carrier.Set("F1", "Steel", 300, when);

        var (said, trade, _) = await AskAsync(install, carrier);

        Assert.Contains("The carrier covers the whole of it", said, StringComparison.Ordinal);
        Assert.Null(trade.Last);
    }

    /// <summary>
    /// Posted on the way out, so the Checklist tab draws the answer the Commander was just given
    /// rather than running a second search that could disagree with it.
    /// </summary>
    [Fact]
    public async Task TheAnswerIsPostedForThePanel()
    {
        using var install = new TempInstall();

        var (_, _, board) = await AskAsync(install);

        Assert.NotNull(board.Last);
        Assert.Equal("Ratraii", board.Last.Near);
        Assert.Contains("Ratraii", board.Last.Site, StringComparison.Ordinal);
    }

    /// <summary>
    /// The gate. Asking where to buy leaves the machine, so it is behind the same switch as every
    /// other question that does — and the tracking half above it still answers.
    /// </summary>
    [Fact]
    public async Task WithLookupsOffItSaysSoAndTheHaulingListStillArrives()
    {
        using var install = new TempInstall();

        var (said, trade, _) = await AskAsync(install, lookups: false);

        Assert.Contains("switched off", said, StringComparison.Ordinal);
        Assert.Null(trade.Last);

        // The half that reads only the Commander's own disk is untouched by the switch.
        Assert.Contains("Aluminium", said, StringComparison.Ordinal);
    }

    /// <summary>And without the parameter, the tool answers exactly as it did before.</summary>
    [Fact]
    public async Task WithoutTheParameterNothingIsSearched()
    {
        using var install = new TempInstall();

        var gameState = Store();
        var trade = new FakeTrade();

        var registry = CapabilityRegistry.Build(
            [ColonisationCapability.Create(() => gameState.Active, null, Settings(install), trade)]);

        var result = await registry.InvokeAsync(
            "get_construction_needs", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        Assert.Null(trade.Last);
        Assert.DoesNotContain("shopping", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Aluminium", result.Content, StringComparison.Ordinal);
    }
}
