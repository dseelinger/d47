using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;

namespace D47.Core.Actions;

/// <summary>How far a separation got.</summary>
public enum SeparationEnding
{
    /// <summary>Nothing was pressed.</summary>
    Refused,

    /// <summary>Boosted to the ceiling and the mass lock never cleared.</summary>
    StillMassLocked,

    /// <summary>The lock cleared and the finishing key went in.</summary>
    Away,
}

/// <summary>
/// <param name="Ending">Which of the three happened.</param> <param name="Message">The sentence to say
/// back, whichever it was.</param> <param name="Boosts">How many boosts went in.
/// </summary>
/// <param name="Ending">Which of the three happened.</param>
/// <param name="Message">The sentence to say back, whichever it was.</param>
/// <param name="Boosts">How many boosts went in.</param>
public readonly record struct SeparationOutcome(SeparationEnding Ending, string Message, int Boosts)
{
    public bool Ok => Ending == SeparationEnding.Away;
}

/// <summary>The bounds.</summary>
/// <param name="MaxBoosts">How many boosts before giving up.</param>
/// <param name="Ceiling">
/// How long before giving up, measured across the status samples' own <c>ReadAt</c> rather than against
/// a clock this reads.
/// </param>
/// <param name="BoostSpacing">How long to leave between boosts, measured the same way.</param>
public readonly record struct SeparationLimits(int MaxBoosts, TimeSpan Ceiling, TimeSpan BoostSpacing)
{
    /// <summary>A boost a second for a minute.</summary>
    public static readonly SeparationLimits Default =
        new(90, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
}

/// <summary>Separate and engage, and separate and supercruise (Phase 52, item 3).</summary>
public static class Separation
{
    /// <summary>Runs one separation.</summary>
    /// <param name="actions">Bindings, status, foreground and something to press with.</param>
    /// <param name="finisherId">
    /// <c>hyperspace</c> or <c>supercruise</c> — the action the sequence ends on.
    /// </param>
    /// <param name="nextSample">Awaits the next status sample.</param>
    /// <param name="now">The wall clock, injected because no Core type reads one.</param>
    /// <param name="limits">The bounds.</param>
    /// <param name="cancellationToken">Interruption.</param>
    public static async Task<SeparationOutcome> RunAsync(
        ActionSurface actions,
        string finisherId,
        Func<CancellationToken, Task<GameStatus>> nextSample,
        Func<DateTimeOffset> now,
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

            // The hyperspace finisher, and only it, falls back to the combined FSD key when the dedicated
            // jump cannot be pressed (#344).
            var reach = id == FsdJumpReach.Hyperspace
                ? FsdJumpReach.Resolve(binds, context, actions.SystemTargeted())
                : ActionReachability.Resolve(action, binds, context);

            if (!reach.IsOffered || reach.Binding is null)
            {
                return new SeparationOutcome(SeparationEnding.Refused, reach.Reason, 0);
            }

            resolved[id] = reach.Binding;
        }

        var status = actions.Status();

        // Without a status file there is no flag to watch, and a loop that cannot see the thing it is waiting
        // for would boost to the ceiling every time and then finish anyway.
        if (!status.IsKnown)
        {
            return new SeparationOutcome(
                SeparationEnding.Refused,
                "I cannot see the ship's status, so I do not know when the mass lock breaks. "
                + "Nothing pressed.",
                0);
        }

        var started = now();
        var boosts = 0;

        // Decided once, here, and not re-asked.
        var massLocked = status.Has(StatusFlags.FsdMassLocked);

        // The gear stops all of it.
        EliteBinding? gearKey = null;

        if (status.Has(StatusFlags.LandingGearDown))
        {
            if (GameActions.Find("landing_gear") is not { } gear
                || ActionReachability.Resolve(gear, binds, context) is not { IsOffered: true, Binding: { } key })
            {
                return new SeparationOutcome(
                    SeparationEnding.Refused,
                    "The landing gear is down and I have no binding to raise it, so the drive "
                    + "will not engage. Nothing pressed.",
                    0);
            }

            gearKey = key;
        }

        // Past every refusal, and the boost loop below has a wall-clock ceiling on it — so this is the last
        // moment before a Commander could be sitting in silence wondering whether anything heard them (#158).
        actions.Acknowledge(
            finisherId == "supercruise"
                ? "Acknowledged. Preparing for supercruise."
                : "Acknowledged. Preparing for hyperspace jump.");

        try
        {
            if (gearKey is { } raise)
            {
                var raised = await actions.Input
                    .SendAsync(InputSequence.Tap(raise), cancellationToken)
                    .ConfigureAwait(false);

                if (!raised.Sent)
                {
                    return new SeparationOutcome(SeparationEnding.Refused, raised.Reason, 0);
                }
            }

            // Pips to engines, where the capacitor refills fastest.
            if (massLocked
                && GameActions.Find("power_to_engines") is { } pips
                && ActionReachability.Resolve(pips, binds, context) is { IsOffered: true, Binding: { } pipsKey })
            {
                for (var press = 0; press < PipsToMaximum; press++)
                {
                    await actions.Input
                        .SendAsync(InputSequence.Tap(pipsKey), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

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
                        "Still mass locked; you may be too close to the station. "
                        + "I have not engaged.",
                        boosts);
                }

                if (now() - started > limits.Ceiling)
                {
                    return new SeparationOutcome(
                        SeparationEnding.StillMassLocked,
                        $"Still mass locked after {limits.Ceiling.TotalSeconds:0} seconds; "
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

                // Let the distributor refill.
                var boostedAt = now();

                do
                {
                    status = await nextSample(cancellationToken).ConfigureAwait(false);

                    if (now() - started > limits.Ceiling)
                    {
                        return new SeparationOutcome(
                            SeparationEnding.StillMassLocked,
                            $"Still mass locked after {limits.Ceiling.TotalSeconds:0} seconds; "
                            + "you may be too close to the station. I have not engaged.",
                            boosts);
                    }
                }
                while (status.Has(StatusFlags.FsdMassLocked)
                       && now() - boostedAt < limits.BoostSpacing);
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
                $"{what} engaged.",
                boosts);
        }
        finally
        {
            // Unconditional: a stranded key here is a throttle that will not stop.
            actions.Input.ReleaseAll();
        }
    }

    /// <summary>Presses of <c>IncreaseEnginesPower</c> to reach four pips in engines from any split.</summary>
    private const int PipsToMaximum = 4;

}
