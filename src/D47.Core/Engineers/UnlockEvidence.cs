using D47.Core.Journal;

namespace D47.Core.Engineers;

/// <summary>
/// The readings one engineer's prerequisites are decided from — a Commander's rank, statistics,
/// reputation and contributions, and when their session started (#183).
/// </summary>
public sealed record UnlockEvidence(
    EngineerProgressState? Progress,
    RankState? Ranks,
    CareerStatistics? Statistics,
    ReputationState? Reputation,
    EngineerContributions? Contributions,
    DateTimeOffset? SessionStart)
{
    public static UnlockEvidence From(CommanderGameState? state) => new(
        state?.Engineers,
        state?.Ranks,
        state?.Statistics,
        state?.Reputation,
        state?.Contributions,
        state?.Session.StartedAt);
}
