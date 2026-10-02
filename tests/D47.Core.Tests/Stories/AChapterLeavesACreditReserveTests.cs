using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AChapterLeavesACreditReserveTests
{
    [Theory]
    [InlineData(30_000_000, 60_000_000)]
    [InlineData(700_000_000, 1_200_000_000)]
    [InlineData(500_000_000, 1_000_000_000)]
    public void APurchaseNeedsItsPriceAndAReserveOfThePriceOrFiveHundredMillion(long price, long needed)
    {
        Assert.Equal(needed, ChapterFit.Needed(price));
    }

    [Fact]
    public void AFixedThresholdAboveTheReserveRuleApplies()
    {
        Assert.Equal(ChapterFit.FleetCarrierCredits, ChapterFit.CarrierBuyNeeded);
        Assert.Equal(5_500_000_000, ChapterFit.Needed(ChapterFit.FleetCarrierPrice));
    }

    [Theory]
    [InlineData(100_000_000, 50_000_000)]
    [InlineData(1_000_000_000, 500_000_000)]
    [InlineData(5_600_000_000, 5_100_000_000)]
    public void TheWriterIsToldTheMostItMayAskTheCommanderToSpend(long credits, long most)
    {
        Assert.Equal(most, ChapterFit.MostToSpend(credits));
        Assert.True(ChapterFit.Needed(most) <= credits);
    }

    [Fact]
    public void AHullTheCommanderCannotAffordIsRefusedAndAnOwnedOneIsNot()
    {
        var price = EliteSpecifications.Ship("cobramkiii")!.Cost!.Value;
        var needed = ChapterFit.Needed(price);

        Assert.NotNull(ChapterFit.BoardWhy("cobramkiii", owned: false, needed - 1));
        Assert.Null(ChapterFit.BoardWhy("cobramkiii", owned: false, needed));
        Assert.Null(ChapterFit.BoardWhy("cobramkiii", owned: true, 0));
    }

    [Fact]
    public void AHullWithNoPriceCannotBeBoughtUnlessOwned()
    {
        var unpriced = EliteSpecifications.Ships.First(ship => ship.Cost is null).Symbol;

        Assert.Contains("no price", ChapterFit.BoardWhy(unpriced, owned: false, long.MaxValue));
        Assert.Null(ChapterFit.BoardWhy(unpriced, owned: true, 0));
    }
}
