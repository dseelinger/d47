using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using D47.Core.Storage;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheBodyAnswerNamesWhatOtherCommandersReportedTests
{
    private const long System = 1;

    private sealed class FakeSpansh(Func<SystemBiology> answer) : StubGalaxyService
    {
        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(answer());
    }

    private const string Located =
        """{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Test","SystemAddress":1,"Docked":false}""";

    private static string Conditions(int bodyId, string name) => $$"""
        {"timestamp":"2026-08-16T10:01:00Z","event":"Scan","ScanType":"Detailed",
         "SystemAddress":1,"BodyID":{{bodyId}},"BodyName":"{{name}}",
         "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
         "SurfaceGravity":4.903325,"SurfaceTemperature":200,"SurfacePressure":2026.5,"Landable":true}
        """;

    private static string Mapped(int bodyId, string name, params string[] genera) => $$"""
        {"timestamp":"2026-08-16T10:02:00Z","event":"SAASignalsFound","BodyName":"{{name}}",
         "SystemAddress":1,"BodyID":{{bodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
         "Genuses":[{{string.Join(",", genera.Select(genus => "{\"Genus\":\"$Codex_Ent_" + genus + "_Genus_Name;\",\"Genus_Localised\":\"" + genus + "\"}"))}}]}
        """;

    private static string Fss(int bodyId, string name, int signals) => $$"""
        {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"{{name}}","BodyID":{{bodyId}},"SystemAddress":1,
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{signals}}}]}
        """;

    private static string Sampled(int bodyId, string genus, string species, string scanType) => $$"""
        {"timestamp":"2026-08-16T12:00:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
         "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
         "Species":"$Codex_Ent_{{genus}}_01_Name;","Species_Localised":"{{genus}} {{species}}",
         "SystemAddress":1,"Body":{{bodyId}}}
        """;

    private static SurveyedBody Reported(string name, int bodyId, long total, params ExobiologySpecies[] species) =>
        new(name, bodyId, total, species);

    private static async Task<string> Ask(Func<SystemBiology>? spansh, string json, params string[] lines)
    {
        var store = JournalScript.Store(
        [
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            Located,
            .. lines,
        ]);
        var install = new MemoryInstall();
        var registry = CapabilityRegistry.Build(
        [
            ExobiologyCapability.Create(
                null, () => store.Active, TestSurface.For(install).Settings,
                galaxy: spansh is null ? null : new FakeSpansh(spansh), surveyed: new SurveyedBiology()),
        ]);

        var result = await registry.InvokeAsync(
            "get_body_biology", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        return result.Content;
    }

    private static readonly ExobiologySpecies Stratum = new("Stratum", "Stratum Cucumisis", 2, 16_202_800);

    [Fact]
    public async Task AReportedSpeciesIsNamedWithItsValueAndNoCandidatesForThatGenus()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 3 b", 23, 0, Stratum)]),
            """{"body":"3 b"}""",
            Conditions(23, "Test 3 b"),
            Mapped(23, "Test 3 b", "Stratum"));

        Assert.Contains("Stratum: Stratum Cucumisis 16,202,800 ×2, reported by other Commanders.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Stratum Aureum", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGenusNobodyReportedKeepsItsCandidatesAndThePredictionSentence()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 3 b", 23, 0, Stratum)]),
            """{"body":"3 b"}""",
            Conditions(23, "Test 3 b"),
            Mapped(23, "Test 3 b", "Stratum", "Bacterium"));

        Assert.Contains("Stratum Cucumisis 16,202,800 ×2, reported by other Commanders.", said, StringComparison.Ordinal);
        Assert.Contains("Bacterium: Bacterium ", said, StringComparison.Ordinal);
        Assert.Contains("the sample decides", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithEveryGenusReportedThePredictionSentenceIsNotSaid()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 3 b", 23, 0, Stratum)]),
            """{"body":"3 b"}""",
            Conditions(23, "Test 3 b"),
            Mapped(23, "Test 3 b", "Stratum"));

        Assert.DoesNotContain("prediction", said, StringComparison.Ordinal);
        Assert.DoesNotContain("the sample decides", said, StringComparison.Ordinal);
        Assert.Contains("That could be worth 16,202,800 to 16,202,800 credits.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnFssOnlyBodySpanshReportsNamesTheGeneraAndSpecies()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 4", 24, 0, Stratum)]),
            """{"body":"4"}""",
            Fss(24, "Test 4", 1));

        Assert.DoesNotContain("I do not know the genus.", said, StringComparison.Ordinal);
        Assert.Contains("Stratum: Stratum Cucumisis 16,202,800 ×2, reported by other Commanders.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABodyOnlySpanshReportsIsAnsweredFromTheReportByItsShortName()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 5 c", 25, 20_000_000, Stratum)]),
            """{"body":"5 c"}""",
            Conditions(23, "Test 3 b"),
            Mapped(23, "Test 3 b", "Stratum"));

        Assert.StartsWith("Test 5 c, reported by other Commanders, not scanned by you.", said, StringComparison.Ordinal);
        Assert.Contains("Stratum Cucumisis 16,202,800 ×2", said, StringComparison.Ordinal);
        Assert.EndsWith("That is worth 20,000,000 credits together.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASampledGenusNamesTheSampledSpeciesNotTheReportedOne()
    {
        var said = await Ask(
            () => new SystemBiology(System, [Reported("Test 3 b", 23, 0, Stratum)]),
            """{"body":"3 b"}""",
            Conditions(23, "Test 3 b"),
            Mapped(23, "Test 3 b", "Stratum"),
            Sampled(23, "Stratum", "Tectonicas", "Log"));

        Assert.Contains("Stratum Tectonicas", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Cucumisis", said, StringComparison.Ordinal);
        Assert.DoesNotContain("reported by other Commanders", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedAskAnswersAsItDidWithoutAReport()
    {
        var lines = new[] { Conditions(23, "Test 3 b"), Mapped(23, "Test 3 b", "Stratum") };

        var failed = await Ask(() => throw new HttpRequestException(), """{"body":"3 b"}""", lines);
        var none = await Ask(null, """{"body":"3 b"}""", lines);

        Assert.Equal(none, failed);
        Assert.Contains("Stratum: Stratum Cucumisis 16,202,800", failed, StringComparison.Ordinal);
        Assert.Contains("the sample decides", failed, StringComparison.Ordinal);
    }
}
