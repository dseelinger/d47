namespace D47.Core.Knowledge;

/// <summary>Reads a colonisation plan from an outside planner.</summary>
public interface IColonisationPlanService
{
    /// <summary>The planner's plan for one system, or null when the planner has none.</summary>
    Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken);
}

public sealed record SystemPlan(string SystemName, IReadOnlyList<PlannedSite> Sites, DateTimeOffset FetchedAt)
{
    public IReadOnlyList<PlannedBody> Bodies { get; init; } = [];
}

/// <summary>One body of the planner's system record. Distance, radius, temperature and gravity are -1 when the planner has none.</summary>
public sealed record PlannedBody(int Num, string Name)
{
    public double? DistanceLs { get; init; }

    /// <summary>Body numbers of the parents, nearest first.</summary>
    public IReadOnlyList<int> Parents { get; init; } = [];

    /// <summary>The planner's id for the kind, such as "hmc" or "bc".</summary>
    public string? Type { get; init; }

    public string? SubType { get; init; }

    /// <summary>The planner's feature ids, such as "landable" or "rings".</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    public double? Radius { get; init; }

    public double? Temperature { get; init; }

    public double? Gravity { get; init; }
}

/// <summary>A fleet carrier linked to a project. The carrier's display name is not read.</summary>
public sealed record PlannedCarrier(long MarketId, string Name)
{
    /// <summary>Commodity symbols assigned to this carrier.</summary>
    public IReadOnlyList<string> Assign { get; init; } = [];

    public DateTimeOffset? LastRefresh { get; init; }

    /// <summary>Tonnes aboard, keyed by folded symbol.</summary>
    public IReadOnlyDictionary<string, int> Cargo { get; init; } = new Dictionary<string, int>();
}

public enum PlanStatus
{
    Plan,
    Build,
    Complete,
    Demolish,
}

public sealed record PlannedSite(string SiteId, string Name, PlanStatus Status)
{
    /// <summary>The planner's id for what is built, such as "coriolis".</summary>
    public string? BuildType { get; init; }

    public int? BodyNum { get; init; }

    public string? BodyName { get; init; }

    /// <summary>The planner's project id, needed to link to or write to the project.</summary>
    public string? BuildId { get; init; }

    /// <summary>Joins to <c>ConstructionSite.MarketId</c>.</summary>
    public long? MarketId { get; init; }

    public bool IsPrimaryPort { get; init; }

    public DateTimeOffset? Due { get; init; }

    /// <summary>Tonnes still needed, keyed by folded symbol; empty unless Status is Build.</summary>
    public IReadOnlyDictionary<string, int> Needed { get; init; } = new Dictionary<string, int>();

    /// <summary>Tonnes on the carriers linked to the project, keyed by folded symbol.</summary>
    public IReadOnlyDictionary<string, int> OnLinkedCarriers { get; init; } = new Dictionary<string, int>();

    public int LinkedCarriers { get; init; }

    public int Commanders { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public string? ETag { get; init; }

    public int? SumNeed { get; init; }

    public int? MaxNeed { get; init; }

    public bool Complete { get; init; }

    /// <summary>Commodity symbols the project marks ready.</summary>
    public IReadOnlyList<string> Ready { get; init; } = [];

    public IReadOnlyList<PlannedCarrier> Carriers { get; init; } = [];

    public string? BuildName { get; init; }

    public long? SystemAddress { get; init; }

    public string? SystemName { get; init; }

    /// <summary>The system's x, y and z in light years.</summary>
    public IReadOnlyList<double> StarPos { get; init; } = [];

    public string? FactionName { get; init; }

    public DateTimeOffset? Completed { get; init; }

    public DateTimeOffset? Started { get; init; }

    public string? BodyType { get; init; }

    public IReadOnlyList<string> BodyFeatures { get; init; } = [];

    public IReadOnlyList<string> SystemFeatures { get; init; } = [];

    public string? ReserveLevel { get; init; }
}

/// <summary>The planner could not answer.</summary>
public sealed class ColonisationPlanUnavailableException(string message) : Exception(message);
