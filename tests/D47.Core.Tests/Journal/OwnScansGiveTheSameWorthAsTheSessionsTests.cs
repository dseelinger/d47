using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <see cref="SystemWorth"/> lists the same items from a system's <see cref="OwnSystem"/>, as an earlier session leaves
/// it, as from the session's own scans of the same lines.
/// </summary>
public sealed class OwnScansGiveTheSameWorthAsTheSessionsTests
{
    private const long Here = 1000300;

    private const long Threshold = 500_000;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Signals(int bodyId, string type, int count) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_{{type}};","Type_Localised":"{{type}}","Count":{{count}}}]}
        """;

    private static string Surveyed(int bodyId, string genus) => $$"""
        {"timestamp":"2026-11-20T10:30:00Z","event":"SAASignalsFound","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}],
         "Genuses":[{"Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}"}]}
        """;

    private static IEnumerable<string> Completed(int bodyId, string genus) =>
        new[] { "Log", "Sample", "Sample" }.Select((scanType, i) => $$"""
            {"timestamp":"2026-11-20T11:0{{i}}:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
             "Species":"$Codex_Ent_{{genus}}_01_Name;","Species_Localised":"{{genus}} Tela",
             "SystemAddress":{{Here}},"Body":{{bodyId}}}
            """);

    private static string Rocky(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere",
         "Volcanism":"","SurfaceGravity":1.9,"SurfaceTemperature":175.0,"SurfacePressure":1200.0,"Landable":true,"MassEM":0.02,
         "TerraformState":"","WasDiscovered":true,"WasMapped":true}
        """;

    private static string Water(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Water world","Atmosphere":"","Landable":false,
         "MassEM":1.0,"TerraformState":"","WasDiscovered":true,"WasMapped":false}
        """;

    private static string Icy(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Icy body","Atmosphere":"",
         "Volcanism":"major water geysers volcanism","Landable":true,"WasDiscovered":true,"WasMapped":true}
        """;

    private static readonly string[] Lines =
    [
        """{"timestamp":"2026-11-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
        """{"timestamp":"2026-11-20T09:00:01Z","event":"LoadGame","FID":"F1","Commander":"Fixture","Credits":1}""",
        $$"""{"timestamp":"2026-11-20T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":{{Here}},"StarPos":[0,0,0]}""",
        Water(2),
        Icy(3),
        Signals(3, "Geological", 1),
        Rocky(4),
        Signals(4, "Biological", 2),
        Surveyed(4, "Bacterium"),
        Rocky(5),
        Signals(5, "Guardian", 2),
        Rocky(6),
        Surveyed(6, "Stratum"),
        .. Completed(6, "Stratum"),
    ];

    private static string Shown(WorthItem item) =>
        $"{item.Kind} {item.BodyName} {item.BodyId} [{string.Join(",", item.Names)}] {item.Low} {item.High} {item.FirstMapping}";

    [Fact]
    public void ASystemFromAnEarlierSessionListsWhatTheSameScansListThisSession()
    {
        var events = Lines.Select(Parse).ToList();
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);
        ledger.Apply(events);

        var session = new GameStateStore();

        foreach (var journalEvent in events)
        {
            session.Apply(journalEvent);
        }

        var now = session.Active!;
        now.CodexLedger = now.CodexLedger with { IsWhole = true };

        var later = new GameStateStore();

        foreach (var journalEvent in events.Take(3))
        {
            later.Apply(journalEvent);
        }

        var earlier = later.Active!;
        earlier.Biology = now.Biology;
        earlier.Sampling = now.Sampling;
        earlier.CodexLedger = now.CodexLedger;

        var own = OwnSystemFold.Fold(Here, [.. events.Where(journalEvent => journalEvent.Long("SystemAddress") == Here && journalEvent.Kind != "ScanOrganic")]);

        var expected = SystemWorth.For(now, Here, StarPosition.Origin, ledger, Threshold, []);
        var actual = SystemWorth.For(earlier, Here, StarPosition.Origin, ledger, Threshold, [], own);

        Assert.Empty(earlier.Scans.InSystem(Here));
        Assert.Equal(
            [WorthKind.Unusual, WorthKind.Biology, WorthKind.Mapping, WorthKind.Geology],
            expected.Items.Select(item => item.Kind));
        Assert.Equal(expected.Items.Select(Shown), actual.Items.Select(Shown));
        Assert.Equal(expected.IsKnown, actual.IsKnown);
        Assert.Equal(expected.FirstDiscoveries, actual.FirstDiscoveries);
    }
}
