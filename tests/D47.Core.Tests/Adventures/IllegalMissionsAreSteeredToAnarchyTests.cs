using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Adventures;

public sealed class IllegalMissionsAreSteeredToAnarchyTests
{
    [Theory]
    [InlineData("Mission_OnFoot_AssassinationIllegal_Covert_MB")]
    [InlineData("Mission_OnFoot_Heist_Covert_MB")]
    [InlineData("Mission_OnFoot_Sabotage_Power_MB")]
    [InlineData("mission_onfoot_salvageillegal")]
    [InlineData("Mission_OnFoot_Heist")]
    public void ACrimeAgainstItsTargetIsIllegal(string name) => Assert.True(MissionFamilies.IsIllegal(name));

    [Theory]
    [InlineData("Mission_OnFoot_Assassination_MB")]
    [InlineData("Mission_OnFoot_Salvage_MB")]
    [InlineData("Mission_OnFoot")]
    [InlineData("Mission_Courier")]
    [InlineData(null)]
    public void AnyOtherMissionIsNot(string? name) => Assert.False(MissionFamilies.IsIllegal(name));

    [Fact]
    public void AChapterWithoutAnIllegalMissionPassesWhereverItArrives()
    {
        var slot = new DrawnActivity(TriggerKind.Mission, "Mission_OnFoot_Assassination", 2) { PlaceBefore = true };

        foreach (var seed in new uint[] { 1, 2, 3, 4, 5 })
        {
            var objective = Planned(Inputs(slot), Places(), seed).Objectives.Single(objective => objective.Purpose == PlannedPurpose.Activity);

            Assert.True(objective.Steps[0].Kind is TriggerKind.Arrive or TriggerKind.Dock);
            Assert.Equal("Mission_OnFoot_Assassination", objective.Steps[1].MissionFamily);
        }
    }
}
