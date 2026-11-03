using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The rare table's far sell price and distance from the star (#2103).</summary>
public class ARareGoodCarriesItsFarPriceWhenOneIsKnownTests
{
    private static readonly string[] Identity = ["relics", "Relics", "Sol", "Abraham Lincoln", "128", "1", "2", "3"];

    [Fact]
    public void ARowWithAFarPriceAndADistanceReadsBoth()
    {
        var entry = RareCatalogue.Read([.. Identity, "39770", "512.5"]);

        Assert.NotNull(entry);
        Assert.Equal(39770, entry.FarSellPrice);
        Assert.Equal(512.5, entry.DistanceToArrival);
    }

    [Fact]
    public void ARowWithBlankCellsHasNeither()
    {
        var entry = RareCatalogue.Read([.. Identity, "", ""]);

        Assert.NotNull(entry);
        Assert.Null(entry.FarSellPrice);
        Assert.Null(entry.DistanceToArrival);
    }

    [Fact]
    public void TheShippedTableCarriesFarPricesForMostRares()
    {
        Assert.Contains(RareCatalogue.All, entry => entry.FarSellPrice is > 0);
        Assert.Contains(RareCatalogue.All, entry => entry.DistanceToArrival is >= 0);
    }
}
