using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>How far <em>take us out</em> got.</summary>
public enum LaunchEnding
{
    /// <summary>Nothing was pressed: not docked, a missing binding, or the panel never opened.</summary>
    Refused,

    /// <summary>The keys went in and the ship left the pad.</summary>
    Launched,

    /// <summary>The keys went in and the ship is still docked. Assume it did not work.</summary>
    StillDocked,

    /// <summary>The keys went in and d47 cannot tell either way.</summary>
    Unknown,
}

/// <param name="Ending">Which of the four happened.</param>
/// <param name="Message">The sentence to say back.</param>
public readonly record struct LaunchOutcome(LaunchEnding Ending, string Message)
{
    public bool Ok => Ending == LaunchEnding.Launched;
}

/// <summary>
/// <em>Take us out</em> (Phase 52, item 2), and the one command in the phase that is not a
/// key.
/// <para>
/// <b>Elite has no launch binding.</b> Verified against every <c>.binds</c> under Frontier's own
/// <c>ControlSchemes</c> for both product folders: there is no <c>Autolaunch</c>, no <c>Undock</c>
/// and nothing launch-shaped — the only control whose name contains "launch" is
/// <c>FireChaffLauncher</c>. Launching is a panel button rather than a control, so this is a UI
/// macro of the galaxy map kind, and it is deliberately <em>not</em> a <see cref="GameAction"/>
/// with an invented bind name: a variant naming a control Elite does not have would resolve to
/// nothing and fail as silence, which is the one outcome worse than saying no.
/// </para>
/// <para>
/// <b>As fragile as the galaxy map macro, and for the same reason.</b> It walks a menu by pressing
/// direction keys, so it depends on where that menu starts. The sequence below is the fragile part;
/// everything else is verified against the game's own status. The panel is confirmed open before a
/// single direction key is sent, because those keys typed into a cockpit instead of a panel are
/// direction inputs to a docked ship.
/// </para>
/// <para>
/// <b>Both halves of it shipped wrong, and the first hid the second</b> (#106). The gate waited for
/// <see cref="GuiFocus.InternalPanel"/> after pressing the <em>left</em> panel key — and Frontier
/// number the panels by what they are about rather than by where they are: <b>internal</b> is the
/// ship's own systems, on the <b>right</b>, and <b>external</b> is navigation and contacts, on the
/// <b>left</b>. Measured against a running game on 2026-08-28: left panel gives GuiFocus 2, right
/// panel gives 1. So the wait could never succeed, the macro refused every time, and the walk
/// underneath it was never once seen to run. It was wrong too — four presses of left and four of up
/// where the Commander's own panel wants back, down, select.
/// </para>
/// </summary>
public static class Launch
{
    /// <summary>
    /// Which <see cref="GuiFocus"/> the left panel actually reports, which is <em>not</em> the one
    /// whose name contains the word panel a reader expects.
    /// <para>
    /// <b>Here rather than at the wait, so the App cannot hold a different opinion</b> — it did,
    /// and that was #106. Frontier name the panels by subject: the <em>internal</em> panel is the
    /// ship's own systems and sits on the right; the <em>external</em> panel is navigation and
    /// contacts, which are outside the ship, and sits on the left. Measured against a running game
    /// on 2026-08-28 — left panel 2, right panel 1 — rather than reasoned from the names, because
    /// reasoning from the names is what produced the bug.
    /// </para>
    /// </summary>
    public const GuiFocus Panel = GuiFocus.ExternalPanel;

    /// <summary>
    /// The walk: back, down, select.
    /// <para>
    /// <b>The left panel is not where the launch button is</b>, which is the thing that was
    /// misunderstood twice. <em>Auto Launch</em> — or <em>Launch</em>, on a ship with no advanced
    /// docking computer — is on the station menu in the <b>centre</b>. Opening the left panel and
    /// pressing <em>back</em> is a way of arriving there: back dismisses the panel and leaves the
    /// Commander on that menu, and down then select reach the button. So the panel closing is the
    /// <em>point</em> of the first key rather than a failure of it, and a walk that treated the
    /// panel as the destination could only ever have been wrong.
    /// </para>
    /// <para>
    /// Named as a list so the pre-flight resolve and the walk cannot disagree about which actions
    /// are needed: a macro that opens the panel and then finds it has no <em>select</em> leaves a
    /// panel open over the cockpit.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> Walk = ["ui_back", "ui_down", "ui_select"];

    /// <summary>
    /// Runs the macro.
    /// </summary>
    /// <param name="actions">Bindings, status, foreground and something to press with.</param>
    /// <param name="awaitPanel">
    /// Awaits <c>GuiFocus</c> reaching (or leaving) the internal panel. True when it got there,
    /// false when it did not, null when d47 could not tell. Injected, like the galaxy map's.
    /// </param>
    /// <param name="awaitUndocked">
    /// Awaits the <see cref="StatusFlags.Docked"/> flag clearing, which is what actually says the
    /// ship left. Same three answers.
    /// </param>
    /// <param name="cancellationToken">Interruption. Everything held is released either way.</param>
    public static async Task<LaunchOutcome> RunAsync(
        ActionSurface actions,
        Func<bool, CancellationToken, Task<bool?>> awaitPanel,
        Func<CancellationToken, Task<bool?>> awaitUndocked,
        CancellationToken cancellationToken = default)
    {
        var status = actions.Status();

        if (!status.IsKnown)
        {
            return new LaunchOutcome(
                LaunchEnding.Refused,
                "I cannot see the ship's status, so I do not know whether you are docked. Nothing pressed.");
        }

        // Not docked is a refusal rather than an attempt. The same key walk in space opens a panel
        // and selects whatever happens to be at the top of it.
        if (!status.Has(StatusFlags.Docked))
        {
            return new LaunchOutcome(LaunchEnding.Refused, "You are not docked, so there is nothing to launch from.");
        }

        var binds = actions.Binds();
        var context = actions.Context;
        var resolved = new Dictionary<string, EliteBinding>(StringComparer.Ordinal);

        // All four or none, before a key is sent. A macro that opens the panel and then finds it
        // has no "select" leaves a panel open over the cockpit, which is worse than doing nothing.
        foreach (var id in Walk.Prepend("left_panel"))
        {
            if (GameActions.Find(id) is not { } action)
            {
                return new LaunchOutcome(LaunchEnding.Refused, $"There is no action called '{id}'.");
            }

            var reach = ActionReachability.Resolve(action, binds, context);

            if (!reach.IsOffered || reach.Binding is null)
            {
                return new LaunchOutcome(LaunchEnding.Refused, reach.Reason);
            }

            resolved[id] = reach.Binding;
        }

        try
        {
            // A panel that is already open is not toggled shut first.
            if (status.GuiFocus != Panel)
            {
                var opened = await actions.Input
                    .SendAsync(InputSequence.Tap(resolved["left_panel"]), cancellationToken)
                    .ConfigureAwait(false);

                if (!opened.Sent)
                {
                    return new LaunchOutcome(LaunchEnding.Refused, opened.Reason);
                }

                if (await awaitPanel(true, cancellationToken).ConfigureAwait(false) is false)
                {
                    return new LaunchOutcome(
                        LaunchEnding.Refused,
                        "I pressed the left panel key and the panel did not open, so I have not "
                        + "pressed anything else.");
                }
            }

            // Back, and then wait for the panel to actually go (#106, second report).
            //
            // <b>The panel closing is the point of pressing back, not a failure of it.</b> The left
            // panel is a way of reaching the station menu in the centre, which is where Auto Launch
            // lives; back dismisses the panel and leaves the Commander on that menu. So this is the
            // one step of the walk with a visible effect, and it is the step to wait on.
            //
            // <b>Waiting is also the timing fix.</b> All three keys used to go out as one burst,
            // and the report was that back closed the panel and down and select did nothing — sent
            // during the transition, while there was no menu to receive them. Waiting on the game
            // rather than on a guessed delay is the same answer the galaxy map macro reached.
            var back = await actions.Input
                .SendAsync(InputSequence.Tap(resolved["ui_back"]), cancellationToken)
                .ConfigureAwait(false);

            if (!back.Sent)
            {
                return new LaunchOutcome(LaunchEnding.Refused, back.Reason);
            }

            if (await awaitPanel(false, cancellationToken).ConfigureAwait(false) is false)
            {
                return new LaunchOutcome(
                    LaunchEnding.Refused,
                    "I pressed back and the panel stayed open, so I have not pressed anything else. "
                    + "Without the station menu in front of us, down and select are flight controls.");
            }

            // The two that act on the station menu. Together, because nothing between them is
            // observable in Status.json: the menu the Commander is now looking at reports the same
            // GuiFocus as the cockpit does.
            var walk = new List<InputStep>();

            walk.AddRange(InputSequence.Tap(resolved["ui_down"]));
            walk.AddRange(InputSequence.Tap(resolved["ui_select"]));

            var walked = await actions.Input.SendAsync(walk, cancellationToken).ConfigureAwait(false);

            if (!walked.Sent)
            {
                return new LaunchOutcome(LaunchEnding.Refused, walked.Reason);
            }

            // The status flag is the only honest answer. Believing the ship launched when it did
            // not is the failure that leaves a Commander talking to a docked ship.
            return await awaitUndocked(cancellationToken).ConfigureAwait(false) switch
            {
                true => new LaunchOutcome(LaunchEnding.Launched, "Taking us out."),
                false => new LaunchOutcome(
                    LaunchEnding.StillDocked,
                    "I walked the left panel and we are still docked, so assume it did not work. "
                    + "The panel may not have been where I expected it."),
                null => new LaunchOutcome(
                    LaunchEnding.Unknown,
                    "I walked the left panel but cannot tell whether we launched. Check the panel."),
            };
        }
        finally
        {
            actions.Input.ReleaseAll();
        }
    }
}
