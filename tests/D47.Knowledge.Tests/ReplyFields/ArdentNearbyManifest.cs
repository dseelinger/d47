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
        ["[].bodyId"] = Pending,
        ["[].bodyName"] = Pending,
        ["[].buyPrice"] = To("[].Quotes{}.BuyPrice"),
        ["[].commodityName"] = Pending,
        ["[].demand"] = To("[].Quotes{}.Demand"),
        ["[].demandBracket"] = Pending,
        ["[].distance"] = Excluded("Rounded to a whole light year; d47 measures distance from coordinates."),
        ["[].distanceToArrival"] = To("[].DistanceToArrival"),
        ["[].marketId"] = Pending,
        ["[].maxLandingPadSize"] = To("[].HasLargePad"),
        ["[].meanPrice"] = Pending,
        ["[].sellPrice"] = To("[].Quotes{}.SellPrice"),
        ["[].stationName"] = To("[].Station"),
        ["[].stationType"] = To("[].Type"),
        ["[].stock"] = To("[].Quotes{}.Supply"),
        ["[].stockBracket"] = Pending,
        ["[].systemAddress"] = Pending,
        ["[].systemName"] = To("[].System"),
        ["[].systemX"] = To("[].X"),
        ["[].systemY"] = To("[].Y"),
        ["[].systemZ"] = To("[].Z"),
        ["[].updatedAt"] = To("[].UpdatedAt"),
        });
}
