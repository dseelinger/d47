namespace D47.Core.Knowledge;

/// <summary>
/// One community goal as an external listing reports it — the galaxy's view, with nothing in it about
/// this Commander.
/// </summary>
public sealed record CommunityGoalListing
{
    public required string Name { get; init; }

    public string? SystemName { get; init; }

    public string? StationName { get; init; }

    public DateTimeOffset? Expiry { get; init; }

    public int? TierReached { get; init; }

    /// <summary>The top tier, where the listing knows it.</summary>
    public int? TopTier { get; init; }

    public int? Contributors { get; init; }

    public long? ContributionsTotal { get; init; }

    public bool IsComplete { get; init; }

    /// <summary>When the listing itself was last refreshed.</summary>
    public DateTimeOffset? LastUpdate { get; init; }

    /// <summary>What to hand in, in one line — "Hand in Pilots Federation Combat Bonds".</summary>
    public string? Objective { get; init; }

    /// <summary>What it pays, where the listing says.</summary>
    public string? Reward { get; init; }
}

/// <summary>What an external listing reported, and when it was asked.</summary>
public sealed record CommunityGoalReport(IReadOnlyList<CommunityGoalListing> Goals);

/// <summary>
/// The seam to whatever knows about community goals the Commander has not personally docked next to.
/// </summary>
public interface ICommunityGoalService
{
    /// <summary>Whether a key is stored.</summary>
    bool IsConfigured { get; }

    /// <summary>Ongoing and recently finished goals, as the listing has them.</summary>
    Task<CommunityGoalReport> RecentAsync(CancellationToken cancellationToken);
}

/// <summary>The listing could not answer.</summary>
public sealed class CommunityGoalsUnavailableException(string message) : Exception(message);
