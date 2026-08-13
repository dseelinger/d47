using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>
/// Fires the discovery scanner on arriving in a system (list.md Phase 10, "TheApp honks when
/// you arrive in a system"). The first member of the autonomous-action category, and therefore
/// off by default and enabled on its own row.
/// <para>
/// <b>Elite has no honk binding.</b> The discovery scanner is a fire-group weapon: it goes off
/// when the Commander holds primary fire in analysis mode with the scanner in the current fire
/// group. So this presses <i>their</i> fire button, which on Elite's own default keyboard
/// preset is <c>Mouse_1</c> — the reason the injector reaches mouse buttons at all
/// (architecture.md D4).
/// </para>
/// <para>
/// <b>Arming and firing are separate ticks.</b> The <c>FSDJump</c> event arrives while the
/// ship is still in the witchspace tunnel, where the game has the controls and a held key goes
/// nowhere. This arms on the event and fires once the status flags say normal space, which is
/// usually two or three ticks later.
/// </para>
/// </summary>
public sealed class HonkOnArrival(Func<bool> enabled, Func<EliteBinds> binds) : IAutonomousAction
{
    /// <summary>
    /// How long the scanner needs to be held to complete its sweep. Six seconds is the charge
    /// the game asks for; a shorter hold releases early and produces no scan at all, which is
    /// indistinguishable from d47 having done nothing.
    /// </summary>
    public static readonly TimeSpan Charge = TimeSpan.FromSeconds(6);

    /// <summary>
    /// How long after the jump the honk is still wanted. A Commander who has already turned
    /// and burned for a station does not want their guns cycling ninety seconds later, and an
    /// arm that never expires is one that eventually fires at the wrong moment.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _armedAt;

    public string Id => "honk-on-arrival";

    public string Label => "the discovery scanner on arrival";

    public bool IsEnabled => enabled();

    public AutonomousDecision Examine(CalloutContext context)
    {
        // Folded even while priming, and then discarded, so a backlog of this afternoon's jumps
        // arms nothing. A honk for every system visited since lunch is a worse bug than never
        // honking at all.
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

        // Still in the tunnel, or dropped somewhere the guns do not run. Wait rather than
        // disarm: normal space is where this is going.
        if (ControlContexts.Of(context.Status) != ControlContext.NormalSpace)
        {
            return AutonomousDecision.Nothing;
        }

        _armedAt = null;

        if (GameActions.Find("primary_fire") is not { } fire)
        {
            return AutonomousDecision.Nothing;
        }

        var reach = ActionReachability.Resolve(fire, binds(), ControlContext.NormalSpace);

        if (!reach.IsOffered)
        {
            // Said rather than swallowed. Nobody asked for this, so nobody is watching for it
            // to work, and an autonomous action that silently does nothing is one the Commander
            // will believe is running for months.
            return new AutonomousDecision([], $"I could not honk: {reach.Reason}");
        }

        // The scanner only fires in analysis mode. Switching modes on the Commander's behalf
        // would be a second autonomous action wearing the first one's consent, so this says so
        // instead.
        if (!context.Status.Has(StatusFlags.AnalysisMode))
        {
            return new AutonomousDecision(
                [],
                "I did not honk: the discovery scanner only fires in analysis mode, and you are in combat mode.");
        }

        return new AutonomousDecision(InputSequence.Hold(reach.Binding!, Charge));
    }
}
