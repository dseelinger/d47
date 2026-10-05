using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>A joined community goal with eight hours or less left, said once per goal (#640).</summary>
public sealed class CommunityGoalExpiryCallout : ICallout
{
    public string Id => "community-goal-expiry";

    public const string Key = "community-goal.expiry";

    public static readonly TimeSpan Lead = TimeSpan.FromHours(8);

    private readonly HashSet<int> _spoken = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var goal in state.CommunityGoals.Goals)
        {
            if (!goal.IsParticipating
                || goal.IsComplete
                || !goal.IsLive(context.Now)
                || goal.Expiry is not { } expiry
                || expiry - context.Now > Lead
                || !_spoken.Add(goal.Id))
            {
                continue;
            }

            var hours = Math.Max(1, (int)(expiry - context.Now).TotalHours);
            var unit = hours == 1 ? "hour" : "hours";

            yield return new Announcement(Key, $"The {goal.Title} community goal ends in {hours} {unit}.")
            {
                Cooldown = TimeSpan.Zero,
            };
        }
    }
}
