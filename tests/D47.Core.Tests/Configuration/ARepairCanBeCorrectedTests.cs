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
