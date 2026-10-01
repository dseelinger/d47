using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>The Guardian beacon nearest the Commander, how far it is, and whether the ship they are in can make the trip.</summary>
public sealed record BeaconReach(long Address, string System, double LightYears, bool InReach, string? Why)
{
    /// <summary>The most jumps a trip to the beacon may take for it to be in reach.</summary>
    public const int MostJumps = 20;

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

        var tooFar = ship.MaxJumpRange is { } range && (range <= 0 || Math.Ceiling(lightYears / range) > MostJumps);
        var noScoop = ship.Fitted(ShipLoadout.FuelScoop) is false;

        var why = (tooFar, noScoop) switch
        {
            (true, true) => $"{Range(ship)} and no fuel scoop is fitted",
            (true, false) => Range(ship),
            (false, true) => "no fuel scoop is fitted",
            _ => null,
        };

        return new BeaconReach(address, system, lightYears, why is null, why);
    }

    private static string Range(ShipLoadout ship) =>
        $"the ship's jump range of {ship.MaxJumpRange.GetValueOrDefault().ToString("0.0", CultureInfo.InvariantCulture)} light years "
        + $"makes it more than {MostJumps.ToString(CultureInfo.InvariantCulture)} jumps";
}
