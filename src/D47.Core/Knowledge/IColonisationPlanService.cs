namespace D47.Core.Knowledge;

/// <summary>Reads a colonisation plan from an outside planner.</summary>
public interface IColonisationPlanService
{
    /// <summary>The planner's plan for one system, or null when the planner has none.</summary>
    Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken);
}

public sealed record SystemPlan(string SystemName, IReadOnlyList<PlannedSite> Sites, DateTimeOffset FetchedAt);

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
}

/// <summary>The planner could not answer.</summary>
public sealed class ColonisationPlanUnavailableException(string message) : Exception(message);
