using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AnOutfittingItemShowsItsListPriceTests
{
    private static OutfittingItem Item(string text)
    {
        Assert.True(OutfittingItem.TryParse(text, out var item, out _));

        return item;
    }

    [Fact]
    public void ASingleSpecificationGivesOnePrice()
    {
        var range = Item("1I Detailed Surface Scanner").PriceRange();

        Assert.Equal((250_000L, 250_000L), range);
        Assert.Equal("250,000 CR", Item("1I Detailed Surface Scanner").PriceText());
    }

    [Fact]
    public void MountsWithDifferentCostsGiveARange()
    {
        Assert.Equal((35_400L, 132_800L), Item("2F Pulse Laser").PriceRange());
        Assert.Equal("35,400–132,800 CR", Item("2F Pulse Laser").PriceText());
    }

    [Fact]
    public void AShipHasNoPrice()
    {
        var ship = Item("Anaconda");

        Assert.Null(ship.PriceRange());
        Assert.Equal(string.Empty, ship.PriceText());
    }

    [Fact]
    public void ABareModuleNameSpansMoreThanOneSize()
    {
        var range = Item("Frame Shift Drive").PriceRange();

        Assert.NotNull(range);
        Assert.True(range.Value.High > range.Value.Low);
    }
}
