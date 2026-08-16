using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Organic sampling (list.md Phase 18, "Exobiology sampling"). Every shape here was measured across
/// 632 real <c>ScanOrganic</c> events: the run is Log, Sample, Sample, Analyse on 94 of 101 runs;
/// <c>Body</c> is an id rather than a name; and not one event carries a position.
/// </summary>
public class OrganicSamplingTests
{
    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>Earth's radius, so the metre figures below are recognisable rather than arbitrary.</summary>
    private const double Radius = 6_371_000;

    private static SurfaceFix At(double latitude, double longitude) =>
        new(latitude, longitude, Radius);

    private static string Scan(string scanType, string genus = "Stratum", string timestamp = "2026-08-16T10:00:00Z") =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"ScanOrganic","ScanType":"{{scanType}}",
           "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
           "Species":"$Codex_Ent_{{genus}}_02_Name;","Species_Localised":"{{genus}} Paleas",
           "Variant":"$Codex_Ent_{{genus}}_02_F_Name;","Variant_Localised":"{{genus}} Paleas - Emerald",
           "SystemAddress":2175107336563,"Body":37}
          """;

    // ------------------------------------------------------------ the distance

    /// <summary>
    /// A great circle, not a flat approximation. One degree of latitude is about 111 kilometres on a
    /// body this size, and the whole feature is a distance in metres.
    /// </summary>
    [Fact]
    public void DistanceIsMeasuredOverTheCurveOfTheBody()
    {
        var metres = At(0, 0).MetresTo(At(0, 1));

        Assert.InRange(metres, 111_000, 111_400);
    }

    [Fact]
    public void TheSamePlaceIsNoDistanceAtAll() =>
        Assert.Equal(0, At(12.5, -9.8).MetresTo(At(12.5, -9.8)), 3);

    /// <summary>
    /// Radius comes off <c>Status.json</c> per body, so a small moon and a large planet give
    /// different distances for the same angle — which is the point of carrying it on the fix.
    /// </summary>
    [Fact]
    public void ASmallerBodyMakesTheSameAngleAShorterDrive()
    {
        var large = new SurfaceFix(0, 0, 6_371_000).MetresTo(new SurfaceFix(0, 1, 6_371_000));
        var small = new SurfaceFix(0, 0, 1_000_000).MetresTo(new SurfaceFix(0, 1, 1_000_000));

        Assert.True(small < large);
        Assert.InRange(large / small, 6.3, 6.4);
    }

    // -------------------------------------------------------------- the counts

    [Fact]
    public void ARunCountsSpecimensAndAnalyseIsNotOne()
    {
        var sampling = OrganicSampling.Empty;

        sampling = sampling.Apply(Parse(Scan("Log")), At(0, 0));
        Assert.Equal(1, Genus(sampling).Taken);

        sampling = sampling.Apply(Parse(Scan("Sample")), At(0, 0.005));
        Assert.Equal(2, Genus(sampling).Taken);

        sampling = sampling.Apply(Parse(Scan("Sample")), At(0, 0.010));
        var third = Genus(sampling);
        Assert.Equal(3, third.Taken);
        Assert.Equal(0, third.Remaining);

        // Analyse banks the run. Counting it would report "4 of 3" on every completed genus.
        sampling = sampling.Apply(Parse(Scan("Analyse")), At(0, 0.010));
        var done = Genus(sampling);
        Assert.Equal(3, done.Taken);
        Assert.True(done.Complete);
    }

    /// <summary>
    /// A fresh Log after a completed run is a second run of the same genus on the same body, which
    /// the corpus contains. The count restarts rather than climbing past three.
    /// </summary>
    [Fact]
    public void ASecondRunOfTheSameGenusStartsAgainRatherThanCountingOn()
    {
        var sampling = OrganicSampling.Empty;

        foreach (var scan in new[] { "Log", "Sample", "Sample", "Analyse" })
        {
            sampling = sampling.Apply(Parse(Scan(scan)), At(0, 0));
        }

        sampling = sampling.Apply(Parse(Scan("Log", timestamp: "2026-08-16T11:00:00Z")), At(1, 1));

        var restarted = Genus(sampling);
        Assert.Equal(1, restarted.Taken);
        Assert.False(restarted.Complete);
    }

    // --------------------------------------------------------------- the gap

    /// <summary>
    /// The gap is recorded by the fold, because by the time anything else looks at the record
    /// <c>LastAt</c> has already moved to the new specimen and the distance is gone.
    /// </summary>
    [Fact]
    public void TheGapTravelledForASpecimenIsRecordedAsItLands()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Parse(Scan("Log")), At(0, 0))
            .Apply(Parse(Scan("Sample")), At(0, 0.005));

        var gap = Genus(sampling).LastGapMetres;

        Assert.NotNull(gap);
        Assert.InRange(gap!.Value, 550, 560);
    }

    [Fact]
    public void TheFirstSpecimenOfARunHasNoGap() =>
        Assert.Null(Genus(OrganicSampling.Empty.Apply(Parse(Scan("Log")), At(0, 0))).LastGapMetres);

    /// <summary>
    /// <c>ScanOrganic</c> carries no position on any of the 632 events measured, so a sample taken
    /// with no <c>Status.json</c> fix still counts and simply carries no distance. Reporting zero
    /// would read as "you have not moved".
    /// </summary>
    [Fact]
    public void ASampleWithNoPositionStillCountsAndCarriesNoDistance()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Parse(Scan("Log")), at: null)
            .Apply(Parse(Scan("Sample")), at: null);

        var genus = Genus(sampling);

        Assert.Equal(2, genus.Taken);
        Assert.Null(genus.LastGapMetres);
    }

    // ------------------------------------------------------------ the learning

    /// <summary>
    /// A specimen Elite accepted proves the gap was sufficient, so the smallest accepted gap is an
    /// upper bound on the requirement — measured from this Commander's own play, because no
    /// licence-clean table of the figure exists.
    /// </summary>
    [Fact]
    public void TheClosestAcceptedGapIsLearnedAndKeepsItsSampleSize()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Parse(Scan("Log")), At(0, 0))
            .Apply(Parse(Scan("Sample")), At(0, 0.010))
            .Apply(Parse(Scan("Sample")), At(0, 0.015));

        var closest = sampling.ClosestAccepted("Stratum");

        Assert.NotNull(closest);
        Assert.Equal(2, closest!.Value.Samples);

        // The smaller of the two gaps — roughly 555 m against 1,113 m.
        Assert.InRange(closest.Value.Metres, 550, 560);
    }

    [Fact]
    public void AGenusNeverSampledTwiceHasNoLearnedGap() =>
        Assert.Null(OrganicSampling.Empty.Apply(Parse(Scan("Log")), At(0, 0)).ClosestAccepted("Stratum"));

    // ----------------------------------------------------------- the body key

    /// <summary>
    /// <c>ScanOrganic</c> writes <c>"Body": 37</c> — an id, not a name — so the key is numeric and
    /// two genera on one body share a record.
    /// </summary>
    [Fact]
    public void TwoGeneraOnOneBodyAreTrackedSeparatelyUnderOneBody()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Parse(Scan("Log", "Stratum")), At(0, 0))
            .Apply(Parse(Scan("Log", "Bacterium")), At(0, 0.02));

        var body = sampling.On(2175107336563, 37);

        Assert.NotNull(body);
        Assert.Equal(2, body!.Genera.Count);
        Assert.Equal(2, body.InProgress.Count);
    }

    // -------------------------------------------------------------- the callout

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(params (string Json, SurfaceFix? At)[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var (json, at) in events)
        {
            store.Apply(Parse(json), at);
        }

        return store.Active!;
    }

    [Fact]
    public void TheCalloutSaysTheCountAndTheGapItJustTravelled()
    {
        var state = Commander(
            (Scan("Log"), At(0, 0)),
            (Scan("Sample"), At(0, 0.005)));

        var said = Assert.Single(new SamplingCallout().Examine(Context(state, false, Scan("Sample"))));

        Assert.Contains("Stratum Paleas, 2 of 3", said.Text, StringComparison.Ordinal);
        Assert.Contains("from the last one", said.Text, StringComparison.Ordinal);
        Assert.Contains("1 to go", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyseIsAnnouncedAsTheRunFinishing()
    {
        var state = Commander(
            (Scan("Log"), At(0, 0)),
            (Scan("Sample"), At(0, 0.005)),
            (Scan("Sample"), At(0, 0.010)),
            (Scan("Analyse"), At(0, 0.010)));

        var said = Assert.Single(new SamplingCallout().Examine(Context(state, false, Scan("Analyse"))));

        Assert.Contains("That run is complete", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidFromTheBacklog()
    {
        var state = Commander((Scan("Log"), At(0, 0)));

        Assert.Empty(new SamplingCallout().Examine(Context(state, true, Scan("Log"))));
    }

    /// <summary>
    /// The whole item is a distance, and d47 still must not say whether it was far enough — the
    /// required figure is in the Codex in-game and no licence-clean table of it exists.
    /// </summary>
    [Fact]
    public void TheCalloutNeverJudgesWhetherTheGapWasEnough()
    {
        var state = Commander(
            (Scan("Log"), At(0, 0)),
            (Scan("Sample"), At(0, 0.005)));

        var said = Assert.Single(new SamplingCallout().Examine(Context(state, false, Scan("Sample"))));

        Assert.DoesNotContain("enough", said.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("too close", said.Text, StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------- outliving a session

    /// <summary>
    /// The repository's first per-body state that has to survive d47 restarting. The spine tails
    /// only the newest journal, so a run begun yesterday is otherwise simply gone — which is exactly
    /// the case a Commander who logged off on a planet comes back to.
    /// </summary>
    [Fact]
    public void ARunSurvivesTheAppRestarting()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "sampling.json");

        var before = new GameStateStore();
        before.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        before.Apply(Parse(Scan("Log")), At(0, 0));
        before.Apply(Parse(Scan("Sample")), At(0, 0.005));

        new SamplingStore(path, NullLogger<SamplingStore>.Instance).Save(before.All);

        // A second run of the app: new store, new state, same file.
        var store = new SamplingStore(path, NullLogger<SamplingStore>.Instance);
        store.Load();

        var after = new GameStateStore { Restore = store.For };
        after.Apply(Parse(
            """{"timestamp":"2026-08-17T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        var genus = after.Active!.Sampling.On(2175107336563, 37)!.Genera["Stratum"];

        Assert.Equal(2, genus.Taken);
        Assert.Equal("Stratum Paleas", genus.Species);

        // The position too, so a Commander logging back in beside their last specimen is told how
        // far they have moved rather than being told nothing.
        Assert.NotNull(genus.LastAt);
        Assert.InRange(genus.LastAt!.Value.MetresTo(At(0, 0.010)), 550, 560);
    }

    /// <summary>
    /// A second Commander's history must not merge into the first's — the same rule the whole
    /// journal spine is built on.
    /// </summary>
    [Fact]
    public void TwoCommandersHistoriesStayApart()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "sampling.json");

        var before = new GameStateStore();
        before.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"One"}"""));
        before.Apply(Parse(Scan("Log")), At(0, 0));
        before.Apply(Parse("""{"timestamp":"2026-08-16T09:30:00Z","event":"Commander","FID":"F2","Name":"Two"}"""));

        new SamplingStore(path, NullLogger<SamplingStore>.Instance).Save(before.All);

        var store = new SamplingStore(path, NullLogger<SamplingStore>.Instance);
        store.Load();

        Assert.NotNull(store.For("F1"));
        Assert.Null(store.For("F2"));
    }

    /// <summary>Derived state, so a bad file is discarded and rebuilt by playing rather than refused.</summary>
    [Fact]
    public void AnUnreadableFileLeavesNoHistoryRatherThanThrowing()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "sampling.json");
        File.WriteAllText(path, "{ not json");

        var store = new SamplingStore(path, NullLogger<SamplingStore>.Instance);
        store.Load();

        Assert.Null(store.For("F1"));
    }

    [Fact]
    public void AMissingFileIsTheNormalFirstRun()
    {
        using var install = new TempInstall();

        var store = new SamplingStore(
            Path.Combine(install.Root, "sampling.json"), NullLogger<SamplingStore>.Instance);

        store.Load();

        Assert.Null(store.For("F1"));
    }

    private static GenusProgress Genus(OrganicSampling sampling) =>
        sampling.On(2175107336563, 37)!.Genera["Stratum"];
}
