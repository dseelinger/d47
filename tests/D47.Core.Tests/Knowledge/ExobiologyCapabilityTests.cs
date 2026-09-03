using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Finding exobiology (Phase 18, "Find the exobiology") — two halves from two sources, and
/// the tests that keep them from being mistaken for each other.
/// </summary>
public class ExobiologyCapabilityTests
{
    private sealed class FakeRoutes : IRouteService
    {
        public ExobiologyQuery? LastQuery { get; private set; }

        public ExobiologyRoute? Route { get; set; } =
            new([
                new ExobiologyStop("Opet", 3, [
                    new ExobiologyBody("Opet 7 b", "Rocky body")
                    {
                        DistanceToArrival = 2536.77,
                        LandmarkValue = 6_904_100,
                        Species =
                        [
                            new ExobiologySpecies("Frutexa", "Frutexa Flabellum", 1, 1_808_900),
                            new ExobiologySpecies("Tussock", "Tussock Cultro", 1, 1_766_600),
                        ],
                    },
                ]),
            ]);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(
            ExobiologyQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(Route);
        }

        public Task<PlottedRoute?> PlotAsync(RouteQuery q, CancellationToken t) =>
            Task.FromResult<PlottedRoute?>(null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery q, CancellationToken t) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<TradeRoute?> PlotTradeAsync(TradeQuery q, CancellationToken t) =>
            Task.FromResult<TradeRoute?>(null);
    }

    private static GameStateStore Store(params string[] lines)
    {
        var gameState = new GameStateStore();

        gameState.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var line in lines)
        {
            gameState.Apply(Parse(line));
        }

        return gameState;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Task<ToolResult> Ask(
        GameStateStore gameState,
        string tool,
        string json = "{}",
        IRouteService? routes = null) =>
        CapabilityRegistry
            .Build([ExobiologyCapability.Create(routes, () => gameState.Active)])
            .InvokeAsync(tool, ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

    /// <summary>A real scan: two signal kinds, one genus. All 792 corpus events carry this shape.</summary>
    private const string Scan =
        """
        {"timestamp":"2026-08-16T10:00:00Z","event":"SAASignalsFound","BodyName":"HR 3230 3 a a",
         "SystemAddress":182359951707,"BodyID":20,
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1},
                    {"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":3}],
         "Genuses":[{"Genus":"$Codex_Ent_Brancae_Name;","Genus_Localised":"Brain Trees"}]}
        """;

    // ------------------------------------------------------------- the body

    [Fact]
    public async Task TheScanIsReadBackWithItsGeneraAndItsOtherSignals()
    {
        var result = await Ask(Store(Scan), "get_body_biology");

        Assert.Contains("HR 3230 3 a a", result.Content, StringComparison.Ordinal);
        Assert.Contains("1 biological signal: Brain Trees", result.Content, StringComparison.Ordinal);
        Assert.Contains("3 Geological", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ceiling that shapes this half. Elite names <em>Bacterium</em>, never <em>Bacterium
    /// Alcyoneum</em>, and the species is what sets the price — so this must not quote a figure.
    /// </summary>
    [Fact]
    public async Task TheJournalHalfNamesTheGenusAndRefusesToPriceIt()
    {
        var result = await Ask(Store(Scan), "get_body_biology");

        Assert.Contains("names the genus and not the species", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(" cr", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A ring signal Elite did not localise is said the way the rest of the list is said.
    /// <para>
    /// <b>Measured, and it is one material out of twelve.</b> Where <c>SAASignalsFound</c> writes a
    /// ring's mineral it usually omits <c>Type_Localised</c> and leaves a bare symbol — eleven of
    /// which are already title case (<c>Alexandrite</c>, <c>Painite</c>, <c>Serendibite</c>) and one
    /// of which is not. Across 912 journals Tritium arrives as <c>tritium</c> 22 times against
    /// <c>Tritium</c> 21, so the same mineral appears twice in two spellings in one list — and
    /// because it is the only lower-case one, every other material makes this look correct.
    /// </para>
    /// <para>
    /// The same defect and the same remedy as <c>ProspectedRock.Display</c>, which Phase 18 shipped
    /// one file away for the prospector's identical habit.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ARingSignalEliteLeftUnlocalisedIsSpokenLikeTheRest()
    {
        var result = await Ask(
            Store(
                """
                {"timestamp":"2026-08-16T10:00:00Z","event":"SAASignalsFound","BodyName":"Fixture 1 a Ring",
                 "SystemAddress":1,"BodyID":4,
                 "Signals":[{"Type":"tritium","Count":4},{"Type":"Alexandrite","Count":2}],
                 "Genuses":[]}
                """),
            "get_body_biology");

        Assert.Contains("4 Tritium", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("tritium", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A scan that found nothing is a real answer and the one that saves a landing — different from
    /// d47 never having looked.
    /// </summary>
    [Fact]
    public async Task ABodyScannedAndBareIsSaidDifferentlyFromABodyNeverScanned()
    {
        var bare = await Ask(
            Store(
                """
                {"timestamp":"2026-08-16T10:00:00Z","event":"SAASignalsFound","BodyName":"Fixture 1",
                 "SystemAddress":1,"BodyID":1,
                 "Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":2}],
                 "Genuses":[]}
                """),
            "get_body_biology");

        Assert.Contains("No biological signals", bare.Content, StringComparison.Ordinal);

        var never = await Ask(Store(), "get_body_biology");
        Assert.Contains("no surface scans yet", never.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The FSS resolves a body before anyone flies to it, and 349 of 429 FSS-signalled bodies in the
    /// 944-journal corpus are never surface-scanned — so it is the only source d47 will ever see for
    /// four bodies in five (#275). It says how many biological signals, never which genus.
    /// </summary>
    [Fact]
    public async Task AnFssRowSaysTheCountAndThatItIsNotMapped()
    {
        var result = await Ask(
            Store(
                """
                {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture 3 b",
                 "SystemAddress":1,"BodyID":23,
                 "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":3}]}
                """),
            "get_body_biology");

        Assert.Contains("Fixture 3 b", result.Content, StringComparison.Ordinal);
        Assert.Contains("FSS reported 3 biological signals", result.Content, StringComparison.Ordinal);
        Assert.Contains("not been mapped", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("No biological signals", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Order matters: a surface scan is the later, fuller answer and always replaces an FSS row for
    /// the same body.
    /// </summary>
    [Fact]
    public async Task ASurfaceScanReplacesAnEarlierFssRowForTheSameBody()
    {
        var result = await Ask(
            Store(
                """
                {"timestamp":"2026-08-16T09:30:00Z","event":"FSSBodySignals","BodyName":"HR 3230 3 a a",
                 "SystemAddress":182359951707,"BodyID":20,
                 "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}]}
                """,
                Scan),
            "get_body_biology");

        Assert.Contains("Brain Trees", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("has not been mapped", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other direction must never happen: an FSS row arriving after a surface scan must not
    /// erase the genera the surface scan already found.
    /// </summary>
    [Fact]
    public async Task AnFssRowNeverOverwritesAnExistingSurfaceScan()
    {
        var result = await Ask(
            Store(
                Scan,
                """
                {"timestamp":"2026-08-16T11:00:00Z","event":"FSSBodySignals","BodyName":"HR 3230 3 a a",
                 "SystemAddress":182359951707,"BodyID":20,
                 "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}]}
                """),
            "get_body_biology");

        Assert.Contains("Brain Trees", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("has not been mapped", result.Content, StringComparison.Ordinal);
    }

    /// <summary>A Commander already in Opet says "7 b", not "Opet 7 b".</summary>
    [Fact]
    public async Task ABodyIsFoundByTheShortNameACommanderWouldSay()
    {
        var result = await Ask(Store(Scan), "get_body_biology", """{"body":"3 a a"}""");

        Assert.Contains("HR 3230 3 a a", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownBodyListsTheOnesThatWereScanned()
    {
        var result = await Ask(Store(Scan), "get_body_biology", """{"body":"Sol 4"}""");

        Assert.Contains("no surface scan for \"Sol 4\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("HR 3230 3 a a", result.Content, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ the route

    [Fact]
    public async Task ThePlotNamesSpeciesAndQuotesWhatTheyPay()
    {
        var result = await Ask(
            Store("""{"timestamp":"2026-08-16T09:05:00Z","event":"FSDJump","StarSystem":"Sol"}"""),
            "plot_exobiology_route",
            "{}",
            new FakeRoutes());

        Assert.Contains("Opet", result.Content, StringComparison.Ordinal);
        Assert.Contains("Opet 7 b (Rocky body, 2,536 ls out)", result.Content, StringComparison.Ordinal);

        // The species, which is the half the journal cannot supply.
        Assert.Contains("Frutexa Flabellum — 1,808,900 cr", result.Content, StringComparison.Ordinal);
        Assert.Contains("6,904,100 cr", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The structural limit, said in the answer rather than left for the Commander to work out after
    /// the flight: everything an index holds has been visited, so none of it pays the 5× bonus.
    /// </summary>
    [Fact]
    public async Task ThePlotSaysOutLoudThatNoneOfItCanBeAFirstFootfall()
    {
        var result = await Ask(Store(), "plot_exobiology_route", """{"from":"Sol"}""", new FakeRoutes());

        Assert.Contains("none of it is a first footfall", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A route plotted at a default 50 ly for a ship that jumps 18 is a route nobody can fly, so the
    /// ship's own range is used where the Commander did not say one.
    /// </summary>
    [Fact]
    public async Task TheShipsOwnJumpRangeIsUsedWhenNoneIsGiven()
    {
        var routes = new FakeRoutes();

        await Ask(
            Store(
                """
                {"timestamp":"2026-08-16T09:00:00Z","event":"Loadout","Ship":"diamondbackxl","ShipID":3,
                 "MaxJumpRange":62.5,"Modules":[]}
                """,
                """{"timestamp":"2026-08-16T09:05:00Z","event":"FSDJump","StarSystem":"Sol"}"""),
            "plot_exobiology_route",
            "{}",
            routes);

        Assert.Equal(62.5, routes.LastQuery!.JumpRange);
        Assert.Equal("Sol", routes.LastQuery.From);
    }

    [Fact]
    public async Task AnEmptyPlotSuggestsWhatToWidenRatherThanReportingFailure()
    {
        var result = await Ask(
            Store(),
            "plot_exobiology_route",
            """{"from":"Sol"}""",
            new FakeRoutes { Route = new ExobiologyRoute([]) });

        Assert.False(result.IsError);
        Assert.Contains("wider radius or a lower minimum", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Capabilities are state rather than guards (Phase 3). With no plotter composed the
    /// journal half still answers, so this must not dead-end.
    /// </summary>
    [Fact]
    public async Task WithNoPlotterTheJournalHalfStillAnswers()
    {
        var gameState = Store(Scan);

        var plot = await Ask(gameState, "plot_exobiology_route", """{"from":"Sol"}""");
        Assert.False(plot.IsError);
        Assert.Contains("not available in this build", plot.Content, StringComparison.Ordinal);

        var body = await Ask(gameState, "get_body_biology");
        Assert.Contains("Brain Trees", body.Content, StringComparison.Ordinal);
    }
}
