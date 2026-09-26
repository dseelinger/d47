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

    public string? ControllingFaction { get; init; }

    /// <summary>The minor factions present, as last reported.</summary>
    public IReadOnlyList<FactionPresence> Factions { get; init; } = [];

    /// <summary>When the service last had a report of this system.</summary>
    public DateTimeOffset? ReportedAt { get; init; }
}

/// <summary>A minor faction present in a system, and its influence there as a fraction of 1.</summary>
public sealed record FactionPresence(string Name, double? Influence);

/// <summary>What a galaxy search produced, and how much of it was left behind.</summary>
/// <param name="Reference">
/// The system distances were measured from, as the service resolved it.
/// </param>
/// <param name="Total">How many matched in total.</param>
public sealed record GalaxySearchResult(
    string? Reference,
    int Total,
    IReadOnlyList<SystemSummary> Systems);

/// <summary>A body with reported biology.</summary>
/// <param name="BodyId">The journal's <c>BodyID</c>.</param>
/// <param name="LandmarkValue">What its species are worth together, in credits.</param>
public sealed record SurveyedBody(
    string Name,
    int BodyId,
    long LandmarkValue,
    IReadOnlyList<ExobiologySpecies> Species);

/// <summary>The bodies in one system whose biology has been reported, with their species.</summary>
public sealed record SystemBiology(long SystemAddress, IReadOnlyList<SurveyedBody> Bodies);

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

    /// <summary>The reported biology in a system, by id64. Empty when the service does not know the system.</summary>
    Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken);
}

/// <summary>The service could not answer.</summary>
public sealed class GalaxyUnavailableException(string message) : Exception(message);
