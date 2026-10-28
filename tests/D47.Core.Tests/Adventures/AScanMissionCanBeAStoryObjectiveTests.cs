using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class AScanMissionCanBeAStoryObjectiveTests
{
    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private static AdventureStanding Chapter(params AdventureTrigger[] triggers)
    {
        var route = LanternRoute(Accepted);

        return AdventureFold.Start(route with
        {
            Beats = [route.Beats[0] with { Steps = [.. triggers.Select(trigger => new BeatStep { Trigger = trigger })] }],
        });
    }

    [Fact]
    public void ScanIsOfferedAndCarriesADestination()
    {
        Assert.DoesNotContain(MissionFamilies.SetAside, entry => entry.Family == "Mission_Scan");
        Assert.Contains("Mission_Scan", MissionFamilies.Offered);
        Assert.Contains("Mission_Scan", MissionFamilies.WithDestination);
        Assert.True(MissionFamilies.CarriesDestination("Mission_Scan"));
    }

    [Fact]
    public void AnUpperCaseCompletionCountsTowardAScanBeat()
    {
        var beat = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = "Mission_Scan" };
        var completed = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionCompleted", "Faction":"Test Faction", "Name":"MISSION_Scan_name", "MissionID":1, "Reward":10000 }""");

        Assert.True(AdventureFold.Matches(beat, completed));
    }

    [Fact]
    public void AScanMissionDestinationFiresOnTheJumpIntoItsSystem()
    {
        var taken = new AdventureTrigger { Kind = TriggerKind.MissionTaken, Handle = "survey", MissionFamily = "Mission_Scan" };
        var there = new AdventureTrigger { Kind = TriggerKind.MissionDestination, Handle = "survey" };
        var accepted = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionAccepted", "Faction":"Test Faction", "Name":"MISSION_Scan", "DestinationSystem":"Kweleutahe", "DestinationStation":"Test Installation", "MissionID":5 }""");

        var standing = AdventureFold.Apply(Chapter(taken, there), accepted);
        Assert.Equal(1, standing.CurrentStep);

        standing = AdventureFold.Apply(standing, Jump(Veyl, At(2), "Kweleutahe"));
        Assert.True(standing.IsDone);
        Assert.DoesNotContain(AdventureValidation.Problems(Chapter(taken, there).Adventure), problem => problem.Contains("destination", StringComparison.Ordinal));
    }
}
