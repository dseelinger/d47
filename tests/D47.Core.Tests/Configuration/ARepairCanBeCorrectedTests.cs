using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A repair that shipped wrong has to be able to reach the files it stamped (Phase 11,
/// #33).
/// <para>
/// v0.6.2 wrote "the named defaults have been checked" onto files it had only half-repaired: it
/// moved George onto Warden but left it on the core the model had given it to, so two cores
/// shared one voice. v0.6.3 corrected the repair — and could not run it, because the file said
/// it already had. A flag cannot say "done, but wrongly"; a revision can.
/// </para>
/// </summary>
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
        // The shape of a file written by v0.6.2: the old flag set, and no revision — which is
        // the whole point of not reading that flag any more.
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
    /// Commander may have decided differently. Pinned so a bump is a considered edit to this
    /// number and this test, rather than something that rides along with an unrelated change.
    /// </summary>
    [Fact]
    public void TheRevisionIsTheOneThisBuildMeansToShip()
    {
        Assert.Equal(1, VoicePairing.RepairRevision);
    }
}
