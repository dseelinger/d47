namespace D47.Knowledge;

/// <summary>One commodity of the market index's list.</summary>
internal sealed record ArdentCommodity(string Name)
{
    public bool Rare { get; init; }

    public long? RareMarketId { get; init; }

    public int? RareMaxCount { get; init; }

    public int? MinBuyPrice { get; init; }

    public int? MaxBuyPrice { get; init; }

    public int? AvgBuyPrice { get; init; }

    public long? TotalStock { get; init; }

    public int? MinSellPrice { get; init; }

    public int? MaxSellPrice { get; init; }

    public int? AvgSellPrice { get; init; }

    public long? TotalDemand { get; init; }

    public DateTimeOffset? Timestamp { get; init; }
}
