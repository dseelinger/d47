using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public sealed class AMissionNameSaysWhereItIsListedAndWhyTests
{
    [Fact]
    public void ADisableMissionInACivilWarIsAnOperationsMission()
    {
        var mission = MissionCatalogue.Find("Mission_Disable_CivilWar");

        Assert.NotNull(mission);
        Assert.Equal("Operations", mission.Group);
        Assert.Equal("Civil War", mission.State);
    }

    [Fact]
    public void ACourierMissionInAnExpansionIsATransportationMission()
    {
        var mission = MissionCatalogue.Find("Mission_Courier_Expansion");

        Assert.NotNull(mission);
        Assert.Equal("Transportation", mission.Group);
        Assert.Equal("Expansion", mission.State);
    }

    [Fact]
    public void ACourierMissionForTheFederationNavyHasARankAndNoState()
    {
        var mission = MissionCatalogue.Find("MISSION_Courier_RankFed");

        Assert.NotNull(mission);
        Assert.Equal("Federation", mission.Rank);
        Assert.Equal(string.Empty, mission.State);
    }

    [Fact]
    public void AWingMissionIsItsSoloFamilyWithAWingVariant()
    {
        var mission = MissionCatalogue.Find("Mission_AssassinateWing_Legal_Bust");

        Assert.NotNull(mission);
        Assert.Equal("Mission_Assassinate", mission.Family);
        Assert.Contains("wing", mission.Variants);
        Assert.Equal("Bust", mission.State);
    }

    [Fact]
    public void ASkimmerMissionKeepsTheFamilyTheSetAsideListNames()
    {
        var mission = MissionCatalogue.Find("Mission_Massacre_Skimmer_Expansion");

        Assert.NotNull(mission);
        Assert.Equal("Mission_Massacre_Skimmer", mission.Family);
    }

    [Fact]
    public void AThargoidWarMissionIsNotOnTheBoard()
    {
        Assert.Null(MissionCatalogue.Find("Mission_TW_Rescue_Alert"));
    }

    [Fact]
    public void EveryFamilyInTheTableHasAGroup()
    {
        Assert.NotEmpty(MissionCatalogue.All);
        Assert.All(MissionCatalogue.All, mission => Assert.False(string.IsNullOrWhiteSpace(mission.Group), mission.Name));
    }
}
