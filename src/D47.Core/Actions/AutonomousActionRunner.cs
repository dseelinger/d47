using System.Collections.Concurrent;
using D47.Core.Callouts;
using Microsoft.Extensions.Logging;

namespace D47.Core.Actions;

/// <summary>One decision, with the action that made it, waiting to be carried out.</summary>
public readonly record struct PendingAction(string Id, string Label, AutonomousDecision Decision);

/// <summary>
/// Runs the autonomous actions on each tick and queues what they decided (Phase 10, items 2 and 3).
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
                // Even while priming.
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
