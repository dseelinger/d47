using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A DSS that names a landable body's genera is followed by one line naming their candidate species.</summary>
public class ACompletedDssNamesTheCandidateSpeciesTests
{
    private const long SystemAddress = 1_000_200;
    private const int BodyId = 23;
    private const string BodyName = "Fixture 3 b";

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:01Z","event":"Location","StarSystem":"Fixture","SystemAddress":1000200}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Signals(int biological) => $$"""
        {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"{{BodyName}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{biological}}}]}
        """;

    private static string Dss(string at, params string[] genera) => $$"""
        {"timestamp":"2026-08-16T{{at}}Z","event":"SAASignalsFound","BodyName":"{{BodyName}}",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":{{genera.Length}}}],
         "Genuses":[{{string.Join(",", genera.Select(genus => $$"""{"Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}"}"""))}}]}
        """;

    private static string Scan(string at, double gravity = 0.5, double temperature = 200) => $$"""
        {"timestamp":"2026-08-16T{{at}}Z","event":"Scan","ScanType":"Detailed","StarSystem":"Fixture",
         "SystemAddress":{{SystemAddress}},"BodyID":{{BodyId}},"BodyName":"{{BodyName}}",
         "PlanetClass":"Rocky body","Atmosphere":"thin carbon dioxide atmosphere","Volcanism":"",
         "SurfaceGravity":{{gravity * 9.80665}},"SurfaceTemperature":{{temperature}},"SurfacePressure":{{0.02 * 101325}},
         "Landable":true}
        """;

    private static ExobiologyEntry Species(string genus, string species) => new()
    {
        Species = species,
        Genus = genus,
        Value = 1_000_000,
        PlanetTypes = [],
        Atmospheres = [],
        Volcanism = [],
        GravityLow = 0,
        GravityHigh = 0,
        TemperatureLow = 0,
        TemperatureHigh = 0,
        PressureLow = 0,
        PressureHigh = 0,
        Regions = [],
    };

    private static GenusCandidates Genus(string genus, params string[] species) =>
        new(genus, 1_000_000, 5_000_000, [.. species.Select(name => Species(genus, name))]);

    private static BiologyEstimate Estimate(params GenusCandidates[] named) =>
        new(named.Sum(genus => genus.High), [.. named.Select(genus => genus.Genus)], named.Sum(genus => genus.Low), named.Sum(genus => genus.High), named);

    [Fact]
    public void ADssAfterAnFssScanIsSaidAfterTheFssLine()
    {
        var callout = new BiologyCallout();
        var scan = Scan("10:01:00");
        var dss = Dss("10:02:00", "Stratum");

        var first = Assert.Single(callout.Examine(Context(Commander(Signals(2), scan), false, scan)));
        var second = Assert.Single(callout.Examine(Context(Commander(Signals(2), scan, dss), false, dss)));

        Assert.Equal("3 b could hold up to 18.2 million in biology: Stratum, Bacterium.", first.Text);
        Assert.StartsWith("3 b could hold 1.4 million to 16.2 million in biology: ", second.Text);
        Assert.Contains("Stratum", second.Text);
    }

    [Fact]
    public void ADssBeforeTheScanIsSaidAndTheDetailedScanAfterItIsNot()
    {
        var callout = new BiologyCallout();
        var scan = Scan("10:01:00");
        var dss = Dss("10:02:00", "Stratum");
        var detailed = Scan("10:03:00");

        var said = Assert.Single(callout.Examine(Context(Commander(Signals(2), scan, dss), false, dss)));

        Assert.StartsWith("3 b could hold 1.4 million to 16.2 million in biology: ", said.Text);
        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, dss, detailed), false, detailed)));
        Assert.True(callout.Named(SystemAddress, BodyId));
    }

    [Fact]
    public void ASecondDssOfTheSameBodySaysNothing()
    {
        var callout = new BiologyCallout();
        var scan = Scan("10:01:00");
        var dss = Dss("10:02:00", "Stratum");
        var again = Dss("10:09:00", "Stratum");

        Assert.Single(callout.Examine(Context(Commander(Signals(2), scan, dss), false, dss)));
        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, dss, again), false, again)));
    }

    [Fact]
    public void ADssOfABodyBelowTheThresholdSaysNothing()
    {
        var scan = Scan("10:01:00", gravity: 0.3, temperature: 180);
        var dss = Dss("10:02:00", "Stratum");

        Assert.Empty(new BiologyCallout().Examine(Context(Commander(Signals(2), scan, dss), false, dss)));
    }

    [Fact]
    public void ADssOfABodyAnotherCalloutHasSaidSaysNothing()
    {
        var scan = Scan("10:01:00");
        var dss = Dss("10:02:00", "Stratum");
        var callout = new BiologyCallout { AlreadySaid = (_, _) => true };

        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, dss), false, dss)));
    }

    [Fact]
    public void ADssInTheBacklogSaysNothing()
    {
        var scan = Scan("10:01:00");
        var dss = Dss("10:02:00", "Stratum");
        var callout = new BiologyCallout();

        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, dss), true, dss)));
        Assert.Empty(callout.Examine(Context(Commander(Signals(2), scan, dss), false, scan)));
    }

    [Fact]
    public void AGenusWithOneCandidateIsNamedByItsSpecies() =>
        Assert.Equal("Tussock Pennatis", BiologyCallout.Species(Estimate(Genus("Tussock", "Tussock Pennatis"))));

    [Fact]
    public void TwoCandidatesThatStartWithTheirGenusSayTheGenusOnce() =>
        Assert.Equal(
            "Stratum Paleas or Excutitus",
            BiologyCallout.Species(Estimate(Genus("Stratum", "Stratum Paleas", "Stratum Excutitus"))));

    [Fact]
    public void TwoCandidatesThatDoNotStartWithTheirGenusAreSaidInFull() =>
        Assert.Equal(
            "Roseum Anemone or Croceum Anemone",
            BiologyCallout.Species(Estimate(Genus("Anemone", "Roseum Anemone", "Croceum Anemone"))));

    [Fact]
    public void ThreeCandidatesAreSaidAsTheGenusAndACount() =>
        Assert.Equal(
            "Bacterium, one of three",
            BiologyCallout.Species(Estimate(Genus("Bacterium", "Bacterium Aurasus", "Bacterium Tela", "Bacterium Nebulus"))));

    [Fact]
    public void AGenusWithNoCandidatesIsNamedAlone() =>
        Assert.Equal("Fonticulua", BiologyCallout.Species(Estimate(Genus("Fonticulua"))));

    [Fact]
    public void SeveralGeneraAreJoinedWithAnAndBeforeTheLast() =>
        Assert.Equal(
            "Tussock Pennatis, Stratum Paleas or Excutitus, and Bacterium, one of three",
            BiologyCallout.Species(Estimate(
                Genus("Tussock", "Tussock Pennatis"),
                Genus("Stratum", "Stratum Paleas", "Stratum Excutitus"),
                Genus("Bacterium", "Bacterium Aurasus", "Bacterium Tela", "Bacterium Nebulus"))));

    [Fact]
    public void GeneraPastThreeAreCounted() =>
        Assert.Equal(
            "Tussock Pennatis, Osseus Spiralis, Fungoida Setisis, and 2 more",
            BiologyCallout.Species(Estimate(
                Genus("Tussock", "Tussock Pennatis"),
                Genus("Osseus", "Osseus Spiralis"),
                Genus("Fungoida", "Fungoida Setisis"),
                Genus("Frutexa", "Frutexa Flava"),
                Genus("Aleoida", "Aleoida Arcus"))));
}
