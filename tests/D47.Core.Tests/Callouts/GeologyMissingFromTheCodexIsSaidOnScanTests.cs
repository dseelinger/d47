using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A landable body's likely geological features missing from the codex for this region, said on its scan.</summary>
public class GeologyMissingFromTheCodexIsSaidOnScanTests
{
    private const long SystemAddress = 1_000_300;
    private const int BodyId = 7;
    private const string BodyName = "Fixture B 3";
    private const long WaterIceFumarole = 1400158;
    private const long WaterIceGeyser = 1400258;

    private static readonly int Here = GalacticRegions.IdOf(GalacticRegions.Find(StarPosition.Origin)!)!.Value;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(bool whole, params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:01Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000300,"StarPos":[0,0,0]}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        var state = store.Active!;
        state.CodexLedger = state.CodexLedger with { IsWhole = whole };

        return state;
    }

    private static string Logged(long entryId, int region) => $$"""
        {"timestamp":"2026-08-10T09:00:00Z","event":"CodexEntry","EntryID":{{entryId}},"Name":"$Codex_Ent_{{entryId}};",
         "Region":"$Codex_RegionName_{{region}};","System":"Elsewhere","SystemAddress":5,"BodyID":1}
        """;

    private static string Signals(int geological, string kind = "FSSBodySignals") => $$"""
        {"timestamp":"2026-08-16T10:00:00Z","event":"{{kind}}","BodyName":"{{BodyName}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":{{geological}}}]}
        """;

    private static string Scan(
        bool landable = true, string planetClass = "Icy body", string volcanism = "major water geysers volcanism") => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},"BodyName":"{{BodyName}}",
         "PlanetClass":"{{planetClass}}","Atmosphere":"","Volcanism":"{{volcanism}}","Landable":{{(landable ? "true" : "false")}}}
        """;

    [Fact]
    public void BothUnloggedFeaturesAreNamedOnTheScanAfterTheSignals()
    {
        var scan = Scan();
        var state = Commander(true, Signals(1), scan);

        var said = Assert.Single(new GeologyCallout().Examine(Context(state, false, scan)));

        Assert.Equal(
            "B 3 could have water ice fumaroles or water ice geysers, not in your codex for this region.",
            said.Text);
    }

    [Fact]
    public void ALoggedFeatureIsLeftOut()
    {
        var scan = Scan();
        var state = Commander(true, Logged(WaterIceGeyser, Here), Signals(1), scan);

        var said = Assert.Single(new GeologyCallout().Examine(Context(state, false, scan)));

        Assert.Equal("B 3 could have water ice fumaroles, not in your codex for this region.", said.Text);
    }

    [Fact]
    public void BothLoggedInThisRegionSaysNothing()
    {
        var scan = Scan();
        var state = Commander(true, Logged(WaterIceFumarole, Here), Logged(WaterIceGeyser, Here), Signals(1), scan);

        Assert.Empty(new GeologyCallout().Examine(Context(state, false, scan)));
    }

    [Fact]
    public void BothLoggedInAnotherRegionAreStillNamed()
    {
        var scan = Scan();
        var state = Commander(true, Logged(WaterIceFumarole, Here + 1), Logged(WaterIceGeyser, Here + 1), Signals(1), scan);

        var said = Assert.Single(new GeologyCallout().Examine(Context(state, false, scan)));

        Assert.Contains("water ice fumaroles or water ice geysers", said.Text);
    }

    [Fact]
    public void ALedgerNotYetWholeSaysNothingUntilItIsAndTheBodyIsScannedAgain()
    {
        var scan = Scan();
        var callout = new GeologyCallout();
        var partial = Commander(false, Signals(1), scan);

        Assert.Empty(callout.Examine(Context(partial, false, scan)));

        var whole = Commander(true, Signals(1), scan);

        Assert.Single(callout.Examine(Context(whole, false, scan)));
    }

    [Fact]
    public void AScanBeforeTheSignalsIsSaidOnTheSignals()
    {
        var scan = Scan();
        var signals = Signals(1);
        var state = Commander(true, scan, signals);
        var callout = new GeologyCallout();

        Assert.Empty(callout.Examine(Context(Commander(true, scan), false, scan)));
        Assert.Single(callout.Examine(Context(state, false, signals)));
    }

    [Fact]
    public void ABodySeenWhilePrimingIsNeverSaid()
    {
        var scan = Scan();
        var state = Commander(true, Signals(1), scan);
        var callout = new GeologyCallout();

        Assert.Empty(callout.Examine(Context(state, true, scan)));
        Assert.Empty(callout.Examine(Context(state, false, scan)));
    }

    [Fact]
    public void ABodyThatIsNotLandableSaysNothing()
    {
        var scan = Scan(landable: false);

        Assert.Empty(new GeologyCallout().Examine(Context(Commander(true, Signals(1), scan), false, scan)));
    }

    [Fact]
    public void ABodyWithNoGeologicalCountSaysNothing()
    {
        var scan = Scan();

        Assert.Empty(new GeologyCallout().Examine(Context(Commander(true, Signals(0), scan), false, scan)));
    }

    [Fact]
    public void ABodyWithNoLikelyFeaturesSaysNothing()
    {
        var scan = Scan(planetClass: "Gas giant", volcanism: "");

        Assert.Empty(new GeologyCallout().Examine(Context(Commander(true, Signals(1), scan), false, scan)));
    }

    [Fact]
    public void ASecondScanOfASaidBodySaysNothing()
    {
        var scan = Scan();
        var state = Commander(true, Signals(1), scan);
        var callout = new GeologyCallout();

        Assert.Single(callout.Examine(Context(state, false, scan)));
        Assert.Empty(callout.Examine(Context(state, false, scan)));
    }

    [Fact]
    public void MoreThanThreeMissingNamesThreeAndCountsTheRest()
    {
        var scan = Scan(planetClass: "Rocky body", volcanism: "minor rocky magma volcanism");
        var state = Commander(true, Signals(1), scan);

        var said = Assert.Single(new GeologyCallout().Examine(Context(state, false, scan)));

        Assert.Equal(
            "B 3 could have sulphur dioxide fumaroles, silicate magma lava spouts, sulphur dioxide gas vents and one more, not in your codex for this region.",
            said.Text);
    }

    [Fact]
    public void ThePlayerCanTurnItOff()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new GeologyCallout());
        engine.SetEnabled("geology", false, DateTimeOffset.UnixEpoch);
        var scan = Scan();

        engine.Tick(Context(Commander(true, Signals(1), scan), false, scan));

        Assert.Empty(engine.Drain());
    }
}
