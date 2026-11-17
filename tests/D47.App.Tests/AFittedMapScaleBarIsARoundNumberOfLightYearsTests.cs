using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>A fitted map's scale bar is the largest 1, 2, 5, 10 … LY no more than a quarter of the shorter side (#2477).</summary>
public class AFittedMapScaleBarIsARoundNumberOfLightYearsTests
{
    [Theory]
    [InlineData(152.75, 100)]
    [InlineData(100, 100)]
    [InlineData(99.9, 50)]
    [InlineData(49, 20)]
    [InlineData(19.9, 10)]
    [InlineData(7, 5)]
    [InlineData(2, 2)]
    [InlineData(1.9, 1)]
    [InlineData(0.25, 1)]
    public void TheBarIsTheLargestRoundValueThatFits(double quarter, double expected) =>
        Assert.Equal(expected, GalaxyMapView.RoundScale(quarter));

    [Fact]
    public void ARouteSixHundredElevenLightYearsAcrossDrawsAHundredAtFiftyNinePixels()
    {
        var across = 611.0;
        var lightYears = GalaxyMapView.RoundScale(across / 4);

        Assert.Equal(100, lightYears);
        Assert.Equal(59, Math.Round(lightYears * 360 / across));
        Assert.Equal("100 LY", GalaxyMapView.ScaleWords(lightYears * 4));
    }
}
