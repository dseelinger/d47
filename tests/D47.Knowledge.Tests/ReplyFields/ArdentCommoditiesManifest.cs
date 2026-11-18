using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class ArdentCommoditiesManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "ardent-commodities-full",
        document => ArdentCommodityService.ReadCommodities(document),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["[].commodityName"] = To("[].Name"),
        ["[].rare"] = To("[].Rare"),
        ["[].rareMarketId"] = To("[].RareMarketId"),
        ["[].rareMaxCount"] = To("[].RareMaxCount"),
        ["[].minBuyPrice"] = To("[].MinBuyPrice"),
        ["[].maxBuyPrice"] = To("[].MaxBuyPrice"),
        ["[].avgBuyPrice"] = To("[].AvgBuyPrice"),
        ["[].totalStock"] = To("[].TotalStock"),
        ["[].minSellPrice"] = To("[].MinSellPrice"),
        ["[].maxSellPrice"] = To("[].MaxSellPrice"),
        ["[].avgSellPrice"] = To("[].AvgSellPrice"),
        ["[].totalDemand"] = To("[].TotalDemand"),
        ["[].timestamp"] = To("[].Timestamp"),
        });
}
