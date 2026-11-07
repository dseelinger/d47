using D47.Core.Engineers;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Tests.Goals;

internal static class FirstGoalStep
{
    /// <summary>The first step <see cref="GoalClimb.Steps"/> ranks, or null when no arc can give one.</summary>
    public static GoalClimbStep? Of(
        IReadOnlyList<GoalStanding> standings,
        CommanderGameState? state,
        EngineerReport? engineers,
        Func<DrawableActivity, bool>? drawable = null) =>
        GoalClimb.Steps(standings, state, engineers, drawable).FirstOrDefault();
}
