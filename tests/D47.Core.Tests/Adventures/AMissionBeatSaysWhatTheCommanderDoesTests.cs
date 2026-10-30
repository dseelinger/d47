using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class AMissionBeatSaysWhatTheCommanderDoesTests
{
    private static readonly DateTimeOffset Now = Accepted.AddHours(1);

    [Fact]
    public void EveryDrawableMissionFamilyHasALine()
    {
        var missing = ChapterActivities.All
            .Where(row => row.Kind == TriggerKind.Mission && MissionHowTo.For(row.Family) is null)
            .Select(row => row.Family)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void ALineIsFoundIgnoringCaseAndAFamilyWithNoneGivesNull()
    {
        Assert.NotNull(MissionHowTo.For("mission_collect"));
        Assert.Null(MissionHowTo.For("Mission_Disable"));
        Assert.Null(MissionHowTo.For(null));
    }

    [Fact]
    public void ACountedMissionHandOffEndsWithTheFamilyLine()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 4, MissionFamily = "Mission_Collect" };

        Assert.Equal(
            $"Next: complete 4 collect missions, counted from now. Collect missions: 0 of 4. {MissionHowTo.For("Mission_Collect")}",
            trigger.HandOff());
    }

    [Fact]
    public void ATakeAMissionHandOffEndsWithTheFamilyLine()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.MissionTaken, Handle = "parcel", MissionFamily = "Mission_Courier" };

        Assert.EndsWith(" " + MissionHowTo.For("Mission_Courier"), trigger.HandOff(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMissionFamilyWithNoLineLeavesTheHandOffAlone()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 2, MissionFamily = "Mission_Disable" };

        Assert.EndsWith("counted from now. " + trigger.Progress(0) + ".", trigger.HandOff(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheNowLineForAMissionBeatEndsWithTheFamilyLine()
    {
        var route = LanternRoute(Accepted);
        var trigger = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 4, MissionFamily = "Mission_Collect" };
        var adventure = route with { Beats = [Beat("The Hold", "setup", trigger, "Fill it."), .. route.Beats.Skip(1)] };
        var block = AdventureContext.Describe([AdventureFold.Start(adventure)], _ => null, Now)!;

        Assert.Contains(MissionHowTo.For("Mission_Collect")!, block, StringComparison.Ordinal);
    }
}
