using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Galaxy map's rail pages its layers in sevens, with a pager in the eighth slot, once more than eight are on the map (#2364).</summary>
public class TheGalaxyMapRailPagesPastEightLayersTests
{
    [Fact]
    public void EightLayersHaveNoPager()
    {
        Assert.False(GalaxyMapRailPages.Pages(8));
        Assert.Equal(1, GalaxyMapRailPages.PageCount(8));
        Assert.Equal((0, 8), GalaxyMapRailPages.Rows(8, 0));
    }

    [Fact]
    public void NineLayersMakePagesOfSevenAndTwo()
    {
        Assert.True(GalaxyMapRailPages.Pages(9));
        Assert.Equal(2, GalaxyMapRailPages.PageCount(9));
        Assert.Equal((0, 7), GalaxyMapRailPages.Rows(9, 0));
        Assert.Equal((7, 2), GalaxyMapRailPages.Rows(9, 1));
    }

    [Fact]
    public void TwelveLayersReadOneToSevenThenEightToTwelve()
    {
        Assert.Equal(("1\u20137", "of 12"), GalaxyMapRailPages.Readout(12, 0));
        Assert.Equal(("8\u201312", "of 12"), GalaxyMapRailPages.Readout(12, 1));
    }

    [Fact]
    public void ThePageHoldingTheNinthLayerIsTheSecond()
    {
        Assert.Equal(1, GalaxyMapRailPages.PageOf(9, 8));
        Assert.Equal(0, GalaxyMapRailPages.PageOf(9, 6));
        Assert.Equal(0, GalaxyMapRailPages.PageOf(8, 7));
    }

    [Fact]
    public void APageBeyondTheLastIsTheLast()
    {
        Assert.Equal((7, 2), GalaxyMapRailPages.Rows(9, 5));
    }
}
