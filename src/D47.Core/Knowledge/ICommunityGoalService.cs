namespace D47.Core.Knowledge;

/// <summary>
/// One community goal as an external listing reports it — the galaxy's view, with nothing in it
/// about this Commander.
/// <para>
/// <b>The projection is the design</b>, as it is for <see cref="SystemSummary"/>, and here it
/// has a second reason. One listing entry carries the goal's GalNet copy in
/// <c>goalDescriptionText</c>: several hundred words of third-party prose per goal, fetched from
/// a community site. That is untrusted input (architecture.md §7) and it is the largest such
/// surface d47 has — so it never leaves the implementation. What crosses this seam is the
/// handful of named fields below, and the one-line objective, which is the part a Commander
/// actually asked for.
/// </para>
/// </summary>
public sealed record CommunityGoalListing
{
    public required string Name { get; init; }

    public string? SystemName { get; init; }

    public string? StationName { get; init; }

    public DateTimeOffset? Expiry { get; init; }

    public int? TierReached { get; init; }

    /// <summary>
    /// The top tier, where the listing knows it.
    /// <para>
    /// <b>Zero means unknown, not zero.</b> The journals do not carry a maximum tier, so an
    /// entry whose figures came from a journal upload has <c>tierMax: 0</c> — the site's own
    /// example shows exactly that. Reading it literally would announce a goal whose top tier is
    /// zero, which is not a thing that exists.
    /// </para>
    /// </summary>
    public int? TopTier { get; init; }

    public int? Contributors { get; init; }

    public long? ContributionsTotal { get; init; }

    public bool IsComplete { get; init; }

    /// <summary>When the listing itself was last refreshed. Crowd-reported data has an age.</summary>
    public DateTimeOffset? LastUpdate { get; init; }

    /// <summary>What to hand in, in one line — "Hand in Pilots Federation Combat Bonds".</summary>
    public string? Objective { get; init; }

    /// <summary>What it pays, where the listing says. Free text, and usually empty.</summary>
    public string? Reward { get; init; }
}

/// <summary>What an external listing reported, and when it was asked.</summary>
public sealed record CommunityGoalReport(IReadOnlyList<CommunityGoalListing> Goals);

/// <summary>
/// The seam to whatever knows about community goals the Commander has not personally docked
/// next to. Core declares it and never implements it: the HTTP, the host name and the response
/// shape live outside (architecture.md §8).
/// <para>
/// It exists because the journal cannot answer the question. The board event reports the goals
/// on offer where the Commander happens to be, so ten goals turned up in thirteen months of one
/// Commander's play — the line an external source has to cover is "everywhere I have not been".
/// </para>
/// </summary>
public interface ICommunityGoalService
{
    /// <summary>
    /// Whether a key is stored. Not whether it works: only a request can answer that, and
    /// asking on startup would be a network call nobody requested.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Ongoing and recently finished goals, as the listing has them.
    /// <para>
    /// Throws <see cref="CommunityGoalsUnavailableException"/> carrying a sentence fit to say
    /// aloud — an expired key and an unreachable site are both states a Commander can act on,
    /// and a stack trace is neither.
    /// </para>
    /// </summary>
    Task<CommunityGoalReport> RecentAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The listing could not answer. Carries a sentence meant for a Commander, not a developer.
/// </summary>
public sealed class CommunityGoalsUnavailableException(string message) : Exception(message);
