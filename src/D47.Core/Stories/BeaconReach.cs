using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>
/// The Guardian beacon nearest the Commander, how far it is, and whether the ship they are in can make the trip.
/// <paramref name="EasierWith"/> is the jump range that makes the trip easier, set when the ship's is under it.
/// </summary>
public sealed record BeaconReach(long Address, string System, double LightYears, bool InReach, string? Why, bool Unarmed = false, bool Carrier = false, double? EasierWith = null)
{
    /// <summary>The most jumps a trip to the beacon takes with the jump range <see cref="RangeNeeded"/> gives.</summary>
    public const int MostJumps = 20;

    /// <summary>The most jump range, in light years, <see cref="RangeNeeded"/> gives for a trip to the beacon, however far it is.</summary>
    public const double MostRangeNeeded = 28;

    /// <summary>
    /// In reach with a laser or railgun fitted and either a fleet carrier or a fuel scoop. Jump range never decides it.
    /// What the journal has not said counts in the Commander's favour.
    /// </summary>
    public static BeaconReach Of(StarPosition? here, ShipLoadout ship, bool carrier)
    {
        ArgumentNullException.ThrowIfNull(ship);

        var (address, system) = GuardianCores.NearestBeacon(here);
        var lightYears = GuardianCores.BeaconPositions[address].DistanceTo(here ?? StarPosition.Origin);

        if (!ship.IsKnown)
        {
            return new BeaconReach(address, system, lightYears, true, null);
        }

        var noWeapon = ship.FittedAny(ShipLoadout.BeaconWeapons) is false;
        var parts = new List<string>();

        if (!carrier)
        {
            if (ship.Fitted(ShipLoadout.FuelScoop) is false)
            {
                parts.Add("no fuel scoop is fitted");
            }
        }

        if (noWeapon)
        {
            parts.Add(NoWeapon);
        }

        var why = parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => $"{string.Join(", ", parts.SkipLast(1))} and {parts[^1]}",
        };

        var needed = RangeNeeded(lightYears);
        double? easier = !carrier && ship.MaxJumpRange is { } range && !RangeEnough(range, needed) ? needed : null;

        return new BeaconReach(address, system, lightYears, why is null, why, noWeapon, carrier, easier);
    }

    /// <summary>
    /// The jump range, in light years, that makes a trip of <paramref name="lightYears"/> take at most <see cref="MostJumps"/>
    /// jumps, and never more than <see cref="MostRangeNeeded"/>.
    /// </summary>
    public static double RangeNeeded(double lightYears) => Math.Min(lightYears / MostJumps, MostRangeNeeded);

    public static bool RangeEnough(double range, double needed) => range > 0 && range >= needed;

    /// <summary>
    /// Whether the ship has a fuel scoop and, when <paramref name="weaponNeeded"/>, a laser or railgun fitted;
    /// what the journal has not said counts against it.
    /// </summary>
    public static bool CanMakeTrip(ShipLoadout ship, bool weaponNeeded = true)
    {
        ArgumentNullException.ThrowIfNull(ship);

        return ship.Fitted(ShipLoadout.FuelScoop) is true && (!weaponNeeded || HasWeapon(ship));
    }

    /// <summary>Whether a laser or railgun is fitted; what the journal has not said counts against it.</summary>
    public static bool HasWeapon(ShipLoadout ship)
    {
        ArgumentNullException.ThrowIfNull(ship);

        return ship.FittedAny(ShipLoadout.BeaconWeapons) is true;
    }

    private const string NoWeapon = "no laser or railgun is fitted";
}
