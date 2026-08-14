namespace D47.Core.Knowledge;

/// <summary>
/// One system, cut down to what can be said out loud.
/// <para>
/// <b>The projection is the design.</b> One system record from the live service is 268 KB — every
/// body, every station, every market — and a search returning three of them is 44 KB. None of
/// that can reach a model: it would cost more than the answer is worth, bury the fact the
/// Commander asked for, and hand a hostile-input surface several hundred fields wide to a
/// component whose whole job is to be told things (architecture.md §7). So the service returns
/// this, and the raw record never leaves the implementation.
/// </para>
/// </summary>
public sealed record SystemSummary
{
    public required string Name { get; init; }

    /// <summary>Light years from the query's reference system, when there was one.</summary>
    public double? Distance { get; init; }

    public string? Allegiance { get; init; }

    public string? Government { get; init; }

    public string? PrimaryEconomy { get; init; }

    public string? Security { get; init; }

    public long? Population { get; init; }

    public bool NeedsPermit { get; init; }

    /// <summary>How many stations the service knows about. Not a list: a list is the next question.</summary>
    public int? StationCount { get; init; }
}

/// <summary>What a galaxy search produced, and how much of it was left behind.</summary>
/// <param name="Reference">
/// The system distances were measured from, as the service resolved it. Echoed back rather than
/// assumed: a Commander saying "near me" and a service defaulting to Sol are different answers to
/// the same question, and only one of them is right.
/// </param>
/// <param name="Total">
/// How many matched in total. The difference between this and <see cref="Systems"/> is what the
/// Commander was not told, and a summary that hides it reads as "there are four" when there are
/// four hundred.
/// </param>
public sealed record GalaxySearchResult(
    string? Reference,
    int Total,
    IReadOnlyList<SystemSummary> Systems);

/// <summary>
/// The seam to whatever knows about the galaxy. Core declares it and never implements it: the
/// HTTP, the host name and the response shapes live outside, which is what keeps Core free of
/// providers and lets the whole capability be tested with no network (architecture.md §8).
/// </summary>
public interface IGalaxyService
{
    /// <summary>
    /// Runs a validated search.
    /// <para>
    /// Throws nothing the caller has to catch for control flow — an unreachable service is a
    /// <see cref="GalaxyUnavailableException"/> carrying a sentence fit to say aloud, because
    /// "the galaxy service did not answer" is a state the Commander can act on and a stack trace
    /// is not.
    /// </para>
    /// </summary>
    Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The straight-line distance between two systems, in light years, computed from their
    /// coordinates rather than asked for — the service returns positions, and the arithmetic is
    /// ours so that "how far" has the same answer wherever it is asked from.
    /// </summary>
    Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken);

    /// <summary>
    /// Stations selling a named module or ship, nearest first (list.md Phase 14, "Find Nearest").
    /// </summary>
    Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The service could not answer. Carries a sentence meant for a Commander, not a developer.
/// </summary>
public sealed class GalaxyUnavailableException(string message) : Exception(message);
