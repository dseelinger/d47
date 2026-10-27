using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace D47.Core.Journal;

/// <summary>Names a planet or gas giant codex entry by what the body is, from its <c>Name</c> symbol.</summary>
public static partial class CodexBodyName
{
    private static readonly FrozenDictionary<string, string> Planets = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["High_Metal_Content"] = "high metal content world",
        ["Metal_Rich"] = "metal-rich world",
        ["Rocky"] = "rocky world",
        ["Rocky_Ice"] = "rocky ice world",
        ["Ice"] = "icy world",
        ["Water_Worlds"] = "water world",
        ["Ammonia_Worlds"] = "ammonia world",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> GasGiants = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Sudarsky_Class_I"] = "Class I gas giant",
        ["Sudarsky_Class_II"] = "Class II gas giant",
        ["Sudarsky_Class_III"] = "Class III gas giant",
        ["Sudarsky_Class_IV"] = "Class IV gas giant",
        ["Sudarsky_Class_V"] = "Class V gas giant",
        ["Giant_With_Water_Life"] = "Gas giant with water-based life",
        ["Giant_With_Ammonia_Life"] = "Gas giant with ammonia-based life",
        ["Water_Giant"] = "Water giant",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The name for a planet or gas giant symbol such as <c>$Codex_Ent_TRF_Ter_Rocky_Name;</c>, or null when it is neither.</summary>
    public static string? Of(string? symbol)
    {
        if (symbol is null || Symbol().Match(symbol.Trim()) is not { Success: true } match)
        {
            return null;
        }

        var type = match.Groups["type"].Value;
        var trf = match.Groups["kind"].Value.Equals("TRF", StringComparison.OrdinalIgnoreCase);
        var ter = match.Groups["ter"].Success;
        var noAtmos = match.Groups["noatmos"].Success;

        if (ter && noAtmos)
        {
            return null;
        }

        if (!trf && !ter && !noAtmos && GasGiants.TryGetValue(type, out var giant))
        {
            return giant;
        }

        if (!Planets.TryGetValue(type, out var planet))
        {
            return null;
        }

        var name = trf ? "Terraformable " + planet : char.ToUpperInvariant(planet[0]) + planet[1..];

        return ter ? name + " with an atmosphere" : noAtmos ? name + ", no atmosphere" : name;
    }

    [GeneratedRegex(@"^\$Codex_Ent_(?<kind>Standard|TRF)_(?<ter>Ter_)?(?<type>[A-Za-z_]+?)(?<noatmos>_No_Atmos)?_Name;$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Symbol();
}
