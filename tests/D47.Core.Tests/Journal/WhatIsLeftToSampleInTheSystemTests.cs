using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class WhatIsLeftToSampleInTheSystemTests
{
    private const long System = 2175107336563;

    private static void Apply(GameStateStore store, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static GameStateStore InSystem(long address = System)
    {
        var store = new GameStateStore();
        Apply(store, """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(store, $$"""{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Test","SystemAddress":{{address}},"Docked":false}""");
        return store;
    }

    private static void Fss(GameStateStore store, int bodyId, string letter, int signals) =>
        Apply(store, $$"""
            {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"Test {{letter}}","BodyID":{{bodyId}},"SystemAddress":{{System}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{signals}}}]}
            """);

    private static void Mapped(GameStateStore store, int bodyId, string letter, params string[] genera)
    {
        var rows = string.Join(",", genera.Select(genus =>
            "{\"Genus\":\"$Codex_Ent_" + genus + "_Genus_Name;\",\"Genus_Localised\":\"" + genus + "\"}"));

        Apply(store, $$"""
            {"timestamp":"2026-08-16T11:00:00Z","event":"SAASignalsFound","BodyName":"Test {{letter}}","BodyID":{{bodyId}},"SystemAddress":{{System}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
             "Genuses":[{{rows}}]}
            """);
    }

    private static void Sample(GameStateStore store, int bodyId, string scanType, string genus, string species) =>
        Apply(store, $$"""
            {"timestamp":"2026-08-16T12:00:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
             "Species":"$Codex_Ent_{{genus}}_01_Name;","Species_Localised":"{{genus}} {{species}}",
             "SystemAddress":{{System}},"Body":{{bodyId}}}
            """);

    private static void Complete(GameStateStore store, int bodyId, string genus, string species)
    {
        Sample(store, bodyId, "Log", genus, species);
        Sample(store, bodyId, "Sample", genus, species);
        Sample(store, bodyId, "Sample", genus, species);
    }

    private static CapabilityRegistry Registry(GameStateStore store) =>
        CapabilityRegistry.Build(
            [ExobiologyCapability.Create(null, () => store.Active, TestSurface.For(new MemoryInstall()).Settings)]);

    private static async Task<string> Ask(GameStateStore store)
    {
        var result = await Registry(store).InvokeAsync(
            "get_biology_left_in_system", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Fact]
    public async Task ASystemWithNoScanOnRecordIsNotSaidToHaveNothingLeft()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");

        var elsewhere = InSystem(address: 12345);
        Mapped(elsewhere, 3, "a", "Bacterium");

        Assert.Equal(
            "I have no scan of this system on record, so I cannot say what is left to sample.",
            await Ask(InSystem()));
    }

    [Fact]
    public async Task AnUnsampledGenusIsLeftAndAnOpenSetIsSaidWithItsCount()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium", "Stratum");
        Sample(store, 3, "Log", "Bacterium", "Tela");
        Sample(store, 3, "Sample", "Bacterium", "Tela");

        Assert.Equal("Left to sample: Bacterium Tela, 2 of 3 on a; Stratum on a.", await Ask(store));
    }

    [Fact]
    public async Task AGenusWithACompletedSetIsNotLeft()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium", "Stratum");
        Complete(store, 3, "Bacterium", "Tela");

        Assert.Equal("Left to sample: Stratum on a.", await Ask(store));
    }

    [Fact]
    public async Task AllButThreeGeneraAreCountedByBody()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium", "Stratum", "Fungoida");
        Mapped(store, 4, "b", "Tussock", "Osseus");
        Mapped(store, 5, "c", "Aleoida", "Frutexa");

        var said = await Ask(store);

        Assert.EndsWith(", and 4 more on 2 bodies.", said, StringComparison.Ordinal);
        Assert.Equal(3, said.Count(character => character == ';') + 1);
    }

    [Fact]
    public async Task OpenSetsAreNamedBeforeGeneraNotStarted()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium", "Stratum", "Fungoida");
        Mapped(store, 4, "b", "Tussock");
        Sample(store, 4, "Log", "Tussock", "Pennata");

        Assert.StartsWith("Left to sample: Tussock Pennata, 1 of 3 on b;", await Ask(store), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABodyWithSignalsAndNoSurfaceScanIsNotMapped()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");
        Fss(store, 4, "b", 2);

        Assert.Equal(
            "Left to sample: Bacterium on a. 1 body is not mapped: b with 2 biological signals.",
            await Ask(store));
    }

    [Fact]
    public async Task WhenEveryNamedGenusIsDoneItSaysSoAndCountsTheUnmappedBodies()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");
        Complete(store, 3, "Bacterium", "Tela");
        Fss(store, 4, "b", 1);

        Assert.Equal(
            "Everything mapped in this system is sampled. 1 body is not mapped: b with 1 biological signal.",
            await Ask(store));
    }

    [Fact]
    public async Task ASurfaceScanReplacesTheFssRowSoTheBodyIsNoLongerUnmapped()
    {
        var store = InSystem();
        Fss(store, 3, "a", 1);
        Mapped(store, 3, "a", "Bacterium");

        Assert.Equal("Left to sample: Bacterium on a.", await Ask(store));
    }

    [Fact]
    public void BothPhrasesReachTheToolWithoutTheModel()
    {
        var router = new KeywordRouter(Registry(InSystem()));

        foreach (var phrase in new[] { "what's left to sample", "what is left to sample" })
        {
            Assert.Equal("get_biology_left_in_system", router.Match(phrase)?.ToolName);
        }
    }

    private sealed class FakeSpansh(Func<SystemBiology> answer) : StubGalaxyService
    {
        public int Asked { get; private set; }

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult(answer());
        }
    }

    private static SurveyedBody Body(string letter, int bodyId, params ExobiologySpecies[] species) =>
        new("Test " + letter, bodyId, 0, species);

    private static SystemBiology Report(params SurveyedBody[] bodies) => new(System, bodies);

    private static async Task<string> AskWith(GameStateStore store, IGalaxyService galaxy, SurveyedBiology? held = null)
    {
        var registry = CapabilityRegistry.Build(
        [
            ExobiologyCapability.Create(
                null, () => store.Active, TestSurface.For(new MemoryInstall()).Settings,
                galaxy: galaxy, surveyed: held ?? new SurveyedBiology()),
        ]);

        var result = await registry.InvokeAsync(
            "get_biology_left_in_system", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        return result.Content;
    }

    [Fact]
    public async Task ABodyOnlySpanshReportsIsLeftToSampleBySpeciesWithItsValue()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");
        var spansh = new FakeSpansh(() => Report(Body("e", 7, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800))));

        var answer = await AskWith(store, spansh);

        Assert.Contains("Stratum Cucumisis on e, 16.2 million", answer);
        Assert.Contains("Bacterium on a", answer);
    }

    [Fact]
    public async Task AnOpenSetComesFirstThenTheHigherReportedValue()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");
        Sample(store, 3, "Log", "Bacterium", "Tela");
        var spansh = new FakeSpansh(() => Report(
            Body("e", 7, new ExobiologySpecies("Aleoida", "Aleoida Arcus", 1, 7_000_000)),
            Body("f", 8, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 16_000_000))));

        var answer = await AskWith(store, spansh);

        var set = answer.IndexOf("Bacterium Tela, 1 of 3 on a", StringComparison.Ordinal);
        var high = answer.IndexOf("Stratum Cucumisis on f", StringComparison.Ordinal);
        var low = answer.IndexOf("Aleoida Arcus on e", StringComparison.Ordinal);

        Assert.True(set >= 0 && set < high && high < low, answer);
    }

    [Fact]
    public async Task ABodyWithSignalsSpanshReportsIsNotSaidAsNotMapped()
    {
        var store = InSystem();
        Fss(store, 7, "e", 1);
        var spansh = new FakeSpansh(() => Report(Body("e", 7, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 16_000_000))));

        var answer = await AskWith(store, spansh);

        Assert.DoesNotContain("not mapped", answer);
        Assert.Contains("Stratum Cucumisis on e", answer);
    }

    [Fact]
    public async Task ASystemWithNoCommanderRecordAnswersFromTheSpanshReport()
    {
        var spansh = new FakeSpansh(() => Report(Body("e", 7, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 16_000_000))));

        var answer = await AskWith(InSystem(), spansh);

        Assert.StartsWith("Left to sample: Stratum Cucumisis on e", answer);
    }

    [Fact]
    public async Task AHeldReportIsNotAskedForAgain()
    {
        var held = new SurveyedBiology();
        held.Set(Report(Body("e", 7, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 16_000_000))));
        var spansh = new FakeSpansh(() => throw new InvalidOperationException());

        var answer = await AskWith(InSystem(), spansh, held);

        Assert.Equal(0, spansh.Asked);
        Assert.Contains("Stratum Cucumisis on e", answer);
    }

    [Fact]
    public async Task AFailedAskAnswersFromTheCommandersRecordsAlone()
    {
        var store = InSystem();
        Mapped(store, 3, "a", "Bacterium");
        var spansh = new FakeSpansh(() => throw new HttpRequestException());

        Assert.Equal("Left to sample: Bacterium on a.", await AskWith(store, spansh));
    }
}
