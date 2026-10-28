using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class ASettlementDisableMissionCanBeAStoryObjectiveTests
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
    public void OnlyTheMegashipVariantIsSetAside()
    {
        Assert.Contains(MissionFamilies.SetAside, entry => entry.Family == "Mission_DisableMegaship");
        Assert.DoesNotContain(MissionFamilies.SetAside, entry => entry.Family == "Mission_Disable");
        Assert.Contains("Mission_Disable", MissionFamilies.Offered);
        Assert.DoesNotContain("Mission_DisableMegaship", MissionFamilies.Offered);
        Assert.Contains("Mission_Disable", MissionFamilies.WithDestination);
        Assert.True(MissionFamilies.IsSetAside("MISSION_DisableMegaship_name"));
        Assert.False(MissionFamilies.IsSetAside("MISSION_Disable_BLOPS_name"));
        Assert.False(MissionFamilies.IsSetAside("Mission_Disable_BLOPS_Expansion_name"));
    }

    [Theory]
    [InlineData("MISSION_Disable_BLOPS_name", true)]
    [InlineData("MISSION_DisableMegaship_name", false)]
    public void ADisableBeatCountsSettlementCompletionsOnly(string name, bool counts)
    {
        var beat = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = "Mission_Disable" };
        var completed = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionCompleted", "Faction":"Test Faction", "Name":"{{name}}", "MissionID":1, "Reward":10000 }""");

        Assert.Equal(counts, AdventureFold.Matches(beat, completed));
    }

    [Fact]
    public void ABeatNamingTheMegashipVariantIsRefused()
    {
        var problems = AdventureValidation.Problems(
            Chapter(new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = "Mission_DisableMegaship" }).Adventure);

        Assert.Contains(problems, problem => problem.Contains("set aside", StringComparison.Ordinal));
    }

    [Fact]
    public void ASettlementDisableDestinationFiresOnApproachingTheSettlement()
    {
        var taken = new AdventureTrigger { Kind = TriggerKind.MissionTaken, Handle = "disable", MissionFamily = "Mission_Disable" };
        var there = new AdventureTrigger { Kind = TriggerKind.MissionDestination, Handle = "disable" };
        var accepted = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"MissionAccepted", "Faction":"Test Faction", "Name":"Mission_Disable_name", "DestinationSystem":"Test System", "DestinationSettlement":"Test Settlement", "MissionID":5 }""");
        var approach = Event($$"""{ "timestamp":"{{Stamp(At(2))}}", "event":"ApproachSettlement", "Name":"Test Settlement", "MarketID":1 }""");

        var standing = AdventureFold.Apply(Chapter(taken, there), accepted);
        Assert.Equal(1, standing.CurrentStep);

        standing = AdventureFold.Apply(standing, approach);
        Assert.True(standing.IsDone);
    }
}
