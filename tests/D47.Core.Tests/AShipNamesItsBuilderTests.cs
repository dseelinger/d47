using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A ship being described names who built it, and one with no known builder still reads as a sentence
/// (https://github.com/dseelinger/d47/issues/108).
/// </summary>
public class AShipNamesItsBuilderTests
{
    [Theory]
    [InlineData("anaconda", "Anaconda, by Faulcon DeLacy")]
    [InlineData("type9_military", "Type-10 Defender, by Lakon")]
    [InlineData("anacondamkii", "Anaconda Mk II, by Faulcon DeLacy")]
    [InlineData("corsair", "Corsair, by Gutamaya")]
    [InlineData("explorer_nx", "Caspian Explorer, by Zorgon Peterson")]
    [InlineData("mediumtransport01", "Lynx Highliner, by Zorgon Peterson")]
    [InlineData("smallcombat01_nx", "Kestrel Mk II, by Core Dynamics")]
    public void AMeasuredHullNamesItsBuilder(string symbol, string expected)
    {
        var hull = EliteSpecifications.Ship(symbol);

        Assert.NotNull(hull);
        Assert.Equal(expected, hull!.Described());
    }

    [Fact]
    public void AHullWithNoKnownBuilderIsJustNamed()
    {
        var hull = new ShipSpecification { Symbol = "testhull_mkx", Name = "Test Hull Mk X" };

        Assert.Null(hull.Manufacturer);
        Assert.Equal("Test Hull Mk X", hull.Described());
    }

    /// <summary>
    /// An empty column three is the same as a missing one, since the generator writes what the source
    /// had rather than deciding what it meant.
    /// </summary>
    [Fact]
    public void AnEmptyBuilderIsTreatedAsNone()
    {
        var hull = new ShipSpecification { Symbol = "x", Name = "Something", Manufacturer = "" };

        Assert.Equal("Something", hull.Described());
    }
}
