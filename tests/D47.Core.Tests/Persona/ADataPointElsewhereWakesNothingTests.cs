using D47.Core.Persona;
using Xunit;
using static D47.Core.Tests.Persona.BeaconFixture;

namespace D47.Core.Tests.Persona;

public class ADataPointElsewhereWakesNothingTests
{
    [Fact]
    public void TheScanBeforeTheJumpWakesNothing()
    {
        var cores = GuardianCores.Asleep();
        var woke = false;
        cores.Woke += _ => woke = true;

        foreach (var journalEvent in Events().Take(BeforeTheBeacon))
        {
            cores.Apply(journalEvent);
        }

        Assert.False(cores.CoresAwake);
        Assert.False(woke);
    }

    [Fact]
    public void BeingInABeaconSystemWithoutScanningWakesNothing()
    {
        var cores = GuardianCores.Asleep();

        cores.Apply(JumpTo("IC 2391 Sector MX-T b3-6", 13872878396833));

        Assert.False(cores.CoresAwake);
    }

    [Fact]
    public void AScanWithNoSystemKnownWakesNothing()
    {
        var cores = GuardianCores.Asleep();

        cores.Apply(DataPoint());

        Assert.False(cores.CoresAwake);
    }
}
