namespace D47.Core.Knowledge;

/// <summary>Writes colonisation progress to an outside planner. Every call returns an outcome rather than throwing.</summary>
public interface IColonisationPlanWriter
{
    /// <summary>The Commander the planner holds the key for.</summary>
    Task<PlanCommander> CommanderAsync(CancellationToken cancellationToken);

    /// <summary>Absolute: what the depot says is still needed. POST api/project/{buildId}.</summary>
    Task<PlanWriteResult> NeedsAsync(string buildId, IReadOnlyDictionary<string, int> remaining, int maxNeed, CancellationToken cancellationToken);

    /// <summary>Delta: tonnes delivered. POST api/project/{buildId}/contribute/{commander}.</summary>
    Task<PlanWriteResult> ContributeAsync(string buildId, string commander, IReadOnlyDictionary<string, int> delivered, CancellationToken cancellationToken);

    /// <summary>POST api/project/{buildId}/complete.</summary>
    Task<PlanWriteResult> CompleteAsync(string buildId, CancellationToken cancellationToken);

    /// <summary>Whether the carrier is on the planner. GET api/FC/{marketId}.</summary>
    Task<PlanWriteResult> CarrierRegisteredAsync(long marketId, CancellationToken cancellationToken);

    /// <summary>Delta: tonnes into (+) or out of (−) the carrier. PATCH api/FC/{marketId}/cargo.</summary>
    Task<PlanWriteResult> CarrierMovedAsync(long marketId, IReadOnlyDictionary<string, int> moved, CancellationToken cancellationToken);
}

public enum PlanWriteOutcome
{
    Sent,
    NotOnPlanner,
    KeyRefused,
    Failed,
}

public sealed record PlanWriteResult(PlanWriteOutcome Outcome, string? Message = null);

public sealed record PlanCommander(PlanWriteOutcome Outcome, string? Name);
