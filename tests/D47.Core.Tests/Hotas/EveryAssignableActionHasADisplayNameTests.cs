using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>The switch page lists actions by a display name; the stored id does not change (#1150).</summary>
public class EveryAssignableActionHasADisplayNameTests
{
    [Fact]
    public void EveryActionASwitchMayNameReadsAsAShortName()
    {
        Assert.All(SwitchValidation.Assignable, action => Assert.False(string.IsNullOrWhiteSpace(action.DisplayName), action.Id));
    }

    [Fact]
    public void TheNamesAreTheOnesTheDesignLists()
    {
        Assert.Equal(
            [
                "Landing gear", "Lights", "Night vision", "Cargo scoop", "Hardpoints", "Supercruise", "Flight assist",
                "Silent running", "Analysis mode", "SRV turret", "SRV handbrake", "SRV drive assist",
            ],
            SwitchValidation.Assignable.Select(action => action.DisplayName));
    }
}
