using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class ArdentNearbyManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "ardent-nearby-gold-sol-full",
        document => ArdentCommodityService.Read(document, "gold"),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["[].bodyId"] = To("[].BodyId"),
        ["[].bodyName"] = To("[].Body"),
        ["[].buyPrice"] = To("[].Quotes{}.BuyPrice"),
        ["[].commodityName"] = Excluded("The quote is keyed by the commodity the caller asked for."),
        ["[].demand"] = To("[].Quotes{}.Demand"),
        ["[].demandBracket"] = To("[].Quotes{}.DemandBracket"),
        ["[].distance"] = Excluded("Rounded to a whole light year; d47 measures distance from coordinates."),
        ["[].distanceToArrival"] = To("[].DistanceToArrival"),
        ["[].marketId"] = To("[].MarketId"),
        ["[].maxLandingPadSize"] = To("[].HasLargePad"),
        ["[].meanPrice"] = To("[].Quotes{}.MeanPrice"),
        ["[].sellPrice"] = To("[].Quotes{}.SellPrice"),
        ["[].stationName"] = To("[].Station"),
        ["[].stationType"] = To("[].Type"),
        ["[].stock"] = To("[].Quotes{}.Supply"),
        ["[].stockBracket"] = To("[].Quotes{}.StockBracket"),
        ["[].systemAddress"] = To("[].SystemAddress"),
        ["[].systemName"] = To("[].System"),
        ["[].systemX"] = To("[].X"),
        ["[].systemY"] = To("[].Y"),
        ["[].systemZ"] = To("[].Z"),
        ["[].updatedAt"] = To("[].UpdatedAt"),
        });
}
