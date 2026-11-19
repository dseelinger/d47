namespace D47.Core.Knowledge;

/// <summary>The colour variants of a codex species.</summary>
public static class CodexVariants
{
    public static IReadOnlyList<CodexEntry> Of(string species) => Of(species, CodexCatalogue.Table);

    public static IReadOnlyList<CodexEntry> Of(string species, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (CodexSpecies.Of(species) is not { } wanted)
        {
            return [];
        }

        return [.. table.All
            .Where(entry => string.Equals(CodexSpecies.Of(entry.Name), wanted, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.EntryId ?? long.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)];
    }

    /// <summary>The EntryID of a localised variant name such as "Clypeus Lacrimam - Mauve"; null when none matches.</summary>
    public static long? Named(string variant) => Named(variant, CodexCatalogue.Table);

    public static long? Named(string variant, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (string.IsNullOrWhiteSpace(variant))
        {
            return null;
        }

        var name = variant.Trim();

        return table.All
            .Where(entry => entry.EntryId is not null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.EntryId)
            .Min();
    }
}
