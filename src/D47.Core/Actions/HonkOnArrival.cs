using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>
/// Fires the discovery scanner on arriving in a system (Phase 10, "TheApp honks when you arrive in a
/// system").
/// </summary>
public sealed class HonkOnArrival(Func<bool> enabled, Func<EliteBinds> binds) : IAutonomousAction
{
    /// <summary>How long the scanner needs to be held to complete its sweep.</summary>
    public static readonly TimeSpan Charge = TimeSpan.FromSeconds(5.3);

    /// <summary>How long after the jump the honk is still wanted.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _armedAt;

    public string Id => "honk-on-arrival";

    public string Label => "the discovery scanner on arrival";

    public bool IsEnabled => enabled();

    public AutonomousDecision Examine(CalloutContext context)
    {
        // Folded even while priming, and then discarded, so a backlog of this afternoon's jumps arms nothing.
        if (context.Events.Any(e => e.Kind is "FSDJump"))
        {
            _armedAt = context.IsPriming ? null : context.Now;
        }

        if (context.IsPriming || _armedAt is not { } armed)
        {
            return AutonomousDecision.Nothing;
        }

        if (context.Now - armed > Window)
        {
            _armedAt = null;
            return AutonomousDecision.Nothing;
        }

        // Still in the tunnel, or dropped somewhere the guns do not run.
        var where = ControlContexts.Of(context.Status);

        if ((where & ControlContext.Flying) == 0)
        {
            return AutonomousDecision.Nothing;
        }

        _armedAt = null;

        if (GameActions.Find("primary_fire") is not { } fire)
        {
            return AutonomousDecision.Nothing;
        }

        var reach = ActionReachability.Resolve(fire, binds(), where);

        if (!reach.IsOffered)
        {
            // Said rather than swallowed.
            return new AutonomousDecision([], $"I could not honk: {reach.Reason}");
        }

        // The scanner only fires in analysis mode.
        if (!context.Status.Has(StatusFlags.AnalysisMode))
        {
            return new AutonomousDecision(
                [],
                "I did not honk: the discovery scanner only fires in analysis mode, and you are in combat mode.");
        }

        return new AutonomousDecision(InputSequence.Hold(reach.Binding!, Charge));
    }
}
