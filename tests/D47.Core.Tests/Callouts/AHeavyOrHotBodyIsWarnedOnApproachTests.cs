using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Approaching a scanned body with over 1 g or 700 K or more on the surface says so (#2593).</summary>
public class AHeavyOrHotBodyIsWarnedOnApproachTests
{
    private const long SystemAddress = 1_000_100;
    private const int BodyId = 4;
    private const string BodyName = "Col 285 Sector AB-C d1-2 3 a";

    private static string Scan(string gravity, string kelvin) => $$"""
        {"timestamp":"3311-04-02T09:00:00Z","event":"Scan","ScanType":"Detailed","SystemAddress":{{SystemAddress}},
         "BodyID":{{BodyId}},"BodyName":"{{BodyName}}","Landable":true,"SurfaceGravity":{{gravity}},"SurfaceTemperature":{{kelvin}}}
        """;

    private static readonly string Approach = $$"""
        {"timestamp":"3311-04-02T09:10:00Z","event":"ApproachBody","StarSystem":"Col 285 Sector AB-C d1-2",
         "SystemAddress":{{SystemAddress}},"Body":"{{BodyName}}","BodyID":{{BodyId}}}
        """;

    private static CommanderGameState Commander(params string[] events) => JournalScript.State(
    [
        """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
        .. events,
    ]);

    private static List<Announcement> Said(ICallout callout, CommanderGameState state, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [JournalScript.Parse(Approach)]))];

    private static List<string> Said(string gravity, string kelvin) =>
        [.. Said(new ApproachHazardCallout(), Commander(Scan(gravity, kelvin), Approach)).Select(a => a.Text)];

    [Fact]
    public void HighGravityIsSaid() =>
        Assert.Equal(["High gravity on Col 285 Sector AB-C d1-2 3 a: 1.14 g."], Said("11.2", "300"));

    [Fact]
    public void GravityUnderOneGIsNotSaid() => Assert.Empty(Said("9.7", "300"));

    [Fact]
    public void ASurfaceAtSevenHundredKelvinIsSaid() =>
        Assert.Equal(["Hot surface on Col 285 Sector AB-C d1-2 3 a: 700 K."], Said("5", "700"));

    [Fact]
    public void ASurfaceBelowSevenHundredKelvinIsNotSaid() => Assert.Empty(Said("5", "699"));

    [Fact]
    public void BothHazardsAreSaidInOneLine() =>
        Assert.Equal(
            ["High gravity and a hot surface on Col 285 Sector AB-C d1-2 3 a: 1.42 g, 812 K."],
            Said("13.93", "812"));

    [Fact]
    public void EveryApproachIsSaid()
    {
        var state = Commander(Scan("11.2", "300"), Approach);
        var callout = new ApproachHazardCallout();

        Assert.Single(Said(callout, state));
        var again = Said(callout, state);
        Assert.Single(again);
        Assert.Equal($"approach-hazard.{SystemAddress}.{BodyId}", again[0].Key);
    }

    [Fact]
    public void ABodyWithoutAScanIsNotSaid() =>
        Assert.Empty(Said(new ApproachHazardCallout(), Commander(Approach)));

    [Fact]
    public void NothingIsSaidFromTheBacklog() =>
        Assert.Empty(Said(new ApproachHazardCallout(), Commander(Scan("11.2", "300"), Approach), priming: true));
}
