using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Ranking what the market you are standing in sells against what the destination pays.</summary>
public class TheBestCargoIsWhatPaysMostAtTheDestinationTests
{
    private static MarketSnapshot Market(string station, params MarketQuote[] quotes) => new()
    {
        Station = station,
        System = "Fixture",
        Quotes = quotes.ToDictionary(quote => quote.Commodity, StringComparer.OrdinalIgnoreCase),
    };

    private static MarketQuote Sells(string commodity, int buyPrice, int supply) =>
        new(commodity) { BuyPrice = buyPrice, Supply = supply };

    private static MarketQuote Buys(string commodity, int sellPrice, int demand) =>
        new(commodity) { SellPrice = sellPrice, Demand = demand };

    [Fact]
    public void TheTwoLargestTotalsAreNamedLargestFirst()
    {
        var here = Market(
            "Home",
            Sells("Gold", 1_000, 10_000),
            Sells("Silver", 500, 10_000),
            Sells("Bauxite", 100, 10_000));

        var there = Market(
            "Away",
            Buys("Gold", 3_000, 10_000),
            Buys("Silver", 2_000, 10_000),
            Buys("Bauxite", 150, 10_000));

        var picks = BestCargo.Rank(here, [there], hold: 100);

        Assert.Equal(["Gold", "Silver"], picks.Select(pick => pick.Commodity));
        Assert.Equal(2_000, picks[0].ProfitPerTonne);
        Assert.Equal(200_000, picks[0].Total);
    }

    [Fact]
    public void AThinSupplyRanksBelowAWholeHoldOfASmallerMargin()
    {
        var here = Market("Home", Sells("Painite", 1_000, 3), Sells("Gold", 1_000, 10_000));
        var there = Market("Away", Buys("Painite", 6_000, 10_000), Buys("Gold", 2_000, 10_000));

        var picks = BestCargo.Rank(here, [there], hold: 100);

        Assert.Equal("Gold", picks[0].Commodity);
        Assert.Equal(100, picks[0].Tonnes);
        Assert.Equal(3, picks[1].Tonnes);
    }

    [Fact]
    public void TheDemandThereCapsTheTonnageTheSameWay()
    {
        var here = Market("Home", Sells("Gold", 1_000, 10_000));
        var there = Market("Away", Buys("Gold", 2_000, 40));

        var pick = Assert.Single(BestCargo.Rank(here, [there], hold: 100));

        Assert.Equal(40, pick.Tonnes);
        Assert.Equal(40_000, pick.Total);
    }

    [Fact]
    public void TheStationNamedIsTheOneThatPaysMostForThatCommodity()
    {
        var here = Market("Home", Sells("Gold", 1_000, 10_000));

        var picks = BestCargo.Rank(
            here,
            [Market("Cheap", Buys("Gold", 1_500, 10_000)), Market("Rich", Buys("Gold", 4_000, 10_000))],
            hold: 100);

        Assert.Equal("Rich", Assert.Single(picks).Station);
    }

    [Fact]
    public void ACommodityThatPaysNoMoreThanItCostsIsNotNamed()
    {
        var here = Market("Home", Sells("Gold", 2_000, 10_000));
        var there = Market("Away", Buys("Gold", 2_000, 10_000));

        Assert.Empty(BestCargo.Rank(here, [there], hold: 100));
    }

    [Fact]
    public void NothingIsNamedWhereThereIsNoStockNoPriceNoDemandOrNoHold()
    {
        var there = Market("Away", Buys("Gold", 9_000, 10_000));

        Assert.Empty(BestCargo.Rank(Market("Home", Sells("Gold", 1_000, 0)), [there], hold: 100));
        Assert.Empty(BestCargo.Rank(Market("Home", Sells("Gold", 0, 10_000)), [there], hold: 100));
        Assert.Empty(BestCargo.Rank(Market("Home", Sells("Gold", 1_000, 10_000)), [there], hold: 0));

        Assert.Empty(BestCargo.Rank(
            Market("Home", Sells("Gold", 1_000, 10_000)),
            [Market("Away", Buys("Gold", 9_000, 0))],
            hold: 100));
    }

    [Fact]
    public void TheHoldCapsTheTonnageAndSoTheTotal()
    {
        var here = Market("Home", Sells("Gold", 1_000, 10_000));
        var there = Market("Away", Buys("Gold", 2_000, 10_000));

        Assert.Equal(20_000, Assert.Single(BestCargo.Rank(here, [there], hold: 20)).Total);
        Assert.Equal(700_000, Assert.Single(BestCargo.Rank(here, [there], hold: 700)).Total);
    }
}
