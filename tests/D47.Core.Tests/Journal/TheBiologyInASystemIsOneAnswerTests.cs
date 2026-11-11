using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheBiologyInASystemIsOneAnswerTests
{
    private const long Here = 7_700_100;

    private const long Elsewhere = 7_700_200;

    private const string Region = "Arcadian Stream";

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static int _second;

    private static string Time() =>
        Start.AddSeconds(Interlocked.Increment(ref _second)).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    private static void Apply(GameStateStore store, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static GameStateStore Store(long address = Here)
    {
        var store = new GameStateStore();
        Apply(store, """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F7","Name":"Orrin Vask"}""");
        Apply(store, $$"""{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Vask Reach","SystemAddress":{{address}},"Docked":false}""");
        return store;
    }

    private static CommanderGameState State(GameStateStore store) => store.Active!;

    private static string BodyName(int bodyId) => $"Vask Reach {bodyId}";

    private static void Scan(GameStateStore store, int bodyId) =>
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"Scan","ScanType":"Detailed","BodyName":"{{BodyName(bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
             "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
             "SurfaceGravity":2.5,"SurfaceTemperature":200.0,"SurfacePressure":2000.0}
            """);

    private static void Fss(GameStateStore store, int bodyId, int signals) =>
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"FSSBodySignals","BodyName":"{{BodyName(bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{signals}}}]}
            """);

    private static void Mapped(GameStateStore store, int bodyId, params string[] genera)
    {
        var rows = string.Join(",", genera.Select(genus =>
            "{\"Genus\":\"$Codex_Ent_" + genus + "_Genus_Name;\",\"Genus_Localised\":\"" + genus + "\"}"));

        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"SAASignalsFound","BodyName":"{{BodyName(bodyId)}}","BodyID":{{bodyId}},"SystemAddress":{{Here}},
             "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
             "Genuses":[{{rows}}]}
            """);
    }

    private static void Organic(GameStateStore store, int bodyId, string scanType, string species, string? variant = null)
    {
        var genus = species.Split(' ')[0];
        var variantFields = variant is null ? "" : $",\"Variant\":\"$Codex_Ent_{genus}_Variant;\",\"Variant_Localised\":\"{variant}\"";

        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
             "Species":"$Codex_Ent_{{genus}}_Name;","Species_Localised":"{{species}}"{{variantFields}},
             "SystemAddress":{{Here}},"Body":{{bodyId}}}
            """);
    }

    private static void Taken(GameStateStore store, int bodyId, string species, int count, string? variant = null)
    {
        for (var i = 0; i < count; i++)
        {
            Organic(store, bodyId, i == 0 ? "Log" : "Sample", species, variant);
        }
    }

    private static SystemBiologyAnswer Ask(
        GameStateStore store,
        string? region = null,
        SystemBiology? surveyed = null,
        IReadOnlyList<EarlierFind>? earlier = null,
        long address = Here) =>
        BiologyInSystem.For(State(store), address, region, surveyed, earlier);

    private static GenusLine Genus(SystemBiologyAnswer answer, int bodyId, string genus) =>
        Assert.Single(Assert.Single(answer.Bodies, body => body.BodyId == bodyId).Genera, line => line.Genus == genus);

    // ------------------------------------------------------------- the states

    [Fact]
    public void ASystemWithNoBiologyOnRecordIsNotKnown()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        Assert.False(Ask(store, address: Elsewhere).IsKnown);
        Assert.True(Ask(store).IsKnown);
    }

    [Fact]
    public void ABodyWithSignalsOnlyIsNotMappedAndHasNoGenusLines()
    {
        var store = Store();
        Fss(store, 4, 2);

        var body = Assert.Single(Ask(store).Bodies);

        Assert.False(body.Mapped);
        Assert.Equal(2, body.BiologicalCount);
        Assert.Empty(body.Genera);
    }

    [Fact]
    public void AGenusTheSurfaceScanNamesAndNoneSampledIsNotStarted()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var line = Genus(Ask(store), 3, "Bacterium");

        Assert.Equal(GenusState.NotStarted, line.State);
        Assert.Equal(0, line.Taken);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AnOpenSetIsSamplingWithItsCount(int count)
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Taken(store, 3, "Bacterium Tela", count);

        var line = Genus(Ask(store), 3, "Bacterium");

        Assert.Equal(GenusState.Sampling, line.State);
        Assert.Equal(count, line.Taken);
    }

    [Fact]
    public void AFullSetIsDone()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium", "Stratum");
        Taken(store, 3, "Bacterium Tela", 3);

        var answer = Ask(store);

        Assert.Equal(GenusState.Done, Genus(answer, 3, "Bacterium").State);
        Assert.Equal(1, answer.Bodies[0].Done);
    }

    [Fact]
    public void AGenusAnalysedAndThenSoldStaysDone()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Taken(store, 3, "Bacterium Tela", 3);
        Organic(store, 3, "Analyse", "Bacterium Tela");
        Apply(store, $$"""
            {"timestamp":"{{Time()}}","event":"SellOrganicData","MarketID":1,
             "BioData":[{"Genus":"$Codex_Ent_Bacterium_Genus_Name;","Genus_Localised":"Bacterium",
                         "Species":"$Codex_Ent_Bacterium_Name;","Species_Localised":"Bacterium Tela","Value":1949000,"Bonus":0}]}
            """);

        Assert.Equal(GenusState.Done, Genus(Ask(store), 3, "Bacterium").State);
    }

    [Fact]
    public void ASetLostToAnotherSpeciesIsAbandonedWithItsCountUntilTheGenusIsLoggedAgain()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium", "Stratum");
        Taken(store, 3, "Bacterium Tela", 2);
        Organic(store, 3, "Log", "Stratum Tectonicas");

        var lost = Genus(Ask(store), 3, "Bacterium");

        Assert.Equal(GenusState.Abandoned, lost.State);
        Assert.Equal(2, lost.Taken);

        Organic(store, 3, "Log", "Bacterium Tela");

        var again = Genus(Ask(store), 3, "Bacterium");

        Assert.Equal(GenusState.Sampling, again.State);
        Assert.Equal(1, again.Taken);
    }

    [Fact]
    public void AGenusSampledThatTheSurfaceScanDoesNotNameIsAdded()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Organic(store, 3, "Log", "Fungoida Setisis");

        var body = Assert.Single(Ask(store).Bodies);

        Assert.Equal(["Fungoida", "Bacterium"], body.Genera.Select(genus => genus.Genus));
        Assert.Equal(GenusState.Sampling, body.Genera[0].State);
    }

    [Fact]
    public void ABodyRestoredFromAnEarlierSessionAppearsWithItsState()
    {
        var earlier = new[]
            {
                $$"""{"timestamp":"2026-08-10T09:00:00Z","event":"Location","StarSystem":"Vask Reach","SystemAddress":{{Here}}}""",
                $$"""
                {"timestamp":"2026-08-10T09:05:00Z","event":"SAASignalsFound","BodyName":"{{BodyName(6)}}","BodyID":6,"SystemAddress":{{Here}},
                 "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":1}],
                 "Genuses":[{"Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum"}]}
                """,
            }
            .Aggregate(BiologyBodies.None, (bodies, line) =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return bodies.Apply(parsed!);
            });

        var store = Store();
        Mapped(store, 3, "Bacterium");
        Taken(store, 6, "Stratum Tectonicas", 2);

        var state = State(store);
        state.Biology = earlier.With(state.Biology);

        var answer = Ask(store);

        Assert.Equal([3, 6], answer.Bodies.Select(body => body.BodyId));

        var restored = Genus(answer, 6, "Stratum");

        Assert.Equal(GenusState.Sampling, restored.State);
        Assert.Equal(2, restored.Taken);
    }

    // ------------------------------------------------------------ the order

    [Fact]
    public void BodiesAreInIdOrderAndGeneraSamplingAbandonedNotStartedThenDone()
    {
        var store = Store();
        Mapped(store, 5, "Osseus");
        Mapped(store, 3, "Bacterium", "Stratum", "Fungoida", "Tussock");
        Taken(store, 3, "Bacterium Tela", 3);
        Taken(store, 3, "Fungoida Setisis", 1);
        Organic(store, 3, "Log", "Tussock Pennata");

        var answer = Ask(store);

        Assert.Equal([3, 5], answer.Bodies.Select(body => body.BodyId));
        Assert.Equal(
            [("Tussock", GenusState.Sampling), ("Fungoida", GenusState.Abandoned), ("Stratum", GenusState.NotStarted), ("Bacterium", GenusState.Done)],
            answer.Bodies[0].Genera.Select(genus => (genus.Genus, genus.State)));
    }

    // ---------------------------------------------------------- the species

    private static readonly SystemBiology Surveyed = new(
        Here,
        [new SurveyedBody(BodyName(3), 3, 2_000_000, [new ExobiologySpecies("Bacterium", "Bacterium Aurasus", 1, 1_000_000)])]);

    [Fact]
    public void ASetsOwnSpeciesComesFirstWithItsVariantsEntry()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");
        Taken(store, 3, "Bacterium Tela", 1, variant: "Bacterium Tela - Gold");

        var earlier = new[] { new EarlierFind(BodyName(3), 2_320_103, "Bacterium Aurasus - Yellow", 0, 0) };

        var species = Assert.Single(Genus(Ask(store, Region, Surveyed, earlier), 3, "Bacterium").Species);

        Assert.Equal("Bacterium Tela", species.Species);
        Assert.Equal(SpeciesSource.Sampled, species.Source);
        Assert.NotNull(species.EntryId);
        Assert.Equal(CodexVariants.Named("Bacterium Tela - Gold"), species.EntryId);
    }

    [Fact]
    public void ReportedSpeciesJoinSpanshAndCanonnAndCarryCanonnsEntry()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium", "Stratum");

        var earlier = new[]
        {
            new EarlierFind(BodyName(3), 2_320_104, "Bacterium Aurasus - Lime", 1, 2),
            new EarlierFind(BodyName(3), 2_320_103, "Bacterium Aurasus - Yellow", 1, 2),
            new EarlierFind("vask reach 3", 2_420_000, "Stratum Tectonicas - Green", 3, 4),
            new EarlierFind(BodyName(4), 2_320_101, "Bacterium Tela - Turquoise", 5, 6),
        };

        var answer = Ask(store, Region, Surveyed, earlier);

        var bacterium = Assert.Single(Genus(answer, 3, "Bacterium").Species);
        Assert.Equal(new SpeciesLine("Bacterium Aurasus", SpeciesSource.Reported, 2_320_103, 1, 1_000_000), bacterium);

        var stratum = Assert.Single(Genus(answer, 3, "Stratum").Species);
        Assert.Equal(new SpeciesLine("Stratum Tectonicas", SpeciesSource.Reported, 2_420_000), stratum);
    }

    [Fact]
    public void ASpeciesOnlySpanshReportsHasNoEntry()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");

        var species = Assert.Single(Genus(Ask(store, Region, Surveyed), 3, "Bacterium").Species);

        Assert.Equal(new SpeciesLine("Bacterium Aurasus", SpeciesSource.Reported, null, 1, 1_000_000), species);
    }

    [Fact]
    public void WithNothingSampledOrReportedTheConditionsGiveThePossibleSpecies()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");

        var expected = ExobiologyCatalogue.Possible(
                new BodyConditions
                {
                    PlanetClass = "Rocky body",
                    Atmosphere = "thin carbon dioxide atmosphere",
                    Volcanism = "",
                    Gravity = 2.5 / 9.80665,
                    Temperature = 200.0,
                    Pressure = 2000.0 / 101325,
                },
                Region)
            .Where(entry => entry.Genus == "Bacterium")
            .Select(entry => entry.Species)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(expected);

        var line = Genus(Ask(store, Region), 3, "Bacterium");

        Assert.All(line.Species, species => Assert.Equal(SpeciesSource.Possible, species.Source));
        Assert.All(line.Species, species => Assert.Null(species.EntryId));
        Assert.Equal(expected, line.Species.Select(species => species.Species));
    }

    [Fact]
    public void NoRegionGivesNoPossibleSpecies()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");

        Assert.Empty(Genus(Ask(store), 3, "Bacterium").Species);
    }

    private static SystemBiology Spansh(params SurveyedBody[] bodies) => new(Here, bodies);

    private static SurveyedBody Row(int bodyId, long landmark, params ExobiologySpecies[] species) =>
        new(BodyName(bodyId), bodyId, landmark, species);

    [Fact]
    public void ABodyOnlySpanshReportsIsInTheAnswerWithItsReportedSpecies()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var answer = Ask(store, Region, Spansh(Row(7, 57_257_100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800))));

        var body = Assert.Single(answer.Bodies, line => line.BodyId == 7);
        Assert.False(body.Mapped);
        Assert.True(body.GeneraReported);
        Assert.Equal(57_257_100, body.ReportedValue);
        Assert.Equal(1, body.BiologicalCount);

        var genus = Genus(answer, 7, "Stratum");
        Assert.Equal(GenusState.NotStarted, genus.State);
        Assert.Equal(new SpeciesLine("Stratum Cucumisis", SpeciesSource.Reported, null, 7, 16_202_800), Assert.Single(genus.Species));
    }

    [Fact]
    public void ASpanshOnlyBodyTheCommanderSampledShowsTheSamplingWithSpanshsFigures()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Taken(store, 7, "Stratum Cucumisis", 1);

        var answer = Ask(store, Region, Spansh(Row(7, 57_257_100, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 7, 16_202_800))));

        var genus = Genus(answer, 7, "Stratum");
        Assert.Equal(GenusState.Sampling, genus.State);

        var species = Assert.Single(genus.Species);
        Assert.Equal(SpeciesSource.Sampled, species.Source);
        Assert.Equal(7, species.Count);
        Assert.Equal(16_202_800, species.Value);
    }

    [Fact]
    public void ASampledSpeciesSpanshDoesNotReportHasTheCatalogueValueAndNoCount()
    {
        var store = Store();
        Scan(store, 3);
        Mapped(store, 3, "Bacterium");
        Taken(store, 3, "Bacterium Tela", 1);

        var species = Assert.Single(Genus(Ask(store, Region, Surveyed), 3, "Bacterium").Species);

        Assert.Equal("Bacterium Tela", species.Species);
        Assert.Null(species.Count);
        Assert.Equal(ExobiologyCatalogue.All.First(entry => entry.Species == "Bacterium Tela").Value, species.Value);
    }

    [Fact]
    public void ABodyWithSignalsButNoSurfaceMapGetsSpanshsGenera()
    {
        var store = Store();
        Fss(store, 7, 2);

        var body = Assert.Single(Ask(store, Region, Spansh(Row(7, 900, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 2, 450)))).Bodies);

        Assert.False(body.Mapped);
        Assert.True(body.GeneraReported);
        Assert.Equal(2, body.BiologicalCount);
        Assert.Equal(["Stratum"], body.Genera.Select(genus => genus.Genus));
    }

    [Fact]
    public void AMappedBodyKeepsItsOwnGeneraAndStillGetsTheReportedValue()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var body = Assert.Single(Ask(store, Region, Spansh(Row(3, 900, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 2, 450)))).Bodies);

        Assert.True(body.Mapped);
        Assert.False(body.GeneraReported);
        Assert.Equal(900, body.ReportedValue);
        Assert.Equal(["Bacterium"], body.Genera.Select(genus => genus.Genus));
    }

    [Fact]
    public void TheBodiesComeBackInBodyIdOrderWhenTheTwoSourcesInterleave()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");
        Mapped(store, 9, "Bacterium");

        var answer = Ask(store, Region, Spansh(Row(7, 1, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 1)), Row(2, 1, new ExobiologySpecies("Stratum", "Stratum Cucumisis", 1, 1))));

        Assert.Equal([2, 3, 7, 9], answer.Bodies.Select(body => body.BodyId));
    }

    [Fact]
    public void WithNoSpanshAnswerNoFiguresAreAdded()
    {
        var store = Store();
        Mapped(store, 3, "Bacterium");

        var body = Assert.Single(Ask(store, Region).Bodies);

        Assert.Null(body.ReportedValue);
        Assert.False(body.GeneraReported);
        Assert.All(body.Genera.SelectMany(genus => genus.Species), species => Assert.Null(species.Count));
    }
}
