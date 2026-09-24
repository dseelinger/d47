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

    /// <summary>How long after the hold ends an <c>FSSDiscoveryScan</c> is waited for before the honk counts as missed.</summary>
    public static readonly TimeSpan Confirmation = TimeSpan.FromSeconds(3);

    /// <summary>Said when neither hold for an arrival produced an <c>FSSDiscoveryScan</c>.</summary>
    public const string DidNotTake = "The honk did not take";

    private DateTimeOffset? _armedAt;

    /// <summary>When d47 pressed the HUD toggle to reach analysis mode, until the status shows it.</summary>
    private DateTimeOffset? _switchedAt;

    /// <summary>When the latest hold was issued, until its <c>FSSDiscoveryScan</c> arrives or the wait ends.</summary>
    private DateTimeOffset? _heldAt;

    private bool _retried;

    private bool _scanned;

    /// <summary>d47 switched to analysis mode for this honk, so switches back when it is over.</summary>
    private bool _returnToCombat;

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
            _heldAt = null;
        }

        if (context.IsPriming)
        {
            return AutonomousDecision.Nothing;
        }

        if (_heldAt is { } held)
        {
            return Confirm(context, held);
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
            _returnToCombat = false;
            return AutonomousDecision.Nothing;
        }

        // Still in the tunnel, or dropped somewhere the guns do not run.
        var where = ControlContexts.Of(context.Status);

        if ((where & ControlContext.Flying) == 0)
        {
            return AutonomousDecision.Nothing;
        }

        _armedAt = null;

        // Carried over a jump made while d47 had switched to analysis mode; used only if this arrival holds.
        var returnToCombat = _returnToCombat;
        _returnToCombat = false;

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
            _returnToCombat = returnToCombat;
            return Hold(context, reach.Binding!, retry: false);
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
        _returnToCombat = true;
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
            _returnToCombat = false;
            return new AutonomousDecision([], "I could not switch to analysis mode to honk");
        }

        _switchedAt = null;
        var where = ControlContexts.Of(context.Status);

        if (GameActions.Find("primary_fire") is not { } fire
            || ActionReachability.Resolve(fire, binds(), where) is not { IsOffered: true } reach)
        {
            return Finish(context, null);
        }

        return Hold(context, reach.Binding!, retry: false);
    }

    private AutonomousDecision Hold(CalloutContext context, EliteBinding fire, bool retry)
    {
        _heldAt = context.Now;
        _retried = retry;
        _scanned = false;
        return new AutonomousDecision(InputSequence.Hold(fire, Charge));
    }

    /// <summary>
    /// Waits for the <c>FSSDiscoveryScan</c> that shows the hold worked, holds once more if it does not come
    /// while the ship is still flying in analysis mode, and says so if the second hold misses too.
    /// </summary>
    private AutonomousDecision Confirm(CalloutContext context, DateTimeOffset held)
    {
        // Includes a scan from the Commander honking by hand.
        if (context.Events.Any(e => e.Kind is "FSSDiscoveryScan"))
        {
            _scanned = true;
        }

        if (_scanned)
        {
            return context.Now - held < Charge + Settle ? AutonomousDecision.Nothing : Finish(context, null);
        }

        if (context.Now - held < Charge + Confirmation)
        {
            return AutonomousDecision.Nothing;
        }

        // Read again on this tick: the Commander may have changed HUD mode since the first hold.
        var where = ControlContexts.Of(context.Status);

        if (!_retried
            && (where & ControlContext.Flying) != 0
            && context.Status.Has(StatusFlags.AnalysisMode)
            && GameActions.Find("primary_fire") is { } fire
            && ActionReachability.Resolve(fire, binds(), where) is { IsOffered: true } reach)
        {
            return Hold(context, reach.Binding!, retry: true);
        }

        return Finish(context, DidNotTake);
    }

    /// <summary>Ends the honk for this arrival, switching back to combat mode if d47 left it.</summary>
    private AutonomousDecision Finish(CalloutContext context, string? say)
    {
        _heldAt = null;
        var back = _returnToCombat ? ReturnToCombat(context) : AutonomousDecision.Nothing;
        _returnToCombat = false;
        return new AutonomousDecision(back.Steps, say);
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
