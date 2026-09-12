using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// The tank level, and the plotted route's unscoopable stars (Phase 8, "Fuel and range safety"). Every route
/// line is a claim about scooping; whether the fuel reaches a refuel is <see cref="FuelReachCallout"/>.
/// </summary>
/// <param name="logger">
/// Where to say that neither the journal nor the remembered loadouts can describe the ship (#337).
/// </param>
public sealed class FuelCallout(ILogger? logger = null) : ICallout
{
    public string Id => "fuel";

    private static readonly TimeSpan LowFuelCooldown = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan StrandCooldown = TimeSpan.FromMinutes(5);

    /// <summary>Elite's own low-fuel flag trips at 25%.</summary>
    private const double CriticalFraction = 0.10;

    private bool _wasLowFuel;
    private bool _wasCritical;
    private string? _warnedAboutRouteFrom;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
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

    /// <summary>An unscoopable next star, and the strand case where the jump beyond it is out of range.</summary>
    private IEnumerable<Announcement> Route(CalloutContext context, CommanderGameState state)
    {
        var current = state.Location.StarSystem;

        if (current is null || context.IsPriming)
        {
            yield break;
        }

        // Nothing here is news to a ship that cannot scoop: see the class summary.
        var asking = FlownShipLoadout.Asked(context.Events, "the plotted route");

        if (state.Fitted(ShipLoadout.FuelScoop, asking, logger) is false)
        {
            yield break;
        }

        // Once per system.
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

        // Not scoopable, and not merely unrecognised: an unknown class is reported as unknown rather than as
        // a hazard, because routing a Commander around a star that would have refuelled them is its own kind
        // of harm.
        if (next.Scoopable is not false)
        {
            yield break;
        }

        var after = ahead.Count > 1 ? ahead[1] : null;

        if (after is null)
        {
            // Last hop on the route.
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

        // Pure geometry against a range the game reported.
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

        // In range, but an unscoopable next star is still worth one quiet line.
        yield return new Announcement(
            "fuel.route.unscoopable",
            $"Next jump is {StarClasses.Speak(next.StarClass)} — no fuel at {next.StarSystem}.")
        {
            Cooldown = StrandCooldown,
        };
    }
}
