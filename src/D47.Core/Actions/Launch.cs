using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>How far take us out got.</summary>
public enum LaunchEnding
{
    /// <summary>Nothing was pressed: not docked, a missing binding, or the panel never opened.</summary>
    Refused,

    /// <summary>The keys went in and the ship left the pad.</summary>
    Launched,

    /// <summary>The keys went in and the ship is still docked.</summary>
    StillDocked,

    /// <summary>The keys went in and d47 cannot tell either way.</summary>
    Unknown,
}

/// <summary>
/// <param name="Ending">Which of the four happened.</param> <param name="Message">The sentence to say
/// back.</param>
/// </summary>
/// <param name="Ending">Which of the four happened.</param>
/// <param name="Message">The sentence to say back.</param>
public readonly record struct LaunchOutcome(LaunchEnding Ending, string Message)
{
    public bool Ok => Ending == LaunchEnding.Launched;
}

/// <summary>Take us out (Phase 52, item 2), and the one command in the phase that is not a key.</summary>
public static class Launch
{
    /// <summary>
    /// Which <see cref="GuiFocus"/> the left panel actually reports, which is not the one whose name
    /// contains the word panel a reader expects.
    /// </summary>
    public const GuiFocus Panel = GuiFocus.ExternalPanel;

    /// <summary>The walk: back, down, select.</summary>
    public static readonly IReadOnlyList<string> Walk = ["ui_back", "ui_down", "ui_select"];

    /// <summary>Runs the macro.</summary>
    /// <param name="actions">Bindings, status, foreground and something to press with.</param>
    /// <param name="awaitPanel">
    /// Awaits <c>GuiFocus</c> reaching (or leaving) the internal panel.
    /// </param>
    /// <param name="awaitUndocked">
    /// Awaits the <see cref="StatusFlags.Docked"/> flag clearing, which is what actually says the ship
    /// left.
    /// </param>
    /// <param name="cancellationToken">Interruption.</param>
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

        // Not docked is a refusal rather than an attempt.
        if (!status.Has(StatusFlags.Docked))
        {
            return new LaunchOutcome(LaunchEnding.Refused, "You are not docked, so there is nothing to launch from.");
        }

        var binds = actions.Binds();
        var context = actions.Context;
        var resolved = new Dictionary<string, EliteBinding>(StringComparer.Ordinal);

        // All four or none, before a key is sent.
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

        // Everything that could refuse has refused.
        actions.Acknowledge("Taking us out.");

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

            // Back, and then wait for the panel to actually go (#106, second report). The panel closing is
            // the point of pressing back, not a failure of it. The left panel is a way of reaching the
            // station menu in the centre, which is where Auto Launch lives; back dismisses the panel and
            // leaves the Commander on that menu.
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

            // The two that act on the station menu.
            var walk = new List<InputStep>();

            walk.AddRange(InputSequence.Tap(resolved["ui_down"]));
            walk.AddRange(InputSequence.Tap(resolved["ui_select"]));

            var walked = await actions.Input.SendAsync(walk, cancellationToken).ConfigureAwait(false);

            if (!walked.Sent)
            {
                return new LaunchOutcome(LaunchEnding.Refused, walked.Reason);
            }

            // The status flag is the only honest answer.
            return await awaitUndocked(cancellationToken).ConfigureAwait(false) switch
            {
                // The verdict, and no longer the acknowledgement (#158).
                true => new LaunchOutcome(LaunchEnding.Launched, "We are away."),
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
