using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>The Guardian beacon nearest the Commander, how far it is, and whether the ship they are in can make the trip.</summary>
public sealed record BeaconReach(long Address, string System, double LightYears, bool InReach, string? Why)
{
    /// <summary>The most jumps a trip to the beacon may take for it to be in reach.</summary>
    public const int MostJumps = 20;

    /// <summary>The most jump range, in light years, a trip to the beacon asks for, however far it is.</summary>
    public const double MostRangeNeeded = 28;

    /// <summary>
    /// In reach with a fleet carrier, or within <see cref="MostJumps"/> jumps at the ship's jump range with a fuel
    /// scoop fitted. What the journal has not said counts in the Commander's favour.
    /// </summary>
    public static BeaconReach Of(StarPosition? here, ShipLoadout ship, bool carrier)
    {
        ArgumentNullException.ThrowIfNull(ship);

        var (address, system) = GuardianCores.NearestBeacon(here);
        var lightYears = GuardianCores.BeaconPositions[address].DistanceTo(here ?? StarPosition.Origin);

        if (carrier || !ship.IsKnown)
        {
            return new BeaconReach(address, system, lightYears, true, null);
        }

        var needed = RangeNeeded(lightYears);
        var tooFar = ship.MaxJumpRange is { } range && !RangeEnough(range, needed);
        var noScoop = ship.Fitted(ShipLoadout.FuelScoop) is false;

        var why = (tooFar, noScoop) switch
        {
            (true, true) => $"{Range(ship, needed)} and no fuel scoop is fitted",
            (true, false) => Range(ship, needed),
            (false, true) => "no fuel scoop is fitted",
            _ => null,
        };

        return new BeaconReach(address, system, lightYears, why is null, why);
    }

    /// <summary>
    /// The jump range, in light years, that makes a trip of <paramref name="lightYears"/> take at most <see cref="MostJumps"/>
    /// jumps, and never more than <see cref="MostRangeNeeded"/>.
    /// </summary>
    public static double RangeNeeded(double lightYears) => Math.Min(lightYears / MostJumps, MostRangeNeeded);

    public static bool RangeEnough(double range, double needed) => range > 0 && range >= needed;

    /// <summary>Whether the ship has a known jump range of at least <paramref name="needed"/> and a fuel scoop fitted; what the journal has not said counts against it.</summary>
    public static bool CanMakeTrip(ShipLoadout ship, double needed)
    {
        ArgumentNullException.ThrowIfNull(ship);

        return ship.MaxJumpRange is { } range && RangeEnough(range, needed) && ship.Fitted(ShipLoadout.FuelScoop) is true;
    }

    private static string Range(ShipLoadout ship, double needed) =>
        $"the ship's jump range of {ship.MaxJumpRange.GetValueOrDefault().ToString("0.0", CultureInfo.InvariantCulture)} light years "
        + $"is under the {needed.ToString("0.0", CultureInfo.InvariantCulture)} the trip needs";
}
