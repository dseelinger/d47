using D47.Core;
using Xunit;

namespace D47.Core.Tests;

public sealed class ADoubleIsReadAsTheDecimalItIsWrittenAsTests
{
    [Theory]
    [InlineData(0.05, "0.05")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.3, "0.3")]
    [InlineData(1e-5, "0.00001")]
    [InlineData(1234.5678, "1234.5678")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(1000, "1000")]
    public void ADoubleBecomesTheDecimalItIsWrittenAs(double value, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Decimals.FromDouble(value));
    }

    [Fact]
    public void ARateTimesACountCarriesNoBinaryRemainder()
    {
        Assert.Equal(0.03745m, Decimals.FromDouble(0.05) * 749 / 1000m);
    }
}
