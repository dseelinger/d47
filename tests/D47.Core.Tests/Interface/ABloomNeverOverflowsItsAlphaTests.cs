using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ABloomNeverOverflowsItsAlphaTests
{
    public static TheoryData<BloomTier> Tiers() => [BloomTier.Low, BloomTier.Normal, BloomTier.High];

    [Theory]
    [MemberData(nameof(Tiers))]
    public void AtTwoAndAHalfEveryAlphaIsAtMostOneAndEveryRadiusScales(BloomTier tier)
    {
        var table = BloomTiers.Table(tier);
        var stops = BloomTiers.Stops(tier, 2.5);

        Assert.Equal(table.Count, stops.Count);

        for (var i = 0; i < stops.Count; i++)
        {
            Assert.InRange(stops[i].Alpha, 0, 1);
            Assert.Equal(table[i].Radius * 2.5, stops[i].Radius, 9);
        }
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void AtZeroNoStopIsEmitted(BloomTier tier) => Assert.Empty(BloomTiers.Stops(tier, 0));

    [Fact]
    public void AStrongAmountIsCappedAtOneAndAThirdAgainTheTable()
    {
        var stops = BloomTiers.Stops(BloomTier.Normal, 2.5);

        // 0.22 × 1.35, not 0.22 × 2.5.
        Assert.Equal(0.297, stops[^1].Alpha, 9);

        // 0.88 × 1.35 would be 1.188.
        Assert.Equal(1, stops[0].Alpha);
    }

    [Fact]
    public void TheDefaultAmountRaisesTheTableByATenth()
    {
        var stops = BloomTiers.Stops(BloomTier.High, BloomTiers.DefaultAmount);

        Assert.Equal(112 * 1.1, stops[^1].Radius, 9);
        Assert.Equal(0.20 * 1.1, stops[^1].Alpha, 9);
    }

    [Fact]
    public void TheTiersHaveTheHandoffsStopCounts()
    {
        Assert.Equal(5, BloomTiers.Table(BloomTier.High).Count);
        Assert.Equal(4, BloomTiers.Table(BloomTier.Normal).Count);
        Assert.Equal(3, BloomTiers.Table(BloomTier.Low).Count);
    }
}
