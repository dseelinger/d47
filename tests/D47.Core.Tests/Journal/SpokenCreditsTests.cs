using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The one helper every spoken credit figure goes through: million, billion and trillion only,
/// no thousand band, and exact digits below the first band.
/// </summary>
public class SpokenCreditsTests
{
    [Theory]
    [InlineData(2_129_966_400, "2.1 billion")]
    [InlineData(913_400_000, "913.4 million")]
    [InlineData(1_000_000_000, "1 billion")]
    [InlineData(48_295, "48,295")]
    public void ABandedFigureReadsAsACommanderSaysIt(long amount, string expected)
    {
        Assert.Equal(expected, SpokenCredits.Band(amount));
    }

    [Fact]
    public void TheTurnIsAtAThousandOfTheNextUnitNeverAThousandOfThisOne()
    {
        Assert.Equal("999.9 million", SpokenCredits.Band(999_940_000));
        Assert.Equal("1 billion", SpokenCredits.Band(999_960_000));
        Assert.Equal("1 billion", SpokenCredits.Band(1_000_000_000));
    }

    [Fact]
    public void ATrillionBands()
    {
        Assert.Equal("1.2 trillion", SpokenCredits.Band(1_200_000_000_000));
    }

    [Fact]
    public void TheSignIsTheCallersBusiness()
    {
        Assert.Equal(SpokenCredits.Band(2_100_000), SpokenCredits.Band(-2_100_000));
    }
}
