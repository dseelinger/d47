namespace D47.Core.Ships;

/// <summary>The frame shift drive's jump range formula.</summary>
public static class JumpRange
{
    /// <summary>
    /// Light years for one jump at <paramref name="mass"/> tonnes, plus a flat <paramref name="boost"/>.
    /// The caller checks that the mass, fuel per jump and multiplier are positive and the power is not zero.
    /// </summary>
    public static double At(
        double mass, double optimalMass, double maxFuelPerJump, double fuelPower, double fuelMultiplier, double boost = 0) =>
        Math.Pow(maxFuelPerJump / fuelMultiplier, 1 / fuelPower) * optimalMass / mass + boost;
}
