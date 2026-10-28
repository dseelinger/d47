using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class AHackMissionCanBeAStoryObjectiveTests
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
    public void BothHackFamiliesAreOfferedAndCarryADestination()
    {
        foreach (var family in new[] { "Mission_Hack", "Mission_OnFoot_Hack" })
        {
            Assert.DoesNotContain(MissionFamilies.SetAside, entry => entry.Family == family);
            Assert.Contains(family, MissionFamilies.Offered);
            Assert.Contains(family, MissionFamilies.WithDestination);
            Assert.True(MissionFamilies.CarriesDestination(family));
        }

        Assert.True(MissionFamilies.CarriesDestination("Mission_HackMegaship"));
    }

    [Theory]
    [InlineData("Mission_Hack", "Mission_HackMegaship_name")]
    [InlineData("Mission_Hack", "MISSION_Hack_BLOPS_name")]
    [InlineData("Mission_OnFoot_Hack", "Mission_OnFoot_Hack_Upload_Covert_MB_name")]
    public void ACompletionCountsTowardAHackBeat(string family, string name)
    {
        var beat = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = family };
        var completed = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionCompleted", "Faction":"Test Faction", "Name":"{{name}}", "MissionID":1, "Reward":10000 }""");

        Assert.True(AdventureFold.Matches(beat, completed));
    }

    [Fact]
    public void ASettlementHackDestinationFiresOnApproachingTheSettlement()
    {
        var taken = new AdventureTrigger { Kind = TriggerKind.MissionTaken, Handle = "hack", MissionFamily = "Mission_Hack" };
        var there = new AdventureTrigger { Kind = TriggerKind.MissionDestination, Handle = "hack" };
        var accepted = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionAccepted", "Faction":"Test Faction", "Name":"Mission_Hack_name", "DestinationSystem":"Test System", "DestinationSettlement":"Test Settlement", "MissionID":5 }""");
        var approach = Event($$"""{ "timestamp":"{{Stamp(At(2))}}", "event":"ApproachSettlement", "Name":"Test Settlement", "MarketID":1 }""");

        var standing = AdventureFold.Apply(Chapter(taken, there), accepted);
        Assert.Equal(1, standing.CurrentStep);

        standing = AdventureFold.Apply(standing, approach);
        Assert.True(standing.IsDone);
        Assert.DoesNotContain(AdventureValidation.Problems(Chapter(taken, there).Adventure), problem => problem.Contains("destination", StringComparison.Ordinal));
    }
}
