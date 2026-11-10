using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AnUnnamedBodyDefaultsToTheTargetedOneTests
{
    private const string System = "Fixture";
    private const long Address = 1000300;

    private sealed class FakeCommunity : ICommunityBiology
    {
        public Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EarlierFind>>(
            [
                new($"{System} B 3", 1, "Bacterium Aurasus - Green", 10, 20),
                new($"{System} B 4", 1, "Fungoida Setisis - Emerald", 10, 20),
            ]);
    }

    private static string Signals(int bodyId, string time) => $$"""
        {"timestamp":"{{time}}","event":"SAASignalsFound","BodyName":"{{System}} B {{bodyId}}",
         "SystemAddress":{{Address}},"BodyID":{{bodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":2},
                    {"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":3}],
         "Genuses":[{"Genus":"$Codex_Ent_Bacterial_Genus_Name;","Genus_Localised":"Bacterium"}]}
        """;

    private static string Scan(int bodyId) => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"{{System}}",
         "SystemAddress":{{Address}},"BodyID":{{bodyId}},"BodyName":"{{System}} B {{bodyId}}",
         "PlanetClass":"Icy body","Atmosphere":"","Volcanism":"major water geysers volcanism","Landable":true}
        """;

    private static CapabilityRegistry Registry(string? at, GameStatus status)
    {
        var gameState = new GameStateStore();
        var lines = new List<string>
        {
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            $$"""{"timestamp":"2026-08-16T09:00:01Z","event":"LoadGame","Credits":1}""",
            $$"""{"timestamp":"2026-08-16T09:00:02Z","event":"Location","StarSystem":"{{System}}","SystemAddress":{{Address}},"StarPos":[0,0,0],"Docked":false}""",
            Signals(3, "2026-08-16T10:00:00Z"),
            Scan(3),
            Signals(4, "2026-08-16T10:05:00Z"),
            Scan(4),
        };

        if (at is not null)
        {
            lines.Add(
                $$"""{"timestamp":"2026-08-16T10:06:00Z","event":"ApproachBody","StarSystem":"{{System}}","SystemAddress":{{Address}},"Body":"{{at}}","BodyID":3}""");
        }

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var settings = TestSurface.For(new MemoryInstall()).Settings;

        return CapabilityRegistry.Build(
        [
            ExobiologyCapability.Create(
                null, () => gameState.Active, settings, () => status, community: new FakeCommunity(), earlier: new EarlierFinds()),
            JournalCapability.Create(gameState, () => HistoryState.Done, liveStatus: () => status),
        ]);
    }

    private static GameStatus Targeting(long body) =>
        new() { Destination = new StatusDestination(Address, body, "x") };

    private static async Task<string> Ask(CapabilityRegistry registry, string tool) =>
        (await registry.InvokeAsync(tool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken)).Content;

    [Fact]
    public async Task BiologyAnswersForTheTargetedBodyOverTheMoreRecentScan()
    {
        var answer = await Ask(Registry(null, Targeting(3)), "get_body_biology");

        Assert.Contains("B 3", answer);
        Assert.DoesNotContain("B 4", answer);
    }

    [Fact]
    public async Task GeologyAnswersForTheTargetedBodyOverTheMoreRecentScan()
    {
        var answer = await Ask(Registry(null, Targeting(3)), "get_body_geology");

        Assert.StartsWith("B 3 has", answer);
    }

    [Fact]
    public async Task WithNothingTargetedBiologyAndGeologyUseTheMostRecentScan()
    {
        var registry = Registry(null, new GameStatus());

        Assert.Contains("B 4", await Ask(registry, "get_body_biology"));
        Assert.StartsWith("B 4 has", await Ask(registry, "get_body_geology"));
    }

    [Fact]
    public async Task ATargetThatIsTheSystemDoesNotChangeTheChoice()
    {
        var registry = Registry(null, Targeting(0));

        Assert.StartsWith("B 4 has", await Ask(registry, "get_body_geology"));
    }

    [Fact]
    public async Task EarlierFindsUseTheBodyTheCommanderIsOnOverTheTargetedOne()
    {
        var answer = await Ask(Registry($"{System} B 3", Targeting(4)), ExobiologyCapability.EarlierFindsTool);

        Assert.Contains("B 3", answer);
        Assert.Contains("Bacterium Aurasus", answer);
    }

    [Fact]
    public async Task EarlierFindsInSpaceUseTheTargetedBody()
    {
        var answer = await Ask(Registry(null, Targeting(3)), ExobiologyCapability.EarlierFindsTool);

        Assert.Contains("B 3", answer);
        Assert.Contains("Bacterium Aurasus", answer);
    }

    [Fact]
    public async Task EarlierFindsWithNothingTargetedUseTheMostRecentScan()
    {
        var answer = await Ask(Registry(null, new GameStatus()), ExobiologyCapability.EarlierFindsTool);

        Assert.Contains("B 4", answer);
    }
}
