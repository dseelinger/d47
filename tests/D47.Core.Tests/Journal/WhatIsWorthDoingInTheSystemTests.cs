using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class WhatIsWorthDoingInTheSystemTests
{
    private const long Here = 1000300;

    private const long Threshold = 500_000;

    private sealed class World
    {
        public GameStateStore Store { get; } = new();

        public CartographyLedger Ledger { get; } = new(null, new MemoryFileSystem(), NullLogger.Instance);

        public SignalSightings Signals { get; } = new(new MemoryFileSystem(), NullLogger.Instance);

        public World(params string[] lines)
        {
            Ledger.FoldHistory([], TestContext.Current.CancellationToken);

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
                Store.Apply(journalEvent);
            }

            Ledger.Apply(events);
            Signals.Apply(events);
            Store.Active!.CodexLedger = Store.Active.CodexLedger with { IsWhole = true };
        }

        public SystemWorthAnswer Worth() =>
            SystemWorth.For(Store.Active!, Here, StarPosition.Origin, Ledger, Threshold, Signals.Snapshot());

        public async Task<string> Ask()
        {
            var registry = CapabilityRegistry.Build(
                [JournalCapability.Create(Store, cartography: Ledger, mappingThreshold: () => Threshold, signals: Signals)]);
            var result = await registry.InvokeAsync(
                JournalCapability.SystemWorthTool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

            Assert.False(result.IsError);

            return result.Content;
        }
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Biological(int bodyId, int count) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{count}}}]}
        """;

    private static string Guardian(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Guardian;","Type_Localised":"Guardian","Count":2}]}
        """;

    private static string Geological(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture {{bodyId}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
         "Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":1}]}
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

    private static string Water(int bodyId, double mass, bool wasMapped = true, bool wasDiscovered = true) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Water world","Atmosphere":"","Landable":false,
         "MassEM":{{mass}},"TerraformState":"",
         "WasDiscovered":{{(wasDiscovered ? "true" : "false")}},"WasMapped":{{(wasMapped ? "true" : "false")}}}
        """;

    private static string Icy(int bodyId, bool wasDiscovered = true) => $$"""
        {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"BodyName":"Fixture {{bodyId}}","PlanetClass":"Icy body","Atmosphere":"",
         "Volcanism":"major water geysers volcanism","Landable":true,
         "WasDiscovered":{{(wasDiscovered ? "true" : "false")}},"WasMapped":true}
        """;

    private static string MappedByMe(int bodyId) => $$"""
        {"timestamp":"2026-11-20T10:40:00Z","event":"SAAScanComplete","BodyName":"Fixture {{bodyId}}","SystemAddress":{{Here}},
         "BodyID":{{bodyId}},"ProbesUsed":4,"EfficiencyTarget":6}
        """;

    private static readonly string NotableStellarPhenomena = $$"""
        {"timestamp":"2026-11-20T09:30:00Z","event":"FSSSignalDiscovered","SystemAddress":{{Here}},
         "SignalName":"$Fixed_Event_Life_Cloud;","SignalType":"Codex"}
        """;

    private const string WithoutScanner = """
        {"timestamp":"2026-11-20T09:00:03Z","event":"Loadout","Ship":"diamondbackxl","ShipID":5,"Modules":[
         {"Slot":"PowerPlant","Item":"int_powerplant_size4_class2"},
         {"Slot":"Slot05_Size2","Item":"int_fuelscoop_size2_class5"}]}
        """;

    [Fact]
    public void ABiologyBodyBelowTheBiologyThresholdIsListed()
    {
        var item = Assert.Single(new World(Biological(10, 1), Rocky(10), Surveyed(10, "Bacterium")).Worth().Items);

        Assert.Equal(WorthKind.Biology, item.Kind);
        Assert.Equal("Fixture 10", item.BodyName);
        Assert.Equal(["Bacterium"], item.Names);
        Assert.True(item.High is > 0 and < 10_000_000, $"High {item.High}");
    }

    [Fact]
    public void ABodyWithEveryGenusDoneIsNotListed()
    {
        var world = new World([Biological(10, 1), Rocky(10), Surveyed(10, "Bacterium"), .. Completed(10, "Bacterium")]);

        Assert.DoesNotContain(world.Worth().Items, item => item.Kind == WorthKind.Biology);
    }

    [Fact]
    public void APlanetTheCommanderMappedIsNotListedAndOneNoOneMappedIsAFirstMapping()
    {
        var items = new World(Water(20, 0.5), MappedByMe(20), Water(30, 0.5, wasMapped: false)).Worth().Items;

        var item = Assert.Single(items, item => item.Kind == WorthKind.Mapping);
        Assert.Equal("Fixture 30", item.BodyName);
        Assert.True(item.FirstMapping);
    }

    [Fact]
    public void APlanetSomeoneElseMappedIsNotAFirstMapping()
    {
        var item = Assert.Single(new World(Water(20, 0.5)).Worth().Items);

        Assert.Equal(WorthKind.Mapping, item.Kind);
        Assert.False(item.FirstMapping);
        Assert.True(item.High >= Threshold);
    }

    [Fact]
    public void NoPlanetIsListedForMappingWithoutASurfaceScanner()
    {
        var items = new World(WithoutScanner, Water(30, 0.5, wasMapped: false)).Worth().Items;

        Assert.DoesNotContain(items, item => item.Kind == WorthKind.Mapping);
    }

    [Fact]
    public void ANotableStellarPhenomenonAndAGuardianBodyAreUnusual()
    {
        var items = new World(NotableStellarPhenomena, Guardian(40)).Worth().Items;

        Assert.Collection(
            items,
            item => Assert.Equal((WorthKind.Unusual, (int?)null), (item.Kind, item.BodyId)),
            item =>
            {
                Assert.Equal((WorthKind.Unusual, (int?)40), (item.Kind, item.BodyId));
                Assert.Equal(["Guardian"], item.Names);
            });
    }

    [Fact]
    public void FirstDiscoveriesAreACountOfStarsAndPlanets()
    {
        var star = $$"""
            {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"AutoScan","StarSystem":"Fixture","SystemAddress":{{Here}},
             "BodyID":0,"BodyName":"Fixture","StarType":"K","DistanceFromArrivalLS":0.0,"WasDiscovered":false,"WasMapped":false}
            """;
        var cluster = $$"""
            {"timestamp":"2026-11-20T10:01:00Z","event":"Scan","ScanType":"AutoScan","StarSystem":"Fixture","SystemAddress":{{Here}},
             "BodyID":2,"BodyName":"Fixture A Belt Cluster 1","WasDiscovered":false,"WasMapped":false}
            """;

        var answer = new World(star, cluster, Icy(30, wasDiscovered: false)).Worth();

        Assert.Equal(2, answer.FirstDiscoveries);
        Assert.Empty(answer.Items);
    }

    [Fact]
    public void UnusualSignalsComeFirstThenBiologyByValueThenMappingByValueThenGeology()
    {
        var world = new World(
            Icy(50), Geological(50),
            Water(20, 0.3), Water(30, 0.9),
            Biological(10, 1), Rocky(10), Biological(11, 3), Rocky(11),
            Guardian(40), NotableStellarPhenomena);

        var items = world.Worth().Items;

        Assert.Equal(
            [
                (WorthKind.Unusual, "Notable stellar phenomena"),
                (WorthKind.Unusual, "Fixture 40"),
                (WorthKind.Biology, "Fixture 11"),
                (WorthKind.Biology, "Fixture 10"),
                (WorthKind.Mapping, "Fixture 30"),
                (WorthKind.Mapping, "Fixture 20"),
                (WorthKind.Geology, "Fixture 50"),
            ],
            items.Select(item => (item.Kind, item.BodyName)));
    }

    [Fact]
    public async Task TheAnswerNamesThreeItemsCountsTheRestAndEndsWithTheFirstDiscoveries()
    {
        var world = new World(
            Icy(50), Geological(50), Water(30, 0.9, wasMapped: false, wasDiscovered: false),
            Biological(10, 1), Rocky(10), Guardian(40), NotableStellarPhenomena);

        var said = await world.Ask();

        Assert.StartsWith("Worth doing here: notable stellar phenomena; Guardian signals on 40; biology on 10", said, StringComparison.Ordinal);
        Assert.EndsWith("; and 2 more. 1 body you scanned here was undiscovered.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASystemWithNoScanOnRecordIsNotSaidToHaveNothingLeft()
    {
        Assert.Equal(
            "I have no scan of this system on record, so I cannot say what is left in it.",
            await new World().Ask());
    }

    [Fact]
    public void ThePhrasesReachTheToolWithoutTheModel()
    {
        var router = new KeywordRouter(CapabilityRegistry.Build([JournalCapability.Create(new GameStateStore())]));

        foreach (var phrase in new[]
        {
            "what's worth doing here", "what is worth doing in this system", "what's left in this system",
            "what is left in this system",
        })
        {
            Assert.Equal(JournalCapability.SystemWorthTool, router.Match(phrase)?.ToolName);
        }
    }
}
