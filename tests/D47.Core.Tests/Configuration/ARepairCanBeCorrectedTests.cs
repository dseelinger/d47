using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A repair that shipped wrong has to be able to reach the files it stamped.</summary>
public class ARepairCanBeCorrectedTests
{
    [Fact]
    public void AFileThatHasNeverBeenRepairedIsBehindThisBuild()
    {
        Assert.True(new PersonaSettings().VoicesRepaired < VoicePairing.RepairRevision);
    }

    [Fact]
    public void AFileTheBrokenRepairStampedIsStillBehindThisBuild()
    {
        // The shape of a file written by v0.6.2: the old flag set, and no revision — which is the reason for
        // not reading that flag any more.
        var stamped = new PersonaSettings { VoicesNamedChecked = true };

        Assert.True(stamped.VoicesRepaired < VoicePairing.RepairRevision);
    }

    [Fact]
    public void AFileThisBuildHasRepairedIsNotRepairedAgain()
    {
        var repaired = new PersonaSettings { VoicesRepaired = VoicePairing.RepairRevision };

        Assert.False(repaired.VoicesRepaired < VoicePairing.RepairRevision);
    }

    /// <summary>
    /// The revision is raised deliberately, one at a time, and each raise re-decides something a
    /// Commander may have decided differently.
    /// </summary>
    [Fact]
    public void TheRevisionIsTheOneThisBuildMeansToShip()
    {
        Assert.Equal(1, VoicePairing.RepairRevision);
    }
}
