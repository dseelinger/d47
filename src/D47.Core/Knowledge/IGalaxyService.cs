namespace D47.Core.Knowledge;

/// <summary>One system, cut down to what can be said out loud.</summary>
public sealed record SystemSummary
{
    public required string Name { get; init; }

    /// <summary>The system's id64 — what the journal writes as <c>SystemAddress</c>.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the query's reference system, when there was one.</summary>
    public double? Distance { get; init; }

    public string? Allegiance { get; init; }

    public string? Government { get; init; }

    public string? PrimaryEconomy { get; init; }

    public string? Security { get; init; }

    public long? Population { get; init; }

    public bool NeedsPermit { get; init; }

    /// <summary>How many stations the service knows about.</summary>
    public int? StationCount { get; init; }
}

/// <summary>What a galaxy search produced, and how much of it was left behind.</summary>
/// <param name="Reference">
/// The system distances were measured from, as the service resolved it.
/// </param>
/// <param name="Total">How many matched in total.</param>
public sealed record GalaxySearchResult(
    string? Reference,
    int Total,
    IReadOnlyList<SystemSummary> Systems);

/// <summary>The seam to whatever knows about the galaxy.</summary>
public interface IGalaxyService
{
    /// <summary>Runs a validated search.</summary>
    Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The straight-line distance between two systems, in light years, computed from their coordinates
    /// rather than asked for — the service returns positions, and the arithmetic is ours so that "how
    /// far" has the same answer wherever it is asked from.
    /// </summary>
    Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken);

    /// <summary>Stations selling a named module or ship, nearest first (Phase 14, "Find Nearest").</summary>
    Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Bodies matching some criteria, nearest first — the body and signal half of "Find Nearest".
    /// </summary>
    Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Every system within claim range of a reference, least populated first (Phase 18, "Find somewhere
    /// worth colonising").
    /// </summary>
    Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken);
}

/// <summary>The service could not answer.</summary>
public sealed class GalaxyUnavailableException(string message) : Exception(message);
