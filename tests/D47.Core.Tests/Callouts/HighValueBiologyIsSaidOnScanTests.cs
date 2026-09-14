using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A landable body whose biology could reach the threshold, said on its scan.</summary>
public class HighValueBiologyIsSaidOnScanTests
{
    private const long SystemAddress = 1_000_200;
    private const int BodyId = 23;
    private const string System = "Fixture";
    private const string BodyName = "Fixture 3 b";

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Signals(int biological) => $$"""
        {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"{{BodyName}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{biological}}}]}
        """;

    private static string Genera(params string[] genera) => $$"""
        {"timestamp":"2026-08-16T10:02:00Z","event":"SAASignalsFound","BodyName":"{{BodyName}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
         "Genuses":[{{string.Join(",", genera.Select(genus => $$"""{"Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}"}"""))}}]}
        """;

    // 0.5 g, 200 K and 0.02 atm admit Bacterium and Stratum; 0.3 g at 180 K admits the same two genera, both
    // under 3 million.
    private static string Scan(bool landable = true, double gravity = 0.5, double temperature = 200, string at = "10:01:00") => $$"""
        {"timestamp":"2026-08-16T{{at}}Z","event":"Scan","ScanType":"Detailed","StarSystem":"{{System}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},"BodyName":"{{BodyName}}",
         "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
         "SurfaceGravity":{{gravity * 9.80665}},"SurfaceTemperature":{{temperature}},"SurfacePressure":{{0.02 * 101325}},
         "Landable":{{(landable ? "true" : "false")}}}
        """;

    [Fact]
    public void TwoSignalsOnALandableBodyAreSaidAsTheSumOfTheirGenerasBest()
    {
        var scan = Scan();
        var state = Commander(Signals(2), scan);

        var said = Assert.Single(new BiologyCallout().Examine(Context(state, false, scan)));

        Assert.Equal("3 b could hold up to 18.2 million in biology: Stratum, Bacterium.", said.Text);
    }

    [Fact]
    public void ABestCaseBelowTheThresholdSaysNothing()
    {
        var scan = Scan(gravity: 0.3, temperature: 180);
        var state = Commander(Signals(2), scan);

        Assert.Empty(new BiologyCallout().Examine(Context(state, false, scan)));
    }

    [Fact]
    public void ABodyThatIsNotLandableSaysNothing()
    {
        var scan = Scan(landable: false);
        var state = Commander(Signals(2), scan);

        Assert.Empty(new BiologyCallout().Examine(Context(state, false, scan)));
    }

    [Fact]
    public void ABodyWithNoBiologicalCountSaysNothing()
    {
        var scan = Scan();
        var state = Commander(scan);

        Assert.Empty(new BiologyCallout().Examine(Context(state, false, scan)));
    }

    [Fact]
    public void PrimingSaysNothingAndTheBodyIsNotSaidLater()
    {
        var callout = new BiologyCallout();
        var scan = Scan();
        var state = Commander(Signals(2), scan);

        Assert.Empty(callout.Examine(Context(state, true, scan)));

        var rescan = Scan(at: "10:05:00");
        state = Commander(Signals(2), scan, rescan);

        Assert.Empty(callout.Examine(Context(state, false, rescan)));
    }

    [Fact]
    public void ASecondScanOfTheSameBodySaysNothing()
    {
        var callout = new BiologyCallout();
        var scan = Scan();
        var rescan = Scan(at: "10:05:00");

        Assert.Single(callout.Examine(Context(Commander(Signals(2), scan), false, scan)));
        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, rescan), false, rescan)));
    }

    [Fact]
    public void NamedGeneraAreSaidAsARangeOverThoseGenera()
    {
        var scan = Scan(at: "10:05:00");
        var state = Commander(Signals(2), Genera("Stratum"), scan);

        var said = Assert.Single(new BiologyCallout().Examine(Context(state, false, scan)));

        Assert.Equal("3 b could hold 2.6 million to 16.2 million in biology: Stratum.", said.Text);
    }

    [Fact]
    public void WithTheToggleOffNothingIsAnnounced()
    {
        var scan = Scan();
        var state = Commander(Signals(2), scan);
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new BiologyCallout());

        engine.SetEnabled("biology", false);
        engine.Tick(Context(state, false, scan));

        Assert.Empty(engine.Drain());
    }
}
