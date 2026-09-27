using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ABloomNeverOverflowsItsAlphaTests
{
    public static TheoryData<BloomTier> Tiers() => [BloomTier.Normal, BloomTier.High];

    [Theory]
    [MemberData(nameof(Tiers))]
    public void AtTheFixedAmountEveryAlphaIsAtMostOneAndEveryRadiusScales(BloomTier tier)
    {
        var table = BloomTiers.Table(tier);
        var stops = BloomTiers.Stops(tier);

        Assert.Equal(table.Count, stops.Count);

        for (var i = 0; i < stops.Count; i++)
        {
            Assert.InRange(stops[i].Alpha, 0, 1);
            Assert.Equal(table[i].Radius * 1.1, stops[i].Radius, 9);
        }
    }

    [Fact]
    public void TheHighTiersWidestStopIsCappedAtOne()
    {
        var stops = BloomTiers.Stops(BloomTier.High);

        // 0.95 × 1.1 would be 1.045.
        Assert.Equal(1, stops[0].Alpha);
    }

    [Fact]
    public void TheFixedAmountRaisesTheTableByATenth()
    {
        var stops = BloomTiers.Stops(BloomTier.High);

        Assert.Equal(112 * 1.1, stops[^1].Radius, 9);
        Assert.Equal(0.20 * 1.1, stops[^1].Alpha, 9);
    }

    [Fact]
    public void TheTiersHaveTheHandoffsStopCounts()
    {
        Assert.Equal(5, BloomTiers.Table(BloomTier.High).Count);
        Assert.Equal(4, BloomTiers.Table(BloomTier.Normal).Count);
    }
}
