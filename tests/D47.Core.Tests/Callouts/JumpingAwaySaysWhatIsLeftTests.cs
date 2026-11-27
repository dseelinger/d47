using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The biology and mapping left in the system, said when a hyperspace jump starts (#2761).</summary>
public class JumpingAwaySaysWhatIsLeftTests
{
    private const long Here = 1000300;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Water(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Water world","Atmosphere":"","Landable":false,
         "MassEM":0.5,"TerraformState":"","WasDiscovered":true,"WasMapped":true}
        """;

    private static string Rocky(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere",
         "Volcanism":"","SurfaceGravity":1.9,"SurfaceTemperature":175.0,"SurfacePressure":1200.0,"Landable":true,"MassEM":0.02,
         "TerraformState":"","WasDiscovered":true,"WasMapped":true}
        """;

    private static string Biological(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}]}
        """;

    private static string Surveyed(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:30:00Z","event":"SAASignalsFound","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}],
         "Genuses":[{"Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum"}]}
        """;

    private static string Sample(int bodyId) => $$"""
        {"timestamp":"2026-11-20T11:00:00Z","event":"ScanOrganic","ScanType":"Log",
         "Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum",
         "Species":"$Codex_Ent_Stratum_02_Name;","Species_Localised":"Stratum Paleas",
         "SystemAddress":{{Here}},"Body":{{bodyId}}}
        """;

    private static string Approach(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:50:00Z","event":"ApproachBody","StarSystem":"Fixture","SystemAddress":{{Here}},
         "Body":"Fixture {{bodyId}}","BodyID":{{bodyId}}}
        """;

    private static string Jump(string jumpType) => $$"""
        {"timestamp":"2026-11-20T12:00:00Z","event":"StartJump","JumpType":"{{jumpType}}","Taxi":false,
         "StarSystem":"Elsewhere","SystemAddress":1,"StarClass":"K"}
        """;

    private sealed class World
    {
        private readonly CartographyLedger _ledger = new(null, new MemoryFileSystem(), NullLogger.Instance);
        private readonly GameStateStore _store = new();

        public World(params string[] lines)
        {
            _ledger.FoldHistory([], TestContext.Current.CancellationToken);

            var events = new[]
                {
                    """{"timestamp":"2026-11-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                    """{"timestamp":"2026-11-20T09:00:01Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1}""",
                    $$"""{"timestamp":"2026-11-20T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":{{Here}},"StarPos":[0,0,0]}""",
                }
                .Concat(lines)
                .Select(Parse)
                .ToList();

            foreach (var journalEvent in events)
            {
                _store.Apply(journalEvent);
            }

            _ledger.Apply(events);
            _store.Active!.CodexLedger = _store.Active.CodexLedger with { IsWhole = true };
        }

        public (List<string> Left, List<string> Unfinished) Jump(string line, bool priming = false)
        {
            var journalEvent = Parse(line);
            _store.Apply(journalEvent);

            var context = new CalloutContext(
                DateTimeOffset.UnixEpoch, priming, _store.Active, GameStatus.Unknown, NavRoute.None, [journalEvent]);

            return (
                [.. new SystemLeftCallout(_ledger).Examine(context).Select(announcement => announcement.Text)],
                [.. new UnfinishedSamplesCallout().Examine(context).Select(announcement => announcement.Text)]);
        }
    }

    [Fact]
    public void TwoPlanetsToMapAndABiologyBodyAreCounted()
    {
        var world = new World(Water(20), Water(30), Biological(10), Rocky(10), Surveyed(10));

        Assert.Equal(["Leaving two bodies worth mapping and one with biology."], world.Jump(Jump("Hyperspace")).Left);
    }

    [Fact]
    public void OneOfEachIsSaidInTheSingular()
    {
        var world = new World(Water(20), Biological(10), Rocky(10), Surveyed(10));

        Assert.Equal(["Leaving one body worth mapping and one with biology."], world.Jump(Jump("Hyperspace")).Left);
    }

    [Fact]
    public void BiologyAloneCountsTheBodies()
    {
        var world = new World(Biological(10), Rocky(10), Surveyed(10), Biological(11), Rocky(11), Surveyed(11));

        Assert.Equal(["Leaving two bodies with biology."], world.Jump(Jump("Hyperspace")).Left);
    }

    [Fact]
    public void ASupercruiseJumpSaysNothing()
    {
        var world = new World(Water(20), Biological(10), Rocky(10), Surveyed(10));

        Assert.Empty(world.Jump(Jump("Supercruise")).Left);
    }

    [Fact]
    public void ABodyWithOneSampleTakenIsLeftToTheUnfinishedSamplesLine()
    {
        var world = new World(Water(20), Biological(10), Rocky(10), Surveyed(10), Approach(10), Sample(10));

        var (left, unfinished) = world.Jump(Jump("Hyperspace"));

        Assert.Equal(["Leaving one body worth mapping."], left);
        Assert.Equal(["Leaving Fixture 10 with Stratum Paleas at 1 of 3."], unfinished);
    }

    [Fact]
    public void ASystemWithNothingLeftSaysNothing()
    {
        Assert.Empty(new World().Jump(Jump("Hyperspace")).Left);
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var world = new World(Water(20), Biological(10), Rocky(10), Surveyed(10));

        Assert.Empty(world.Jump(Jump("Hyperspace"), priming: true).Left);
    }
}
