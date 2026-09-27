namespace D47.Core.Journal;

/// <summary>
/// What Universal Cartographics pays for one body, by the community formula from the Frontier forums, for the
/// Odyssey client. An estimate: the journal records credits only for a whole sale.
/// </summary>
public static class CartographicValue
{
    private const double MassFactor = 0.56591828;

    /// <param name="wasDiscovered">The scan's <c>WasDiscovered</c>; false earns the first discoverer's bonus.</param>
    /// <param name="wasMapped">The scan's <c>WasMapped</c>; false earns the first mapper's multiplier.</param>
    /// <param name="mapped">Whether the Commander mapped the body with the DSS.</param>
    /// <param name="efficient">Whether the mapping used no more probes than the efficiency target.</param>
    public static long Planet(
        string planetClass,
        string? terraformState,
        double massEm,
        bool wasDiscovered,
        bool wasMapped,
        bool mapped,
        bool efficient)
    {
        var k = PlanetConstant(planetClass, terraformState is "Terraformable" or "Terraforming");

        var multiplier = !mapped ? 1
            : !wasDiscovered && !wasMapped ? 3.699622554
            : !wasMapped ? 8.0956
            : 3.3333333333;

        var value = (k + (k * MassFactor * Math.Pow(Math.Max(massEm, 0), 0.2))) * multiplier;

        if (mapped)
        {
            value += Math.Max(value * 0.3, 555);

            if (efficient)
            {
                value *= 1.25;
            }
        }

        value = Math.Max(500, value);

        if (!wasDiscovered)
        {
            value *= 2.6;
        }

        return (long)Math.Round(value);
    }

    public static long Star(string starType, double stellarMass, bool wasDiscovered)
    {
        double k = starType switch
        {
            "N" or "H" => 22628,
            "SupermassiveBlackHole" => 33.5678,
            _ when starType.StartsWith('D') => 14057,
            _ => 1200,
        };

        var value = k + (stellarMass * k / 66.25);

        return (long)Math.Round(wasDiscovered ? value : value * 2.6);
    }

    private static double PlanetConstant(string planetClass, bool terraformable) => planetClass switch
    {
        "Metal rich body" => 21790,
        "Ammonia world" => 96932,
        "Sudarsky class I gas giant" => 1656,
        "Sudarsky class II gas giant" or "High metal content body" => 9654 + (terraformable ? 100677 : 0),
        "Water world" => 64831 + (terraformable ? 116295 : 0),
        "Earthlike body" => 64831 + 116295,
        _ => 300 + (terraformable ? 93328 : 0),
    };
}
