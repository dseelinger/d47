using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class SampledVariantsAndLostSetsTests
{
    private const long System = 2175107336563;
    private const string Path = @"C:\d47-test\sampling.json";

    private static JournalEvent Scan(string scanType, string species = "02", int body = 37, string? variant = "F")
    {
        var variantFields = variant is null
            ? ""
            : $$"""
                "Variant":"$Codex_Ent_Stratum_{{species}}_{{variant}}_Name;","Variant_Localised":"Stratum {{species}} - {{variant}}",
                """;

        var json = $$"""
            {"timestamp":"2026-08-16T10:00:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
             "Genus":"$Codex_Ent_Stratum_Genus_Name;","Genus_Localised":"Stratum",
             "Species":"$Codex_Ent_Stratum_{{species}}_Name;","Species_Localised":"Stratum {{species}}",
             {{variantFields}}
             "SystemAddress":{{System}},"Body":{{body}}}
            """;

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static GenusProgress Stratum(OrganicSampling sampling, int body = 37) =>
        sampling.On(System, body)!.Genera["Stratum"];

    [Fact]
    public void ALogThenASampleKeepTheVariant()
    {
        var sampling = OrganicSampling.Empty.Apply(Scan("Log")).Apply(Scan("Sample", variant: null));

        var genus = Stratum(sampling);

        Assert.Equal("Stratum 02 - F", genus.Variant);
        Assert.Equal("$Codex_Ent_Stratum_02_F_Name;", genus.VariantSymbol);
        Assert.Equal(2, genus.Taken);
    }

    [Fact]
    public void ASpecimenOfAnotherSpeciesPutsTheOpenSetOnItsBodysLost()
    {
        var sampling = OrganicSampling.Empty.Apply(Scan("Log")).Apply(Scan("Log", species: "03", body: 12));

        var lost = sampling.On(System, 37)!.Lost["Stratum"];

        Assert.Equal("Stratum 02", lost.Species);
        Assert.Equal("Stratum 02 - F", lost.Variant);
        Assert.Equal(1, lost.Taken);
        Assert.Empty(sampling.On(System, 12)!.Lost);
        Assert.NotNull(sampling.Abandoned);
        Assert.Single(sampling.Abandoned!.Sets);
    }

    [Fact]
    public void TheNextLogOfThatGenusOnThatBodyClearsTheLostSet()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Scan("Log"))
            .Apply(Scan("Log", species: "03", body: 12))
            .Apply(Scan("Log", species: "03", body: 37));

        Assert.Empty(sampling.On(System, 37)!.Lost);
        Assert.Equal("Stratum 03", Stratum(sampling).Species);
    }

    [Fact]
    public void ALostSetStaysOnItsBodyUntilThatGenusIsLoggedThere()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Scan("Log"))
            .Apply(Scan("Log", species: "03", body: 12))
            .Apply(Scan("Sample", species: "03", body: 12));

        Assert.Single(sampling.On(System, 37)!.Lost);
    }

    [Fact]
    public void ALostSetAndAVariantSurviveTheStore()
    {
        var files = new MemoryFileSystem();
        var before = new GameStateStore();
        before.Apply(JournalEvent.TryParse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            NullLogger.Instance, out var commander) ? commander! : null!);
        before.Apply(Scan("Log"));
        before.Apply(Scan("Log", species: "03", body: 12));

        new SamplingStore(Path, files, NullLogger<SamplingStore>.Instance).Save(before.All);

        var store = new SamplingStore(Path, files, NullLogger<SamplingStore>.Instance);
        store.Load();

        var sampling = store.For("F1")!;
        var lost = sampling.On(System, 37)!.Lost["Stratum"];

        Assert.Equal("Stratum 02 - F", lost.Variant);
        Assert.Equal("$Codex_Ent_Stratum_02_F_Name;", lost.VariantSymbol);
        Assert.Equal(1, lost.Taken);
        Assert.Equal("Stratum 03 - F", Stratum(sampling, 12).Variant);
        Assert.Empty(sampling.On(System, 12)!.Lost);
    }

    [Fact]
    public void ASavedFileWithNeitherFieldReadsBackWithNullsAndNoLostSets()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Path, """
            {"commanders":[{"frontierId":"F1","bodies":[{"systemAddress":2175107336563,"bodyId":37,
              "genera":[{"genus":"Stratum","species":"Stratum 02","taken":2,"complete":false,
                         "seenAt":"2026-08-16T10:00:00+00:00"}]}]}]}
            """);

        var store = new SamplingStore(Path, files, NullLogger<SamplingStore>.Instance);
        store.Load();

        var body = store.For("F1")!.On(System, 37)!;

        Assert.Null(body.Genera["Stratum"].Variant);
        Assert.Null(body.Genera["Stratum"].VariantSymbol);
        Assert.Empty(body.Lost);
    }
}
