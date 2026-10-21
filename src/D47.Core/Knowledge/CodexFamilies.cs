namespace D47.Core.Knowledge;

/// <summary>The codex families that have a placeholder picture, by the journal's <c>SubCategory</c> symbol.</summary>
public static class CodexFamilies
{
    private static readonly Dictionary<string, string> BySymbol = new(StringComparer.OrdinalIgnoreCase)
    {
        ["$Codex_SubCategory_Stars;"] = "stars.jpg",
        ["$Codex_SubCategory_Gas_Giants;"] = "gas-giant-planets.jpg",
        ["$Codex_SubCategory_Terrestrials;"] = "terrestrial-planets.jpg",
        ["$Codex_SubCategory_Geology_and_Anomalies;"] = "geology-and-anomalies.jpg",
        ["$Codex_SubCategory_Organic_Structures;"] = "organic-structures.jpg",
        ["$Codex_SubCategory_Guardian;"] = "guardian-objects.jpg",
    };

    private static readonly Dictionary<string, string> ByTableName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gas giant planets"] = "gas-giant-planets.jpg",
        ["Geology and anomalies"] = "geology-and-anomalies.jpg",
        ["Organic structures"] = "organic-structures.jpg",
        ["Guardian objects"] = "guardian-objects.jpg",
    };

    /// <summary>The placeholder file for a journal <c>SubCategory</c> symbol; null for a symbol outside the families.</summary>
    public static string? FileOfSymbol(string? symbol) =>
        symbol is not null && BySymbol.TryGetValue(symbol.Trim(), out var file) ? file : null;

    /// <summary>The placeholder file for a subcategory name in the codex table; null for a name outside the families.</summary>
    public static string? FileOfTableName(string? name) =>
        name is not null && ByTableName.TryGetValue(name.Trim(), out var file) ? file : null;
}
