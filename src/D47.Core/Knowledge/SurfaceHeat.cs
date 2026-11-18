namespace D47.Core.Knowledge;

/// <summary>When a body's surface temperature is hot enough to hurt a Commander on foot.</summary>
public static class SurfaceHeat
{
    /// <summary>The surface temperature, in kelvin, from which a landable body is marked hostile.</summary>
    public const double HostileKelvin = 700;

    /// <summary>True for a landable body whose surface temperature is at least <see cref="HostileKelvin"/>.</summary>
    public static bool IsHostile(double? kelvin, bool landable) => landable && kelvin >= HostileKelvin;
}
