using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>
/// A catalogue entry for a region, with the ledger entry that logged it or, for a star or planet kind, the scan that
/// found it; both null when it is missing.
/// </summary>
public sealed record CodexDiscoveryItem(CodexEntry Entry, int Systems, CodexLogEntry? Logged, CodexFoundKind? Found = null)
{
    public bool IsOnRecord => Logged is not null || Found is not null;
}

/// <summary>The codex entries the catalogue lists for one region against what the ledger holds.</summary>
public sealed record CodexDiscoveries(
    int RegionId,
    IReadOnlyList<CodexDiscoveryItem> Known,
    IReadOnlyList<CodexDiscoveryItem> Logged,
    IReadOnlyList<CodexDiscoveryItem> Missing,
    IReadOnlyList<CodexLogEntry> Uncovered)
{
    public const string Coverage =
        "Star and planet counts are the known systems that have one; "
        + "the rest are systems where a Commander reported the entry.";

    private static readonly HashSet<string> KindFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Codex_SubCategory_Stars;",
        "$Codex_SubCategory_Gas_Giants;",
        "$Codex_SubCategory_Terrestrials;",
    };

    /// <summary>
    /// Compares the region's catalogue entries with the whole ledger. An entry is logged when its (entry, region)
    /// pair is anywhere in the ledger, and a star or planet kind when the ledger has found it in the region. Known, logged and missing entries are ordered by category A–Z, then reported
    /// systems, most first, then name. Uncovered are the first entries logged in the region that the catalogue does
    /// not list there, leaving out stars and planets with no row in the catalogue, which its kinds cover.
    /// </summary>
    public static CodexDiscoveries For(CodexLedger ledger, CodexTable catalogue, int regionId)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(catalogue);

        var inRegion = ledger.Entries.Where(entry => entry.RegionId == regionId).ToList();
        var first = inRegion
            .GroupBy(entry => CodexKeys.Of(entry.EntryId))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var known = catalogue.InRegion(regionId)
            .OrderBy(found => found.Entry.Category, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(found => found.Systems)
            .ThenBy(found => found.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(found => new CodexDiscoveryItem(
                found.Entry,
                found.Systems,
                first.GetValueOrDefault(found.Entry.Key),
                found.Entry.Kind is null ? null : ledger.Found(found.Entry.Key, regionId)))
            .ToList();
        var listed = known.Select(item => item.Entry.Key).ToHashSet(StringComparer.Ordinal);

        return new CodexDiscoveries(
            regionId,
            known,
            [.. known.Where(item => item.IsOnRecord)],
            [.. known.Where(item => !item.IsOnRecord)],
            [.. first
                .Where(pair => !listed.Contains(pair.Key) && !IsKindFamily(pair.Value, catalogue))
                .Select(pair => pair.Value)]);
    }

    private static bool IsKindFamily(CodexLogEntry logged, CodexTable catalogue) =>
        logged.SubCategorySymbol is { } symbol
        && KindFamilies.Contains(symbol.Trim())
        && catalogue.Entry(logged.EntryId) is null;
}
