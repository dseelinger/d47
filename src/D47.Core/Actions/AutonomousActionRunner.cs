using System.Collections.Concurrent;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging;

namespace D47.Core.Actions;

/// <summary>One decision, with the action that made it, waiting to be carried out.</summary>
public readonly record struct PendingAction(string Id, string Label, AutonomousDecision Decision);

/// <summary>
/// Runs the autonomous actions on each tick and queues what they decided (Phase 10,
/// items 2 and 3).
/// <para>
/// It owns the two policies that would otherwise be reimplemented differently in each action:
/// <b>an action that is switched off is never examined at all</b>, and <b>an action that
/// throws does not stop the others</b>. The first is not an optimisation — an action that is
/// off must not accumulate state either, so that switching it on mid-session starts it from
/// now rather than from whatever it has been quietly deciding.
/// </para>
/// <para>
/// Nothing here sends. <see cref="Drain"/> hands the queue to the app, which does the awaiting,
/// because the tick is synchronous and the honk holds a key for six seconds (see
/// <c>TickLoop</c>).
/// </para>
/// </summary>
public sealed class AutonomousActionRunner(ILogger<AutonomousActionRunner> logger)
{
    private readonly List<IAutonomousAction> _actions = [];
    private readonly ConcurrentQueue<PendingAction> _pending = new();

    public IReadOnlyList<IAutonomousAction> Actions => _actions;

    public AutonomousActionRunner Add(IAutonomousAction action)
    {
        _actions.Add(action);
        return this;
    }

    public void Tick(CalloutContext context)
    {
        foreach (var action in _actions)
        {
            try
            {
                // Even while priming. An action must be given the backlog so it can fold it,
                // and it is the action's job to act on none of it — the alternative is that
                // every action learns the state of the world only from the moment d47 started.
                if (!action.IsEnabled)
                {
                    continue;
                }

                var decision = action.Examine(context);

                if (decision.Acts || decision.Say is not null)
                {
                    logger.LogInformation(
                        "Autonomous action {Id} decided to act ({Steps} steps){Say}",
                        action.Id,
                        decision.Steps.Count,
                        decision.Say is null ? string.Empty : $": {decision.Say}");

                    _pending.Enqueue(new PendingAction(action.Id, action.Label, decision));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Autonomous action {Id} threw", action.Id);
            }
        }
    }

    /// <summary>Everything decided since the last drain, in the order it was decided.</summary>
    public IReadOnlyList<PendingAction> Drain()
    {
        var drained = new List<PendingAction>();

        while (_pending.TryDequeue(out var pending))
        {
            drained.Add(pending);
        }

        return drained;
    }
}
