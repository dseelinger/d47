using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A scene's brief carries the scenario whoever it is told to, as who the speakers are, and says what they
/// have seen.
/// </summary>
public class SceneSpeakersAreToldTheScenarioAtEveryAudienceTests
{
    private const string Scenario = "A secret raid to recover the data the Silver Partnership stole.";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static SceneBeat Beat(SceneBeatKind kind, string? victim = null, int kills = 0, int merged = 0, bool seen = false) =>
        new(kind, 1, "Archambeau's Serenity", "LTT 7786 Silver Partnership", "Anarchy", "LTT 7786 6 a", victim, kills, merged, seen);

    [Fact]
    public void AnAboardScenarioReachesTheSceneButNotTheTimedChatter()
    {
        var brief = NpcChatter.SceneInstruction(Beat(SceneBeatKind.Arrived), Scenario);

        Assert.Contains(Scenario, brief, StringComparison.Ordinal);
        Assert.Contains(
            "They do not know who the Commander is or why the Commander is there.", brief, StringComparison.Ordinal);
        Assert.Contains("Archambeau's Serenity", brief, StringComparison.Ordinal);

        Assert.Null(NpcChatter.ScenarioFor(ScenarioAudience.Aboard, Scenario));
        Assert.Null(NpcChatter.ScenarioFor(ScenarioAudience.Carrier, Scenario));
    }

    [Fact]
    public void TwoKillsWithNoAttackSaySoInTheSecondBrief()
    {
        var tracker = new SceneTracker();

        foreach (var line in new[]
        {
            """{"timestamp":"2026-09-30T00:48:23Z","event":"ApproachSettlement","Name":"Archambeau's Serenity","BodyName":"LTT 7786 6 a"}""",
            """{"timestamp":"2026-09-30T00:49:45Z","event":"Disembark","Taxi":false,"Body":"LTT 7786 6 a","OnPlanet":true}""",
            """{"timestamp":"2026-09-30T00:50:00Z","event":"CommitCrime","CrimeType":"onFoot_murder","Victim":"Miles Lehner"}""",
            """{"timestamp":"2026-09-30T00:51:00Z","event":"Bounty","PilotName_Localised":"Hugh Juarez","Target":"lightassaultsuitai_class1"}""",
        })
        {
            tracker.Fold(Event(line));
        }

        var second = tracker.Snapshot.Last!;

        Assert.Equal(SceneBeatKind.Down, second.Kind);
        Assert.Equal(2, second.Kills);
        Assert.False(second.Seen);

        var brief = NpcChatter.SceneInstruction(second with { Merged = 1 }, Scenario);

        Assert.Contains("Hugh Juarez, one of their people, has just been killed.", brief, StringComparison.Ordinal);
        Assert.Contains("That makes 2 dead", brief, StringComparison.Ordinal);
        Assert.Contains("nobody has seen the Commander", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void AKillInAFightIsPutOnTheIntruder()
    {
        var brief = NpcChatter.SceneInstruction(Beat(SceneBeatKind.Down, "Sarai Prifti", kills: 3, merged: 2, seen: true), Scenario);

        Assert.Contains("2 of their people have just been killed, the last of them Sarai Prifti.", brief, StringComparison.Ordinal);
        Assert.Contains("The intruder they are fighting did it.", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("nobody has seen", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void ASceneLineIsNotAnsweredAndMayTalkAboutShooting()
    {
        Assert.False(NpcChatter.MayNotice(NpcChatterKind.Scene, 0));
        Assert.Equal(NpcChatterKind.Scene, NpcChatter.KindOf(SceneCallout.Key));

        var lines = NpcChatter.Parse("Vasquez: They're shooting at you, Reyes, get down!\nReyes: Moving to the hangar.", NpcChatterKind.Scene);

        Assert.Equal(2, lines.Count);
    }
}
