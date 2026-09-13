using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The naval rank and Horizons early-adoption gates coriolis-data carries on hulls (#178).</summary>
public class ANavalRankGatesAHullTests
{
    [Theory]
    [InlineData("cutter", "empire:12")]
    [InlineData("empire_trader", "empire:7")]
    [InlineData("empire_courier", "empire:3")]
    [InlineData("federation_corvette", "federation:12")]
    [InlineData("federation_gunship", "federation:7")]
    [InlineData("federation_dropship_mkii", "federation:5")]
    [InlineData("federation_dropship", "federation:3")]
    [InlineData("cobramkiv", "early-adoption")]
    public void TheGatedHullsCarryTheirRequirement(string symbol, string requirement)
    {
        Assert.Equal(requirement, EliteSpecifications.Ship(symbol)?.Requirement);
    }

    [Fact]
    public void AnUngatedHullCarriesNoRequirement()
    {
        Assert.Null(EliteSpecifications.Ship("anaconda")?.Requirement);
        Assert.Null(EliteSpecifications.Ship("krait_mkii")?.Requirement);
    }

    [Theory]
    [InlineData("Imperial Cutter", "Empire rank Duke")]
    [InlineData("Imperial Clipper", "Empire rank Baron")]
    [InlineData("Federal Corvette", "Federal navy rank Rear Admiral")]
    [InlineData("Federal Gunship", "Federal navy rank Ensign")]
    public void ARankGatedShipNamesTheRankInItsGate(string name, string gate)
    {
        var ship = AcquisitionGuide.For(name);

        Assert.NotNull(ship);
        Assert.Equal(AcquisitionKind.Ship, ship.Kind);
        Assert.Equal([AcquisitionMethod.Shipyard], ship.Methods);
        Assert.Equal(gate, ship.Gate);
    }

    [Fact]
    public void ACobraMkIVIsUnobtainableOutsideEarlyAdoption()
    {
        var cobra = AcquisitionGuide.For("Cobra MkIV");

        Assert.NotNull(cobra);
        Assert.Equal([AcquisitionMethod.Unobtainable], cobra.Methods);
        Assert.Equal("Horizons early adoption only", cobra.Gate);
    }

    [Fact]
    public void AnAnacondaCarriesNoGate()
    {
        var anaconda = AcquisitionGuide.For("Anaconda");

        Assert.NotNull(anaconda);
        Assert.Equal([AcquisitionMethod.Shipyard], anaconda.Methods);
        Assert.Null(anaconda.Gate);
    }

    [Fact]
    public void ANameTheMeasuredTableDoesNotCarryHasNoShipEntry()
    {
        // The Imperial Corsair has no shipyard.csv row yet, so it never reaches AcquisitionGuide as a
        // ship at all — carrying no Gate because it carries no entry.
        Assert.Null(AcquisitionGuide.For("Imperial Corsair"));
    }
}
