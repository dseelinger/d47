using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>MissionName.RequiredStates: curated per family, not read from name endings (#2137).</summary>
public class TheRebootMissionsCarryTheFactionStatesTheyRequireTests
{
    private static readonly string[] Expected =
    [
        "Civil Unrest", "Bust", "Infrastructure Failure", "Blight", "Famine", "Lockdown", "Natural Disaster",
        "Outbreak", "Pirate Attack", "Terrorist Attack",
    ];

    private static bool IsReboot(MissionName name) =>
        name.Family is "Mission_OnFoot_Reboot" or "Mission_OnFoot_RebootRestore";

    [Fact]
    public void BothRebootFamiliesCarryTheTenStatesInOrder()
    {
        var reboots = MissionCatalogue.All.Where(IsReboot).ToList();

        Assert.Contains(reboots, name => name.Family == "Mission_OnFoot_Reboot");
        Assert.Contains(reboots, name => name.Family == "Mission_OnFoot_RebootRestore");
        Assert.All(reboots, name => Assert.Equal(Expected, name.RequiredStates));
    }

    [Fact]
    public void EveryOtherFamilyCarriesNone()
    {
        Assert.All(MissionCatalogue.All.Where(name => !IsReboot(name)), name => Assert.Empty(name.RequiredStates));
    }

    [Fact]
    public void EveryListedStateIsOneTheGalaxyFiltersOffer()
    {
        var offered = GalaxyFilters.All.First(filter => filter.Name == "faction_state").Choices;

        Assert.All(Expected, state => Assert.Contains(state, offered));
    }
}
