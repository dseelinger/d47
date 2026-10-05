using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Mining;

/// <summary>The material a Commander is mining for, and the percentage a rock has to hold to be worth it.</summary>
/// <param name="Material">The name as <see cref="Materials"/> spells it.</param>
/// <param name="Percent">Null where any amount of the material will do.</param>
public sealed record MiningTarget(string Material, double? Percent)
{
    /// <summary>The ring hotspot materials, plus the five prospected in rocks that are not hotspots.</summary>
    public static IReadOnlyList<string> Materials { get; } =
        [.. BodyCatalogue.RingSignals.Concat(["Gold", "Silver", "Osmium", "Palladium", "Haematite"]).Order(StringComparer.Ordinal)];

    /// <summary>The material folded the way <see cref="Callouts.ProspectedMaterial.Symbol"/> is.</summary>
    public string Symbol => SymbolOf(Material);

    public static string? Match(string spoken) => Catalogue.Match(Materials, spoken);

    /// <summary>Two catalogue names differ from the journal's symbol by more than spacing.</summary>
    public static string SymbolOf(string material) => material switch
    {
        "Low Temperature Diamonds" => "lowtemperaturediamond",
        "Void Opal" => "opal",
        _ => JournalJson.Symbol(material.Replace(" ", string.Empty, StringComparison.Ordinal)) ?? string.Empty,
    };
}
