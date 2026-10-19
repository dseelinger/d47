namespace D47.Core.Knowledge;

/// <summary>Each Power's capital system, keyed by the names in <see cref="GalaxyFilters.Powers"/>.</summary>
public static class PowerCapitals
{
    private static readonly Dictionary<string, string> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A. Lavigny-Duval"] = "Kamadhenu",
        ["Aisling Duval"] = "Cubeo",
        ["Archon Delaine"] = "Harma",
        ["Denton Patreus"] = "Eotienses",
        ["Edmund Mahon"] = "Gateway",
        ["Felicia Winters"] = "Rhea",
        ["Jerome Archer"] = "Kaushpoos",
        ["Li Yong-Rui"] = "Lembava",
        ["Nakato Kaine"] = "Tionisla",
        ["Pranav Antal"] = "Polevnic",
        ["Yuri Grom"] = "Clayakarma",
        ["Zemina Torval"] = "Synteini",
    };

    /// <summary>Every power with a capital, as spelled by <see cref="GalaxyFilters.Powers"/>.</summary>
    public static IReadOnlyCollection<string> Powers => Table.Keys;

    /// <summary>The capital of a power, or false when the name is not one of the twelve.</summary>
    public static bool TryGet(string power, out string capital) => Table.TryGetValue(power, out capital!);
}
