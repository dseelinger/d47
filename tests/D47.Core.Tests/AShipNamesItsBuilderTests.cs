using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A ship being described names who built it, and one with no known builder still reads as a
/// sentence (https://github.com/dseelinger/d47/issues/108).
/// </summary>
public class AShipNamesItsBuilderTests
{
    [Theory]
    [InlineData("anaconda", "Anaconda, by Faulcon DeLacy")]
    [InlineData("type9_military", "Type-10 Defender, by Lakon")]
    public void AMeasuredHullNamesItsBuilder(string symbol, string expected)
    {
        var hull = EliteSpecifications.Ship(symbol);

        Assert.NotNull(hull);
        Assert.Equal(expected, hull!.Described());
    }

    /// <summary>
    /// <b>The case the issue asks to be tested against specifically</b>, because it is the newest
    /// hulls that have no measured row and therefore no column three to read a builder from — and
    /// they are the ones a Commander is most likely to be asking about. What comes out is the name
    /// on its own, not a name with a dangling "by" after it.
    /// </summary>
    [Fact]
    public void AHullWithNoKnownBuilderIsJustNamed()
    {
        var corsair = new ShipSpecification { Symbol = "corsair", Name = "Corsair" };

        Assert.Null(corsair.Manufacturer);
        Assert.Equal("Corsair", corsair.Described());
    }

    /// <summary>An empty column three is the same as a missing one, since the generator writes what
    /// the source had rather than deciding what it meant.</summary>
    [Fact]
    public void AnEmptyBuilderIsTreatedAsNone()
    {
        var hull = new ShipSpecification { Symbol = "x", Name = "Something", Manufacturer = "" };

        Assert.Equal("Something", hull.Described());
    }

    /// <summary>
    /// And the three the issue names really do have no builder in the shipped table — asserted so
    /// that this stops being true loudly, on the day the upstream source grows them, rather than
    /// leaving a test that passes while testing nothing.
    /// </summary>
    [Theory]
    [InlineData("corsair")]
    [InlineData("explorer_nx")]
    [InlineData("smallcombat01_nx")]
    public void TheThreeNewestHullsAreSilentAboutTheirBuilder(string symbol)
    {
        // Named from armour, so HullSaid answers even though the measured table does not.
        Assert.NotEqual(symbol, EliteSpecifications.HullSaid(symbol));

        Assert.Null(EliteSpecifications.Ship(symbol)?.Manufacturer);
    }
}
