using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AnArxOnlyHullSaysSoTests
{
    [Fact]
    public void AHullInArxEarlyAccessIsAShipyardBuyWithThatGate()
    {
        var acquisition = AcquisitionGuide.Ship(new ShipSpecification
        {
            Symbol = "newhull",
            Name = "New Hull",
            Requirement = "arx-early-access",
        });

        Assert.Equal([AcquisitionMethod.Shipyard], acquisition.Methods);
        Assert.Equal("ARX early access only", acquisition.Gate);
    }
}
