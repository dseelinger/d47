using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Leaving a body, or jumping away from it, with a set of one or two specimens taken there (#2639).</summary>
public class LeavingABodyMidSampleIsSaidTests
{
    private const long System = 2175107336563;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(string scanType, int body = 37, string genus = "Stratum", string timestamp = "2026-08-16T10:00:00Z") =>
        $$"""
          {"timestamp":"{{timestamp}}","event":"ScanOrganic","ScanType":"{{scanType}}",
           "Genus":"$Codex_Ent_{{genus}}_Genus_Name;","Genus_Localised":"{{genus}}",
           "Species":"$Codex_Ent_{{genus}}_02_Name;","Species_Localised":"{{genus}} Paleas",
           "SystemAddress":{{System}},"Body":{{body}}}
          """;

    private static string Approach(int body = 37) =>
        $$"""
          {"timestamp":"2026-08-16T09:55:00Z","event":"ApproachBody","StarSystem":"Smojue EB-O d6-37",
           "SystemAddress":{{System}},"Body":"Smojue EB-O d6-37 AB 1 b","BodyID":{{body}}}
          """;

    private static string Leave(int body = 37) =>
        $$"""
          {"timestamp":"2026-08-16T10:20:00Z","event":"LeaveBody","StarSystem":"Smojue EB-O d6-37",
           "SystemAddress":{{System}},"Body":"Smojue EB-O d6-37 AB 1 b","BodyID":{{body}}}
          """;

    private static string Jump(string jumpType) =>
        $$"""
          {"timestamp":"2026-08-16T10:20:00Z","event":"StartJump","JumpType":"{{jumpType}}","Taxi":false,
           "StarSystem":"Elsewhere","SystemAddress":1,"StarClass":"K"}
          """;

    private static GameStateStore Commander(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        store.Apply(Parse("""
            {"timestamp":"2026-08-16T09:30:00Z","event":"FSDJump","StarSystem":"Smojue EB-O d6-37","SystemAddress":2175107336563,
             "StarPos":[1.0,2.0,3.0],"Body":"Smojue EB-O d6-37 A","BodyID":1,"BodyType":"Star","JumpDist":10.0,"FuelUsed":1.0,"FuelLevel":30.0}
            """));

        foreach (var line in lines)
        {
            store.Apply(Parse(line));
        }

        return store;
    }

    private static List<string> Said(GameStateStore store, string line, bool priming = false)
    {
        var journalEvent = Parse(line);
        store.Apply(journalEvent);

        var context = new CalloutContext(
            DateTimeOffset.UnixEpoch, priming, store.Active, GameStatus.Unknown, NavRoute.None, [journalEvent]);

        return [.. new UnfinishedSamplesCallout().Examine(context).Select(announcement => announcement.Text)];
    }

    [Fact]
    public void LeavingTheBodyWithTwoSpecimensSaysSo()
    {
        var store = Commander(Approach(), Scan("Log"), Scan("Sample", timestamp: "2026-08-16T10:05:00Z"));

        Assert.Equal(
            ["Leaving Smojue EB-O d6-37 AB 1 b with Stratum Paleas at 2 of 3."],
            Said(store, Leave()));
    }

    [Fact]
    public void AHyperspaceJumpFromTheBodySaysSo()
    {
        var store = Commander(Approach(), Scan("Log"));

        Assert.Equal(
            ["Leaving Smojue EB-O d6-37 AB 1 b with Stratum Paleas at 1 of 3."],
            Said(store, Jump("Hyperspace")));
    }

    [Fact]
    public void ASupercruiseJumpSaysNothing()
    {
        var store = Commander(Approach(), Scan("Log"));

        Assert.Empty(Said(store, Jump("Supercruise")));
    }

    [Fact]
    public void ACompletedRunSaysNothing()
    {
        var store = Commander(
            Approach(),
            Scan("Log"),
            Scan("Sample", timestamp: "2026-08-16T10:05:00Z"),
            Scan("Sample", timestamp: "2026-08-16T10:10:00Z"),
            Scan("Analyse", timestamp: "2026-08-16T10:10:05Z"));

        Assert.Empty(Said(store, Leave()));
    }

    [Fact]
    public void AnOpenSetOnAnotherBodySaysNothing()
    {
        var store = Commander(Approach(38), Scan("Log", body: 38), Approach());

        Assert.Empty(Said(store, Leave()));
        Assert.Empty(Said(Commander(Approach(38), Scan("Log", body: 38), Approach()), Jump("Hyperspace")));
    }

    [Fact]
    public void ABodyWithNoSamplesSaysNothing()
    {
        Assert.Empty(Said(Commander(Approach()), Leave()));
    }

    [Fact]
    public void LiftoffAndSupercruiseBetweenSpecimensSayNothing()
    {
        var store = Commander(Approach(), Scan("Log"));

        foreach (var line in new[]
        {
            """{"timestamp":"2026-08-16T10:01:00Z","event":"Liftoff","PlayerControlled":true,"Taxi":false,"StarSystem":"Smojue EB-O d6-37","SystemAddress":2175107336563,"Body":"Smojue EB-O d6-37 AB 1 b","BodyID":37}""",
            """{"timestamp":"2026-08-16T10:02:00Z","event":"SupercruiseEntry","Taxi":false,"StarSystem":"Smojue EB-O d6-37","SystemAddress":2175107336563}""",
            """{"timestamp":"2026-08-16T10:03:00Z","event":"SupercruiseExit","Taxi":false,"StarSystem":"Smojue EB-O d6-37","SystemAddress":2175107336563,"Body":"Smojue EB-O d6-37 AB 1 b","BodyID":37,"BodyType":"Planet"}""",
        })
        {
            Assert.Empty(Said(store, line));
        }
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var store = Commander(Approach(), Scan("Log"));

        Assert.Empty(Said(store, Leave(), priming: true));
    }
}
