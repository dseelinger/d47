using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The table gives a colony distance per genus, and none where it has no value (#549).</summary>
public class AGenusCarriesItsColonyDistanceTests
{
    [Theory]
    [InlineData("Stratum", 500)]
    [InlineData("Electricae", 1000)]
    [InlineData("Radicoida", 15)]
    public void AKnownGenusReturnsItsDistance(string genus, int metres) =>
        Assert.Equal(metres, ExobiologyCatalogue.ColonyDistance(genus));

    [Fact]
    public void AGenusWithNoValueReturnsNull() =>
        Assert.Null(ExobiologyCatalogue.ColonyDistance("Nonexistentia"));

    [Fact]
    public void EveryGenusInTheTableHasADistance() =>
        Assert.All(ExobiologyCatalogue.All, entry => Assert.NotNull(entry.ColonyDistance));
}
