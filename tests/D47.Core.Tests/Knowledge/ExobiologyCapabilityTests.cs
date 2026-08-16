using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Finding exobiology (list.md Phase 18, "Find the exobiology") — two halves from two sources, and
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
    /// Capabilities are state rather than guards (list.md Phase 3). With no plotter composed the
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
