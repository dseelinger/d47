using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheGeologyAnswerNamesWhatABodyLikelyHasTests
{
    private const long WaterIceFumarole = 1400158;
    private const long WaterIceGeyser = 1400258;

    private static readonly int Here = GalacticRegions.IdOf(GalacticRegions.Find(StarPosition.Origin)!)!.Value;

    private static GameStateStore Store(bool located, params string[] events)
    {
        var store = new GameStateStore();
        var lines = new List<string>
        {
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-08-16T09:00:01Z","event":"LoadGame","Credits":1}""",
        };

        if (located)
        {
            lines.Add(
                """{"timestamp":"2026-08-16T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000300,"StarPos":[0,0,0]}""");
        }
        else
        {
            lines.Add(
                """{"timestamp":"2026-08-16T09:00:02Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000300}""");
        }

        foreach (var json in lines.Concat(events))
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static string Logged(long entryId, int region) => $$"""
        {"timestamp":"2026-08-10T09:00:00Z","event":"CodexEntry","EntryID":{{entryId}},"Name":"$Codex_Ent_{{entryId}};",
         "Region":"$Codex_RegionName_{{region}};","System":"Elsewhere","SystemAddress":5,"BodyID":1}
        """;

    private static string Signals(int bodyId, int geological, string time = "2026-08-16T10:00:00Z") => $$"""
        {"timestamp":"{{time}}","event":"FSSBodySignals","BodyName":"Fixture B {{bodyId}}",
         "SystemAddress":1000300,"BodyID":{{bodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Geological;","Type_Localised":"Geological","Count":{{geological}}}]}
        """;

    private static string Scan(int bodyId, string volcanism = "major water geysers volcanism") => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture",
         "SystemAddress":1000300,"BodyID":{{bodyId}},"BodyName":"Fixture B {{bodyId}}",
         "PlanetClass":"Icy body","Atmosphere":"","Volcanism":"{{volcanism}}","Landable":true}
        """;

    private static async Task<string> Ask(GameStateStore store, string? json = null)
    {
        var capability = JournalCapability.Create(store, () => HistoryState.Done);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_body_geology", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    [Fact]
    public async Task EveryLikelyFeatureIsMarkedLoggedOrNotLoggedForTheRegion()
    {
        var store = Store(true, Logged(WaterIceGeyser, Here), Signals(3, 3), Scan(3));

        var spoken = await Ask(store);

        Assert.StartsWith(
            "B 3 has 3 geological signals. Likely: water ice fumaroles, not logged in this region, "
            + "and water ice geysers, logged in this region. The record runs on record since ",
            spoken);
        Assert.EndsWith(", so an entry logged before then reads as not logged.", spoken);
    }

    [Fact]
    public async Task AFeatureLoggedInAnotherRegionReadsAsNotLogged()
    {
        var store = Store(true, Logged(WaterIceGeyser, Here + 1), Signals(3, 1), Scan(3));

        var spoken = await Ask(store, """{"body":"B 3"}""");

        Assert.Contains("1 geological signal. Likely:", spoken);
        Assert.Contains("water ice geysers, not logged in this region", spoken);
    }

    [Fact]
    public async Task WithNoBodyGivenTheMostRecentOneWithGeologyIsUsed()
    {
        var store = Store(
            true,
            Signals(3, 1, "2026-08-16T10:00:00Z"),
            Scan(3),
            Signals(4, 2, "2026-08-16T11:00:00Z"),
            Scan(4));

        Assert.StartsWith("B 4 has 2 geological signals.", await Ask(store));
    }

    [Fact]
    public async Task ABodyWithNoGeologicalSignalsSaysSo()
    {
        var store = Store(true, Signals(3, 0), Scan(3));

        Assert.Equal("B 3 shows no geological signals.", await Ask(store, """{"body":"B 3"}"""));
    }

    [Fact]
    public async Task AClassWithNoFeatureInTheTableSaysSo()
    {
        var store = Store(true, Signals(3, 2), Scan(3, volcanism: "none"));

        Assert.Equal(
            "B 3 has 2 geological signals, but the geology table has no feature for its class and volcanism.",
            await Ask(store, """{"body":"B 3"}"""));
    }

    [Fact]
    public async Task ABodyWithSignalsButNoScanSaysItCannotSay()
    {
        var store = Store(true, Signals(3, 2));

        Assert.Equal(
            "I have no scan of B 3, so I cannot say which geology it likely has.",
            await Ask(store, """{"body":"B 3"}"""));
    }

    [Fact]
    public async Task AnUnknownRegionLeavesOutLoggedAndTheRecordSentence()
    {
        var store = Store(false, Signals(3, 3), Scan(3));

        Assert.Equal(
            "B 3 has 3 geological signals. Likely: water ice fumaroles and water ice geysers.",
            await Ask(store));
    }

    [Fact]
    public void TheToolIsOpenToTheModel()
    {
        var tool = Assert.Single(
            JournalCapability.Create(new GameStateStore(), () => HistoryState.Done).Tools,
            tool => tool.Name == "get_body_geology");

        Assert.False(tool.Protected);
    }
}
