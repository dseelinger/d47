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

    /// <summary>How long a switch to analysis mode has to show in the status before the honk is abandoned.</summary>
    public static readonly TimeSpan SwitchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Allowance past the charge before switching back, for the hold to start and finish.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(0.5);

    private DateTimeOffset? _armedAt;

    /// <summary>When d47 pressed the HUD toggle to reach analysis mode, until the status shows it.</summary>
    private DateTimeOffset? _switchedAt;

    /// <summary>When the hold was issued after a switch d47 made, until the switch back.</summary>
    private DateTimeOffset? _heldAt;

    public string Id => "honk-on-arrival";

    public string Label => "the discovery scanner on arrival";

    public bool IsEnabled => enabled();

    public AutonomousDecision Examine(CalloutContext context)
    {
        // Folded even while priming, and then discarded, so a backlog of this afternoon's jumps arms nothing.
        if (context.Events.Any(e => e.Kind is "FSDJump"))
        {
            _armedAt = context.IsPriming ? null : context.Now;
            _switchedAt = null;
        }

        if (context.IsPriming)
        {
            return AutonomousDecision.Nothing;
        }

        if (_heldAt is { } held)
        {
            return SwitchBack(context, held);
        }

        if (_switchedAt is { } switched)
        {
            return HoldOnceSwitched(context, switched);
        }

        if (_armedAt is not { } armed)
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

        if (context.Status.Has(StatusFlags.AnalysisMode))
        {
            return new AutonomousDecision(InputSequence.Hold(reach.Binding!, Charge));
        }

        // The scanner only fires in analysis mode. Switched only in supercruise, where the hardpoints are stowed.
        if (!context.Status.Has(StatusFlags.Supercruise))
        {
            return new AutonomousDecision(
                [],
                "I did not honk: the discovery scanner only fires in analysis mode, and you are in combat mode.");
        }

        var toggle = HudToggle(where);

        if (toggle is not { IsOffered: true })
        {
            return new AutonomousDecision(
                [],
                $"I did not honk: you are in combat mode, and I cannot switch to analysis mode. {toggle?.Reason}".TrimEnd());
        }

        _switchedAt = context.Now;
        return new AutonomousDecision(InputSequence.Tap(toggle.Binding!));
    }

    /// <summary>Holds on the first tick the status shows analysis mode, or gives up after <see cref="SwitchTimeout"/>.</summary>
    private AutonomousDecision HoldOnceSwitched(CalloutContext context, DateTimeOffset switched)
    {
        if (!context.Status.Has(StatusFlags.AnalysisMode))
        {
            if (context.Now - switched <= SwitchTimeout)
            {
                return AutonomousDecision.Nothing;
            }

            _switchedAt = null;
            return new AutonomousDecision([], "I could not switch to analysis mode to honk");
        }

        _switchedAt = null;
        var where = ControlContexts.Of(context.Status);

        if (GameActions.Find("primary_fire") is not { } fire
            || ActionReachability.Resolve(fire, binds(), where) is not { IsOffered: true } reach)
        {
            return ReturnToCombat(context);
        }

        _heldAt = context.Now;
        return new AutonomousDecision(InputSequence.Hold(reach.Binding!, Charge));
    }

    /// <summary>Returns to combat mode once the hold is over, if the ship is still in supercruise and analysis mode.</summary>
    private AutonomousDecision SwitchBack(CalloutContext context, DateTimeOffset held)
    {
        if (context.Now - held < Charge + Settle)
        {
            return AutonomousDecision.Nothing;
        }

        _heldAt = null;
        return ReturnToCombat(context);
    }

    /// <summary>Presses the HUD toggle only while the ship is still in supercruise and analysis mode.</summary>
    private AutonomousDecision ReturnToCombat(CalloutContext context)
    {
        if (!context.Status.Has(StatusFlags.Supercruise) || !context.Status.Has(StatusFlags.AnalysisMode))
        {
            return AutonomousDecision.Nothing;
        }

        var where = ControlContexts.Of(context.Status);

        return HudToggle(where) is { IsOffered: true } toggle
            ? new AutonomousDecision(InputSequence.Tap(toggle.Binding!))
            : AutonomousDecision.Nothing;
    }

    private ActionReach? HudToggle(ControlContext where) =>
        GameActions.Find("analysis_mode") is { } mode ? ActionReachability.Resolve(mode, binds(), where) : null;
}
