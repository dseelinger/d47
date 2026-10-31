namespace D47.Core.Knowledge;

/// <summary>Turns a codex genus key such as <c>$Codex_Ent_Bacterial_Genus_Name;</c> into the genus name the game shows.</summary>
public static class CodexGenus
{
    private const string Prefix = "$Codex_Ent_";

    private const string Suffix = "_Genus_Name;";

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aleoids"] = "Aleoida",
        ["Bacterial"] = "Bacterium",
        ["Cactoid"] = "Cactoida",
        ["Clypeus"] = "Clypeus",
        ["Conchas"] = "Concha",
        ["Electricae"] = "Electricae",
        ["Fonticulus"] = "Fonticulua",
        ["Fumerolas"] = "Fumerola",
        ["Fungoids"] = "Fungoida",
        ["Osseus"] = "Osseus",
        ["Recepta"] = "Recepta",
        ["Shrubs"] = "Frutexa",
        ["Stratum"] = "Stratum",
        ["Tubus"] = "Tubus",
        ["Tussocks"] = "Tussock",
    };

    /// <summary>The genus name, the key's own word when the key is not in the table, or the text unchanged when it is no genus key.</summary>
    public static string Name(string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !key.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return key;
        }

        var word = key[Prefix.Length..^Suffix.Length];

        return Names.TryGetValue(word, out var name) ? name : word.Replace('_', ' ');
    }
}
