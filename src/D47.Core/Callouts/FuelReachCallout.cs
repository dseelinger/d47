using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>
/// Whether the fuel in the tank reaches the end of the plotted route or a place to refuel on it, for a
/// ship with a Fuel Scoop and a ship without one.
/// </summary>
/// <param name="logger">
/// Where to say that neither the journal nor the remembered loadouts can describe the ship (#337).
/// </param>
public sealed class FuelReachCallout(ILogger? logger = null) : ICallout
{
    public const string Key = "fuel.reach";

    /// <summary>Shares the fuel toggle with <see cref="FuelCallout"/>.</summary>
    public string Id => "fuel";

    /// <summary>Shorter than a jump, so the warning on the last jump is not dropped by the engine.</summary>
    private static readonly TimeSpan ReachCooldown = TimeSpan.FromSeconds(20);

    /// <summary>Fuel actually burned on the jumps seen this session in the ship being flown.</summary>
    private double _fuelBurned;

    private int _jumpsBurned;

    private int? _burnedBy;

    /// <summary>The system, destination and ship last evaluated, so the answer is worked out once for each.</summary>
    private (string System, string Destination, int? Ship)? _evaluated;

    /// <summary>The route and ship last warned about, and the jumps left when it was said; null once the shortfall clears.</summary>
    private (string Destination, int? Ship, int JumpsLeft)? _warned;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var shipId = context.State?.FlownShip.ShipId;

        // A different ship burns fuel at a different rate.
        if (shipId is not null && shipId != _burnedBy)
        {
            _burnedBy = shipId;
            _fuelBurned = 0;
            _jumpsBurned = 0;
        }

        // Folded even while priming.
        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "FSDJump" && journalEvent.Double("FuelUsed") is { } used and > 0)
            {
                _fuelBurned += used;
                _jumpsBurned++;
            }
        }

        if (context.IsPriming ||
            _jumpsBurned == 0 ||
            context.State is not { } state ||
            state.Location.StarSystem is not { } current ||
            !context.Route.IsPlotted)
        {
            yield break;
        }

        var status = context.Status;

        if (!status.IsKnown || !status.InShip || status.FuelMain is not { } fuel)
        {
            yield break;
        }

        var destination = context.Route.Hops[^1].StarSystem;

        if (_evaluated is { } done &&
            string.Equals(done.System, current, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(done.Destination, destination, StringComparison.OrdinalIgnoreCase) &&
            done.Ship == shipId)
        {
            yield break;
        }

        _evaluated = (current, destination, shipId);

        var ahead = context.Route.Ahead(current);
        var perJump = _fuelBurned / _jumpsBurned;
        var jumpsLeft = (int)Math.Floor(fuel / perJump);

        // Positive evidence only: an unread loadout is spoken to as a ship that can scoop.
        var scoopless = ahead.Count > jumpsLeft &&
            state.Fitted(ShipLoadout.FuelScoop, FlownShipLoadout.Asked(context.Events, "the plotted route"), logger) is false;
        var scoopableAt = FirstScoopable(ahead);

        // Reaching the hop at index i takes i + 1 jumps.
        var refuelInReach = ahead.Count <= jumpsLeft || (!scoopless && scoopableAt >= 0 && scoopableAt < jumpsLeft);

        if (refuelInReach)
        {
            _warned = null;
            yield break;
        }

        // Once per route and ship, and again for the last jump the fuel covers.
        if (_warned is { } warned &&
            string.Equals(warned.Destination, destination, StringComparison.OrdinalIgnoreCase) &&
            warned.Ship == shipId &&
            !(jumpsLeft < warned.JumpsLeft && jumpsLeft <= 1))
        {
            yield break;
        }

        _warned = (destination, shipId, jumpsLeft);

        var reach = jumpsLeft == 0
            ? "there is not enough fuel for another jump"
            : $"there is fuel for {Jumps(jumpsLeft)}";

        var refuel = scoopless
            ? $"the route has {Jumps(ahead.Count)} left. Replot through a station to refuel"
            : scoopableAt >= 0
                ? $"the nearest scoopable star on the route is {Jumps(scoopableAt + 1)} away. Refuel or replot"
                : "no star on the rest of the route is known to be scoopable. Refuel or replot";

        yield return new Announcement(
            Key,
            $"Fuel warning. At {perJump:0.##} tonnes a jump {reach}, and {refuel}.",
            CalloutUrgency.Urgent)
        {
            Cooldown = ReachCooldown,
        };
    }

    private static int FirstScoopable(IReadOnlyList<RouteHop> ahead)
    {
        for (var index = 0; index < ahead.Count; index++)
        {
            if (ahead[index].Scoopable is true)
            {
                return index;
            }
        }

        return -1;
    }

    private static string Jumps(int count) => count == 1 ? "1 jump" : $"{count} jumps";
}
