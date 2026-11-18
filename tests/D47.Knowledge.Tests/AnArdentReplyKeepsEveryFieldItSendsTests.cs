using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class AnArdentReplyKeepsEveryFieldItSendsTests
{
    [Fact]
    public void ANearbyRowKeepsItsMarketBodyAndBrackets()
    {
        using var document = Fixture.Json("ardent-nearby-gold-sol-full.json");

        var markets = ArdentCommodityService.Read(document, "gold");
        var settlement = Assert.Single(markets, market => market.Station == "Berdo Drilling Hub");
        var quote = settlement.Quote("gold")!;

        Assert.Equal(3827279360, settlement.MarketId);
        Assert.Equal(5068732573089, settlement.SystemAddress);
        Assert.Equal("LHS 2459 A 1", settlement.Body);
        Assert.Equal(5, settlement.BodyId);
        Assert.Equal(3, quote.StockBracket);
        Assert.Equal(0, quote.DemandBracket);
        Assert.Equal(47113, quote.MeanPrice);
    }

    [Fact]
    public void ANearbyRowWithoutABodyLeavesItNull()
    {
        using var document = Fixture.Json("ardent-nearby-gold-sol-full.json");

        var carrier = ArdentCommodityService.Read(document, "gold").First(market => market.Station == "G2M-BQQ");

        Assert.Null(carrier.Body);
        Assert.Null(carrier.BodyId);
        Assert.Null(carrier.Quote("gold")!.DemandBracket);
    }

    [Fact]
    public void AMarketRowKeepsItsIdAndMeanPrice()
    {
        using var document = Fixture.Json("ardent-market-gold-berdo-full.json");

        var quote = ArdentCommodityService.ReadQuote(document.RootElement);

        Assert.Equal(3827279360, quote.MarketId);
        Assert.Equal(0, quote.DemandBracket);
        Assert.Equal(47113, quote.MeanPrice);
        Assert.Equal(3, quote.StockBracket);
    }

    [Fact]
    public void TheCommodityListKeepsEachRowsStatisticsAndRareFields()
    {
        using var document = Fixture.Json("ardent-commodities-full.json");

        var commodities = ArdentCommodityService.ReadCommodities(document);
        var rare = commodities.First(commodity => commodity.Rare);

        Assert.NotNull(rare.RareMarketId);
        Assert.NotNull(rare.RareMaxCount);
        Assert.Contains(commodities, commodity => commodity.AvgBuyPrice > 0 && commodity.TotalStock > 0);
        Assert.Contains(commodities, commodity => commodity.AvgSellPrice > 0 && commodity.TotalDemand > 0);
        Assert.All(commodities, commodity => Assert.NotNull(commodity.Timestamp));
    }
}
