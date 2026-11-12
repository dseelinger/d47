using D47.Core.Knowledge;

namespace D47.Core.Watches;

public enum WatchKind
{
    Stock,
    NewCommunityGoal,
    NextSystem,
}

public enum StockComparison
{
    Above,
    Below,
}

/// <summary>A condition the Commander set up, checked in the background.</summary>
/// <param name="Commodity">The commodity as the table spells it, such as "Soontill Relics".</param>
/// <param name="FiredForTick">The BGS tick this last notified for, in the form <see cref="Knowledge.BgsTickWatch.Format"/> writes.</param>
/// <param name="Service">A name from <see cref="StationQuery.ServiceNames"/>, for a next-system watch.</param>
/// <param name="ServiceKind">Raw, Manufactured or Encoded for a material trader; Human or Guardian for a technology broker; null for any.</param>
/// <param name="MinPad">The smallest pad the station must have: Medium, Large, or null for any.</param>
/// <param name="CheckRoute">Whether a plotted route is checked as well as the system arrived in.</param>
/// <param name="FiredSystem">The system a next-system watch last fired in.</param>
/// <param name="FiredAt">When a next-system watch last fired.</param>
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
    string? FiredForTick = null,
    string? Service = null,
    string? ServiceKind = null,
    PadSize? MinPad = null,
    bool CheckRoute = false,
    string? FiredSystem = null,
    DateTimeOffset? FiredAt = null);
