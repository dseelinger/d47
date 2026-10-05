namespace D47.Core.Journal;

/// <summary>
/// How far a fleet carrier's tritium would carry it, from the carrier-jump formula. Based on a
/// measurement, not derived from one: #25 recorded spansh plotting Sol to Colonia (22,000 ly) at 3,524 t
/// for an empty carrier, and this formula is within 25% of that (#307).
/// </summary>
public static class CarrierFuel
{
    private const double DefaultJumpRange = 500;

    /// <summary>
    /// The range <paramref name="totalTritium"/> tonnes buys, and the whole jumps of <paramref
    /// name="jumpRange"/> ly (500 when unknown) that fit in it. Ignores the carrier growing lighter as it
    /// burns tritium, which is why the range is rough.
    /// </summary>
    public static (double RangeLy, int Jumps) RoughRange(int totalTritium, int usedSpace, double? jumpRange)
    {
        var distance = jumpRange ?? DefaultJumpRange;
        var perJump = Math.Ceiling(5 + distance * (25000 + usedSpace) / 200000);
        var jumps = (int)(totalTritium / perJump);

        return (jumps * distance, jumps);
    }

    /// <summary>The tank is low below this many full-range jumps.</summary>
    public const int ReserveJumps = 2;

    /// <summary>Tonnes burned by a jump of the maximum range at the carrier's current load, or null until the reading is whole.</summary>
    public static double? FullJumpCost(CarrierState carrier)
    {
        ArgumentNullException.ThrowIfNull(carrier);

        if (carrier.MaxJumpRange is not { } range
            || carrier.Capacity is not { } capacity
            || carrier.FreeSpace is not { } free
            || carrier.FuelLevel is not { } fuel)
        {
            return null;
        }

        var used = capacity - free;

        return 5 + range * (25_000 + used + fuel) / 200_000;
    }

    public static bool IsLow(CarrierState carrier) =>
        FullJumpCost(carrier) is { } cost && carrier.FuelLevel < cost * ReserveJumps;
}
