using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class ArdentMarketManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "ardent-market-gold-berdo-full",
        document => ArdentCommodityService.ReadQuote(document.RootElement),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["commodityName"] = Excluded("The request names the commodity."),
        ["marketId"] = To("MarketId"),
        ["buyPrice"] = To("BuyPrice"),
        ["demand"] = To("Demand"),
        ["demandBracket"] = To("DemandBracket"),
        ["meanPrice"] = To("MeanPrice"),
        ["sellPrice"] = To("SellPrice"),
        ["stock"] = To("Stock"),
        ["stockBracket"] = To("StockBracket"),
        ["updatedAt"] = To("UpdatedAt"),
        });
}
