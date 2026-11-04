namespace D47.Core.Watches;

public enum WatchKind
{
    Stock,
    NewCommunityGoal,
}

public enum StockComparison
{
    Above,
    Below,
}

/// <summary>A condition the Commander set up, checked in the background and spoken at most once per BGS tick.</summary>
/// <param name="Commodity">The commodity as the table spells it, such as "Soontill Relics".</param>
/// <param name="FiredForTick">The BGS tick this last notified for, in the form <see cref="Knowledge.BgsTickWatch.Format"/> writes.</param>
public sealed record Watch(
    Guid Id,
    string Name,
    WatchKind Kind,
    bool Enabled = true,
    long? MarketId = null,
    string? Station = null,
    string? System = null,
    string? Commodity = null,
    StockComparison? Comparison = null,
    int? Threshold = null,
    string? FiredForTick = null);
