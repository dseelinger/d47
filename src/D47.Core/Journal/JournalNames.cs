using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Names for journal fields where Frontier's <c>_Localised</c> value is an unresolved <c>$key;</c>.</summary>
internal static class JournalNames
{
    /// <summary>"Dominator Suit, grade 3" from the raw symbol of a suit field, or null when it is not a suit.</summary>
    public static string? Suit(JsonElement raw, string property) =>
        OnFootCatalogue.Find(raw.String(property)) is { Kind: OnFootKind.Suit } entry ? entry.Speak() : null;

    /// <summary>The hull in a ship field: the localised name, or the table's name for the symbol.</summary>
    public static string? Hull(JsonElement raw, string property) =>
        raw.Named(property) is { } named ? EliteSpecifications.HullSaid(named) : null;

    /// <summary>The ship as the journal names it; the table's hull name only where the localised name is a placeholder.</summary>
    public static string? Ship(JsonElement raw, string property)
    {
        var localised = raw.String(property + "_Localised");

        return JournalJson.IsPlaceholder(localised) && raw.String(property) is { } symbol
            ? EliteSpecifications.HullSaid(symbol)
            : localised ?? raw.String(property);
    }

    /// <summary>Whether a <c>LoadGame</c> was made on foot, by its suit loadout id or its suit symbol.</summary>
    public static bool IsOnFoot(JsonElement raw) =>
        raw.Long("ShipID") >= OutstandingCrimes.SuitLoadoutIds
        || OnFootCatalogue.Find(raw.String("Ship")) is { Kind: OnFootKind.Suit };

    /// <summary>Whether a bounty or kill target symbol is a suit AI.</summary>
    public static bool IsSuitAi(string? symbol) =>
        symbol is not null && symbol.Contains("suitai_class", StringComparison.OrdinalIgnoreCase);

    /// <summary>The name of an experimental effect: the localised one, or the blueprint table's for the symbol.</summary>
    public static string? Experimental(JsonElement raw, string property) =>
        raw.String(property + "_Localised") is { } localised && !JournalJson.IsPlaceholder(localised)
            ? localised
            : BlueprintCatalogue.NameOf(raw.String(property)) ?? raw.String(property);
}
