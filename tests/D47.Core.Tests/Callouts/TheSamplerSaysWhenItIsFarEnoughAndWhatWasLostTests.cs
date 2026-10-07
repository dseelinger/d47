using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>"Far enough", the abandoned set and the specimen positions they rest on (#581).</summary>
public class TheSamplerSaysWhenItIsFarEnoughAndWhatWasLostTests
{
    /// <summary>Earth's radius: 0.001 degrees of longitude on the equator is about 111 metres.</summary>
    private const double Radius = 6_371_000;

    private static SurfaceFix At(double latitude, double longitude) => new(latitude, longitude, Radius);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(string scanType, string genus = "Stratum", string timestamp = "2026-08-16T10:00:00Z") =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"ScanOrganic","ScanType":"{{scanType}}",
           "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
           "Species":"$Codex_Ent_{{genus}}_02_Name;","Species_Localised":"{{genus}} Paleas",
           "SystemAddress":2175107336563,"Body":37}
          """;

    private static GameStateStore Commander()
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        return store;
    }

    private static GameStatus Standing(double latitude, double longitude) => new()
    {
        Latitude = latitude,
        Longitude = longitude,
        PlanetRadius = Radius,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static CalloutContext Tick(GameStateStore store, GameStatus status, params string[] lines) =>
        new(DateTimeOffset.UnixEpoch, IsPriming: false, store.Active, status, NavRoute.None, [.. lines.Select(Parse)]);

    private static List<string> Said(ICallout callout, CalloutContext context) =>
        [.. callout.Examine(context).Select(announcement => announcement.Text)];

    [Fact]
    public void EverySpecimenOfTheSetKeepsItsPosition()
    {
        var sampling = OrganicSampling.Empty
            .Apply(Parse(Scan("Log")), At(0, 0))
            .Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), At(0, 0.005));

        var genus = sampling.On(2175107336563, 37)!.Genera["Stratum"];

        Assert.Equal([At(0, 0), At(0, 0.005)], genus.Specimens);
    }

    /// <summary>Stratum needs 500 metres; the two specimens are 556 metres apart on the equator.</summary>
    [Fact]
    public void FarEnoughIsSaidOnCrossingTheRangeFromEverySpecimenAndOncePerSample()
    {
        var store = Commander();
        store.Apply(Parse(Scan("Log")), At(0, 0));
        store.Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), At(0, 0.005));

        var callout = new SamplingRangeCallout();

        // 778 metres from the second specimen, 222 from the first.
        Assert.Empty(Said(callout, Tick(store, Standing(0, -0.002))));

        // 489 metres from the second.
        Assert.Empty(Said(callout, Tick(store, Standing(0, 0.0094))));

        Assert.Equal(
            ["Far enough for the next Stratum Paleas sample."],
            Said(callout, Tick(store, Standing(0, 0.0096))));

        Assert.Empty(Said(callout, Tick(store, Standing(0, 0.0100))));
    }

    [Fact]
    public void FarEnoughIsArmedAgainByTheNextSample()
    {
        var store = Commander();
        store.Apply(Parse(Scan("Log")), At(0, 0));

        var callout = new SamplingRangeCallout();

        Assert.Single(Said(callout, Tick(store, Standing(0, 0.005))));

        store.Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), At(0, 0.005));

        Assert.Empty(Said(callout, Tick(store, Standing(0, 0.005))));
        Assert.Single(Said(callout, Tick(store, Standing(0, 0.0096))));
    }

    [Fact]
    public void FarEnoughIsNotSaidWithoutAPosition()
    {
        var store = Commander();
        store.Apply(Parse(Scan("Log")), At(0, 0));

        var noFix = new GameStatus { PlanetRadius = Radius, ReadAt = DateTimeOffset.UnixEpoch };

        Assert.Empty(Said(new SamplingRangeCallout(), Tick(store, noFix)));
        Assert.Empty(Said(new SamplingRangeCallout(), Tick(store, GameStatus.Unknown)));
    }

    [Fact]
    public void FarEnoughIsNotSaidForASpecimenTakenWithoutAPosition()
    {
        var store = Commander();
        store.Apply(Parse(Scan("Log")), At(0, 0));
        store.Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), null);

        Assert.Empty(Said(new SamplingRangeCallout(), Tick(store, Standing(0, 0.02))));
    }

    [Fact]
    public void ADifferentSpeciesAbandonsTheSetAndSaysHowMany()
    {
        var store = Commander();
        store.Apply(Parse(Scan("Log")), At(0, 0));
        store.Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), At(0, 0.005));

        var bacterium = Scan("Log", "Bacterium", "2026-08-16T10:10:00Z");
        store.Apply(Parse(bacterium), At(0, 0.006));

        Assert.Equal(
            ["That abandoned your 2 Stratum Paleas samples."],
            Said(new AbandonedSamplesCallout(), Tick(store, GameStatus.Unknown, bacterium)));

        var body = store.Active!.Sampling.On(2175107336563, 37)!;
        Assert.Equal("Bacterium", Assert.Single(body.InProgress).Genus);
    }

    [Fact]
    public void ACompletedRunIsNotAbandoned()
    {
        var store = Commander();

        foreach (var scan in new[] { "Log", "Sample", "Sample", "Analyse" })
        {
            store.Apply(Parse(Scan(scan)), At(0, 0));
        }

        var bacterium = Scan("Log", "Bacterium", "2026-08-16T10:10:00Z");
        store.Apply(Parse(bacterium), At(0, 0.006));

        Assert.Empty(Said(new AbandonedSamplesCallout(), Tick(store, GameStatus.Unknown, bacterium)));
    }

    [Fact]
    public void AnOlderSaveWithOnlyTheLastPositionStillLoads()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "sampling.json");

        File.WriteAllText(path, """
            {"commanders":[{"frontierId":"F1","bodies":[{"systemAddress":2175107336563,"bodyId":37,
              "genera":[{"genus":"Stratum","species":"Stratum Paleas","taken":2,"complete":false,
                "seenAt":"2026-08-16T10:05:00+00:00","latitude":0,"longitude":0.005,"radius":6371000}]}]}]}
            """);

        var store = new SamplingStore(path, NullLogger<SamplingStore>.Instance);
        store.Load();

        var genus = store.For("F1")!.On(2175107336563, 37)!.Genera["Stratum"];

        Assert.Equal(2, genus.Taken);
        Assert.Equal([At(0, 0.005)], genus.Specimens);
    }

    [Fact]
    public void EverySpecimenPositionSurvivesTheAppRestarting()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "sampling.json");

        var before = Commander();
        before.Apply(Parse(Scan("Log")), At(0, 0));
        before.Apply(Parse(Scan("Sample", timestamp: "2026-08-16T10:05:00Z")), At(0, 0.005));

        new SamplingStore(path, NullLogger<SamplingStore>.Instance).Save(before.All);

        var store = new SamplingStore(path, NullLogger<SamplingStore>.Instance);
        store.Load();

        Assert.Equal([At(0, 0), At(0, 0.005)], store.For("F1")!.On(2175107336563, 37)!.Genera["Stratum"].Specimens);
    }
}
