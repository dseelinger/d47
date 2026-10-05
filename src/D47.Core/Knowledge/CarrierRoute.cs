using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One system on a plotted carrier route.</summary>
/// <param name="ReserveInHold">The carrier's own tritium reserve (spansh's <c>tritium_in_market</c>), not station supply.</param>
/// <param name="IsDestination">Whether spansh flags it as a requested destination.</param>
public sealed record CarrierWaypoint(
    string Name,
    double Distance,
    double DistanceToDestination,
    int FuelInTank,
    int FuelUsed,
    bool MustRestock,
    int RestockAmount,
    int ReserveInHold,
    bool HasIcyRing,
    bool IsSystemPristine,
    bool IsDestination);

public sealed record CarrierRoute(IReadOnlyList<CarrierWaypoint> Waypoints)
{
    public int TotalTritium => Waypoints.Sum(waypoint => waypoint.FuelUsed);

    public IReadOnlyList<CarrierWaypoint> RestockStops => [.. Waypoints.Where(waypoint => waypoint.MustRestock)];

    public int IcyCount => Waypoints.Count(waypoint => waypoint.HasIcyRing);

    public int PristineCount => Waypoints.Count(waypoint => waypoint.IsSystemPristine);
}

/// <summary>A carrier route to plot, with every carrier figure read from <see cref="CarrierState"/>.</summary>
public sealed record CarrierRouteQuery
{
    private CarrierRouteQuery()
    {
    }

    public required string Source { get; init; }

    /// <summary>The systems to reach in order; a return trip ends with <see cref="Source"/>.</summary>
    public required IReadOnlyList<string> Destinations { get; init; }

    public required int Capacity { get; init; }

    public required int CapacityUsed { get; init; }

    public required int TritiumStored { get; init; }

    public required int FuelLoaded { get; init; }

    public DateTimeOffset? StatsSeenAt { get; init; }

    public long? CarrierId { get; init; }

    /// <summary>Whether the hold's tritium is unread or may have changed without the journal saying.</summary>
    public bool TritiumUncertain { get; init; }

    public static bool TryFrom(
        CarrierState carrier,
        string? from,
        string to,
        bool returnTrip,
        out CarrierRouteQuery query,
        out string failure)
    {
        query = null!;
        failure = string.Empty;

        if (carrier.IsSquadron)
        {
            failure = "That is the squadron's carrier, and I only plot routes for the Commander's own.";
            return false;
        }

        if (!carrier.Owned)
        {
            failure = "I don't know of a fleet carrier the Commander owns.";
            return false;
        }

        if (carrier.Capacity is not { } capacity || carrier.FreeSpace is not { } free || carrier.FuelLevel is not { } fuel)
        {
            failure = "Open carrier management once so I can read the hold.";
            return false;
        }

        var source = (string.IsNullOrWhiteSpace(from) ? carrier.StarSystem : from)?.Trim();

        if (string.IsNullOrEmpty(source))
        {
            failure = "I don't know where the carrier is, so I need a system to start from.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            failure = "Which system should the carrier go to?";
            return false;
        }

        var used = Math.Max(capacity - free, 0);

        query = new CarrierRouteQuery
        {
            Source = source,
            Destinations = returnTrip ? [to.Trim(), source] : [to.Trim()],
            Capacity = capacity,
            CapacityUsed = used,
            TritiumStored = Math.Min(carrier.TritiumInHold ?? 0, used),
            FuelLoaded = fuel,
            StatsSeenAt = carrier.StatsSeenAt,
            CarrierId = carrier.CarrierId,
            TritiumUncertain = carrier.TritiumInHold is null || carrier.TritiumInHoldUncertain,
        };

        return true;
    }
}
