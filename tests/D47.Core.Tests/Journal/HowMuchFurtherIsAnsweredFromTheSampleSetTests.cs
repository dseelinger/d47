using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The distance left to the next specimen, in the callout and in "how much further" (#1818).</summary>
public class HowMuchFurtherIsAnsweredFromTheSampleSetTests
{
    /// <summary>Earth's radius: 0.001 degrees of longitude on the equator is about 111 metres.</summary>
    private const double Radius = 6_371_000;

    private static SurfaceFix At(double latitude, double longitude) => new(latitude, longitude, Radius);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(string scanType, string genus = "Aleoida") =>
        $$"""
          {"timestamp":"2026-08-16T10:00:00Z","event":"ScanOrganic","ScanType":"{{scanType}}",
           "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
           "Species":"$Codex_Ent_{{genus}}_02_Name;","Species_Localised":"{{genus}} Gravis",
           "SystemAddress":2175107336563,"Body":37}
          """;

    private static GameStateStore Landed(params (string Scan, SurfaceFix At)[] specimens)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:30:00Z","event":"Location","StarSystem":"Test","SystemAddress":2175107336563,"Body":"Test 1","BodyID":37,"Docked":false}"""));

        foreach (var (scan, at) in specimens)
        {
            store.Apply(Parse(scan), at);
        }

        return store;
    }

    private static GameStatus Standing(double latitude, double longitude) => new()
    {
        Latitude = latitude,
        Longitude = longitude,
        PlanetRadius = Radius,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static CapabilityRegistry Registry(GameStateStore store, GameStatus status) =>
        CapabilityRegistry.Build(
            [ExobiologyCapability.Create(null, () => store.Active, TestSurface.For(new MemoryInstall()).Settings, () => status)]);

    private static string Said(GameStateStore store, string scan) =>
        Assert.Single(new SamplingCallout().Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch, IsPriming: false, store.Active, Standing(0, 0), NavRoute.None, [Parse(scan)]))).Text;

    [Fact]
    public void TheFirstAndSecondSpecimenNameTheDistanceToTheNextAndTheThirdDoesNot()
    {
        var store = Landed((Scan("Log"), At(0, 0)));

        Assert.Equal("Aleoida Gravis, 1 of 3. 2 to go, 150 metres from here.", Said(store, Scan("Log")));

        store.Apply(Parse(Scan("Sample")), At(0, 0.005));

        Assert.Contains("2 of 3.", Said(store, Scan("Sample")), StringComparison.Ordinal);
        Assert.EndsWith("1 to go, 150 metres from here.", Said(store, Scan("Sample")), StringComparison.Ordinal);

        store.Apply(Parse(Scan("Sample")), At(0, 0.010));

        Assert.DoesNotContain("from here", Said(store, Scan("Sample")), StringComparison.Ordinal);
    }

    /// <summary>Two specimens about 222 m apart; 20 m from the first is 202 m from the second.</summary>
    [Fact]
    public async Task TheDistanceLeftIsTheLargestShortfallAcrossTheWholeSet()
    {
        var store = Landed((Scan("Log"), At(0, 0)), (Scan("Sample"), At(0, 0.002)));
        var registry = Registry(store, Standing(0, 0.00018));

        var result = await registry.InvokeAsync("get_sampling_progress", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        Assert.Contains("130 m to go.", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("far enough", result.Content, StringComparison.Ordinal);
        Assert.Equal("Aleoida Gravis: 130 m to go.", result.ShortForm);
    }

    [Fact]
    public async Task BeingBeyondTheRangeOfEverySpecimenIsFarEnough()
    {
        var store = Landed((Scan("Log"), At(0, 0)), (Scan("Sample"), At(0, 0.002)));
        var registry = Registry(store, Standing(0, 0.004));

        var result = await registry.InvokeAsync("get_sampling_progress", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        Assert.Equal("Aleoida Gravis: You are far enough.", result.ShortForm);
    }

    [Fact]
    public void HowMuchFurtherReachesTheToolOnlyWhileASetIsOpenOnThisBody()
    {
        var open = Landed((Scan("Log"), At(0, 0)));
        var none = Landed();

        foreach (var phrase in new[] { "how much further", "how much farther" })
        {
            Assert.Equal("get_sampling_progress", new KeywordRouter(Registry(open, Standing(0, 0))).MatchToolCommand(phrase)?.ToolName);
            Assert.Null(new KeywordRouter(Registry(none, Standing(0, 0))).MatchToolCommand(phrase));
        }
    }

    [Fact]
    public async Task TheSpokenAnswerIsOneShortLineAndTheModelReadsTheFullReport()
    {
        var store = Landed((Scan("Log"), At(0, 0)));
        var result = await Registry(store, Standing(0, 0.0005)).InvokeAsync(
            "get_sampling_progress", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

        Assert.Equal("Aleoida Gravis: 95 m to go.", result.ShortForm);
        Assert.Contains("from your last specimen", result.Content, StringComparison.Ordinal);
    }
}
