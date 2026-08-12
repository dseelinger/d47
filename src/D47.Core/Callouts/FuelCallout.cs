using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Fuel, and the specific way a Commander gets stranded (list.md Phase 8, "Fuel and range
/// safety").
/// <para>
/// The low-fuel warning is the easy half. The half that matters is the one the checklist names:
/// <b>the next star on the route is unscoopable and the one after it is out of range.</b> That
/// is not a low-fuel condition — the tank can be nearly full when it becomes true — and it is
/// invisible until the Commander is sitting at a brown dwarf with no way out.
/// </para>
/// <para>
/// The warning is unconditional rather than something the model may decide is not worth
/// mentioning. Every figure behind it is reported by the game: hop coordinates and star classes
/// come from the route file, the jump range from the Loadout event, the fuel level from
/// Status.json, and fuel actually burned per jump from the FSDJump events already seen this
/// session. Nothing is looked up and nothing is estimated from a specification table.
/// </para>
/// </summary>
public sealed class FuelCallout : ICallout
{
    public string Id => "fuel";

    private static readonly TimeSpan LowFuelCooldown = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan StrandCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Elite's own low-fuel flag trips at 25%. This is the second, louder threshold — at a
    /// quarter of that again, the Commander is in real trouble rather than merely warned.
    /// </summary>
    private const double CriticalFraction = 0.10;

    private bool _wasLowFuel;
    private bool _wasCritical;
    private string? _warnedAboutRouteFrom;

    /// <summary>
    /// Fuel actually burned on the jumps seen this session. A running figure from reported
    /// values beats a computed one: it already accounts for this ship, this loadout and the
    /// distances this Commander is actually jumping.
    /// </summary>
    private double _fuelBurned;

    private int _jumpsBurned;

    private double? AverageFuelPerJump => _jumpsBurned > 0 ? _fuelBurned / _jumpsBurned : null;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Folded even while priming — that is the point of priming. The average fuel per jump
        // is only useful because the backlog has already been counted into it by the first live
        // tick.
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "FSDJump" && journalEvent.Double("FuelUsed") is { } used and > 0)
            {
                _fuelBurned += used;
                _jumpsBurned++;
            }
        }

        if (context.State is not { } state)
        {
            yield break;
        }

        foreach (var announcement in Level(context, state))
        {
            yield return announcement;
        }

        foreach (var announcement in Route(context, state))
        {
            yield return announcement;
        }
    }

    /// <summary>How much is in the tank, warned on the edge into each threshold.</summary>
    private IEnumerable<Announcement> Level(CalloutContext context, CommanderGameState state)
    {
        var status = context.Status;

        if (!status.IsKnown || !status.InShip)
        {
            yield break;
        }

        var fraction = status.FuelFraction(state.Ship.FuelCapacity);

        // Elite's flag when the capacity is not known yet, the computed fraction when it is.
        // The flag alone cannot distinguish "low" from "nearly empty", which is the distinction
        // worth raising your voice about.
        var low = fraction is { } value ? value < 0.25 : status.Has(StatusFlags.LowFuel);
        var critical = fraction is { } critical1 && critical1 < CriticalFraction;

        if (critical && !_wasCritical && !context.IsPriming)
        {
            yield return new Announcement(
                "fuel.critical",
                fraction is { } value2
                    ? $"Fuel critical. {Math.Round(value2 * 100)} percent remaining."
                    : "Fuel critical.",
                CalloutUrgency.Urgent)
            {
                Cooldown = LowFuelCooldown,
            };
        }
        else if (low && !_wasLowFuel && !context.IsPriming)
        {
            yield return new Announcement(
                "fuel.low",
                fraction is { } value3
                    ? $"Fuel low. {Math.Round(value3 * 100)} percent in the main tank."
                    : "Fuel low.")
            {
                Cooldown = LowFuelCooldown,
            };
        }

        _wasLowFuel = low;
        _wasCritical = critical;
    }

    /// <summary>
    /// The strand case. Evaluated on arrival in a new system rather than every tick, because
    /// that is when the answer changes and the Commander still has the option of replotting.
    /// </summary>
    private IEnumerable<Announcement> Route(CalloutContext context, CommanderGameState state)
    {
        var current = state.Location.StarSystem;

        if (current is null || context.IsPriming)
        {
            yield break;
        }

        // Once per system. Without this the same warning is recomputed ten times a second for
        // as long as the Commander sits there deciding what to do about it.
        if (string.Equals(_warnedAboutRouteFrom, current, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var ahead = context.Route.Ahead(current);

        if (ahead.Count == 0)
        {
            yield break;
        }

        _warnedAboutRouteFrom = current;

        var next = ahead[0];

        // Not scoopable, and not merely unrecognised: an unknown class is reported as unknown
        // rather than as a hazard, because routing a Commander around a star that would have
        // refuelled them is its own kind of harm.
        if (next.Scoopable is not false)
        {
            yield break;
        }

        var after = ahead.Count > 1 ? ahead[1] : null;

        if (after is null)
        {
            // Last hop on the route. Worth mentioning it cannot be refuelled at, but there is no
            // onward jump to be out of range for.
            yield return new Announcement(
                "fuel.route.destination",
                $"{next.StarSystem} is {StarClasses.Speak(next.StarClass)} — no fuel there.")
            {
                Cooldown = StrandCooldown,
            };

            yield break;
        }

        var onward = next.DistanceTo(after);
        var range = state.Ship.MaxJumpRange;

        // Pure geometry against a range the game reported. This is the unambiguous case: the
        // jump after the unscoopable star is longer than the ship can jump at all.
        if (onward is { } distance && range is { } maximum && distance > maximum)
        {
            yield return new Announcement(
                "fuel.route.strand",
                $"Route warning. {next.StarSystem} is {StarClasses.Speak(next.StarClass)} and cannot be "
                + $"scooped, and the jump beyond it is {distance:0.#} light years against a maximum range "
                + $"of {maximum:0.#}. Replot before you jump.",
                CalloutUrgency.Urgent)
            {
                Cooldown = StrandCooldown,
            };

            yield break;
        }

        // The softer case: reachable in principle, but not on the fuel that would be left. Uses
        // the fuel actually burned per jump this session rather than a computed consumption
        // curve — a figure derived from what happened beats one derived from a formula.
        if (AverageFuelPerJump is { } perJump &&
            context.Status.FuelMain is { } fuel &&
            fuel - (perJump * 2) < 0)
        {
            yield return new Announcement(
                "fuel.route.tight",
                $"{next.StarSystem} is {StarClasses.Speak(next.StarClass)} and cannot be scooped. "
                + $"At {perJump:0.##} tonnes a jump you do not have fuel for the one beyond it.",
                CalloutUrgency.Urgent)
            {
                Cooldown = StrandCooldown,
            };

            yield break;
        }

        // Neither stranding condition holds, but an unscoopable next star is still worth one
        // quiet line — it is the thing that makes the two above possible.
        yield return new Announcement(
            "fuel.route.unscoopable",
            $"Next jump is {StarClasses.Speak(next.StarClass)} — no fuel at {next.StarSystem}.")
        {
            Cooldown = StrandCooldown,
        };
    }
}
