using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>
/// How far a separation got. Three endings rather than a boolean, because "it did not work" and
/// "it never started" are different things to say and a Commander would act differently on each.
/// </summary>
public enum SeparationEnding
{
    /// <summary>Nothing was pressed. A binding was missing, or the game was not reachable.</summary>
    Refused,

    /// <summary>Boosted to the ceiling and the mass lock never cleared.</summary>
    StillMassLocked,

    /// <summary>The lock cleared and the finishing key went in.</summary>
    Away,
}

/// <param name="Ending">Which of the three happened.</param>
/// <param name="Message">The sentence to say back, whichever it was.</param>
/// <param name="Boosts">How many boosts went in. Zero when the lock was already clear.</param>
public readonly record struct SeparationOutcome(SeparationEnding Ending, string Message, int Boosts)
{
    public bool Ok => Ending == SeparationEnding.Away;
}

/// <summary>
/// The two bounds. Unbounded, a boost loop is a fuel leak with a heat signature, so both of
/// these exist to end it and both say what happened when they are reached.
/// </summary>
/// <param name="MaxBoosts">How many boosts before giving up.</param>
/// <param name="Ceiling">
/// How long before giving up, measured across the status samples' own <c>ReadAt</c> rather than
/// against a clock this reads. That keeps the rule Core is built on — no component here owns a
/// thread or reads the clock — and it is what lets the replay harness drive the whole loop at
/// any speed without the ceiling meaning something different.
/// </param>
public readonly record struct SeparationLimits(int MaxBoosts, TimeSpan Ceiling)
{
    /// <summary>
    /// Four boosts and twenty seconds. Both are deliberately generous: the cost of one boost too
    /// many is some fuel, and the cost of one too few is a command that gives up while it was
    /// working.
    /// </summary>
    public static readonly SeparationLimits Default = new(4, TimeSpan.FromSeconds(20));
}

/// <summary>
/// <em>Separate and engage</em>, and <em>separate and supercruise</em> (Phase 52, item 3).
/// Full throttle, boost until the mass lock breaks, then the finishing key.
/// <para>
/// <b>This waits on the game's own state rather than on a clock</b>, which is the whole reason it
/// is worth building rather than recording as a macro. <c>Status.json</c> already reports
/// <see cref="StatusFlags.FsdMassLocked"/>, so the loop boosts, watches the flag, and finishes the
/// moment it clears — strictly better than the galaxy map macro's fixed waits, and the shape every
/// later compound command should copy.
/// </para>
/// <para>
/// <b>All the bindings or none.</b> Resolved before a key is sent, the way the galaxy map macro
/// resolves its five: a sequence that throttles up and then finds it has no boost binding has left
/// the ship accelerating at a station with nothing to show for it. The first binding that cannot
/// be pressed stops the attempt before anything moves, and its reason is what the Commander hears.
/// </para>
/// </summary>
public static class Separation
{
    /// <summary>
    /// Runs one separation.
    /// </summary>
    /// <param name="actions">Bindings, status, foreground and something to press with.</param>
    /// <param name="finisherId">
    /// <c>hyperspace</c> or <c>supercruise</c> — the action the sequence ends on. Passed in rather
    /// than chosen here, because the two commands differ in this and nothing else.
    /// </param>
    /// <param name="nextSample">
    /// Awaits the next status sample. Injected for the same reason <c>Poll()</c> is: the tick loop
    /// supplies it in production and a test supplies a scripted stream, and neither this method nor
    /// anything it calls owns a thread.
    /// </param>
    /// <param name="limits">The two ceilings.</param>
    /// <param name="cancellationToken">Interruption. Everything held is released either way.</param>
    public static async Task<SeparationOutcome> RunAsync(
        ActionSurface actions,
        string finisherId,
        Func<CancellationToken, Task<GameStatus>> nextSample,
        SeparationLimits limits,
        CancellationToken cancellationToken = default)
    {
        var binds = actions.Binds();
        var context = actions.Context;

        // All three or none, before anything is pressed.
        var resolved = new Dictionary<string, EliteBinding>(StringComparer.Ordinal);

        foreach (var id in new[] { "throttle_full", "boost", finisherId })
        {
            if (GameActions.Find(id) is not { } action)
            {
                return new SeparationOutcome(
                    SeparationEnding.Refused, $"There is no action called '{id}'.", 0);
            }

            var reach = ActionReachability.Resolve(action, binds, context);

            if (!reach.IsOffered || reach.Binding is null)
            {
                return new SeparationOutcome(SeparationEnding.Refused, reach.Reason, 0);
            }

            resolved[id] = reach.Binding;
        }

        var status = actions.Status();

        // Without a status file there is no flag to watch, and a loop that cannot see the thing
        // it is waiting for would boost to the ceiling every time and then finish anyway. Say so
        // instead of pressing hopefully.
        if (!status.IsKnown)
        {
            return new SeparationOutcome(
                SeparationEnding.Refused,
                "I cannot see the ship's status, so I do not know when the mass lock breaks. "
                + "Nothing pressed.",
                0);
        }

        var started = status.ReadAt;
        var boosts = 0;

        try
        {
            var throttled = await actions.Input
                .SendAsync(InputSequence.Tap(resolved["throttle_full"]), cancellationToken)
                .ConfigureAwait(false);

            if (!throttled.Sent)
            {
                return new SeparationOutcome(SeparationEnding.Refused, throttled.Reason, 0);
            }

            while (status.Has(StatusFlags.FsdMassLocked))
            {
                if (boosts >= limits.MaxBoosts)
                {
                    return new SeparationOutcome(
                        SeparationEnding.StillMassLocked,
                        $"Still mass locked after {Boosts(boosts)}; you may be too close to the station. "
                        + "I have not engaged.",
                        boosts);
                }

                if (status.ReadAt - started > limits.Ceiling)
                {
                    return new SeparationOutcome(
                        SeparationEnding.StillMassLocked,
                        $"Still mass locked {limits.Ceiling.TotalSeconds:0} seconds and {Boosts(boosts)} in; "
                        + "you may be too close to the station. I have not engaged.",
                        boosts);
                }

                var boosted = await actions.Input
                    .SendAsync(InputSequence.Tap(resolved["boost"]), cancellationToken)
                    .ConfigureAwait(false);

                if (!boosted.Sent)
                {
                    return new SeparationOutcome(SeparationEnding.Refused, boosted.Reason, boosts);
                }

                boosts++;
                status = await nextSample(cancellationToken).ConfigureAwait(false);
            }

            var finished = await actions.Input
                .SendAsync(InputSequence.Tap(resolved[finisherId]), cancellationToken)
                .ConfigureAwait(false);

            if (!finished.Sent)
            {
                return new SeparationOutcome(SeparationEnding.Refused, finished.Reason, boosts);
            }

            var what = finisherId == "supercruise" ? "Supercruise" : "Frame shift drive";

            return new SeparationOutcome(
                SeparationEnding.Away,
                boosts == 0
                    ? $"{what} engaged; you were not mass locked."
                    : $"{what} engaged after {Boosts(boosts)}.",
                boosts);
        }
        finally
        {
            // Unconditional, and the reason architecture.md D4 gives: a stranded key here is a
            // throttle that will not stop. An interrupted compound command releases what it was
            // holding rather than leaving the ship accelerating.
            actions.Input.ReleaseAll();
        }
    }

    private static string Boosts(int count) => count == 1 ? "one boost" : $"{count} boosts";
}
