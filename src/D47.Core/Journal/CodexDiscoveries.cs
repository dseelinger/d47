using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>A catalogue entry for a region, with the ledger entry that logged it, or null when it is missing.</summary>
public sealed record CodexDiscoveryItem(CodexEntry Entry, int Systems, CodexLogEntry? Logged);

/// <summary>The codex entries the catalogue lists for one region against what the ledger holds.</summary>
public sealed record CodexDiscoveries(
    int RegionId,
    IReadOnlyList<CodexDiscoveryItem> Known,
    IReadOnlyList<CodexDiscoveryItem> Logged,
    IReadOnlyList<CodexDiscoveryItem> Missing,
    IReadOnlyList<CodexLogEntry> Uncovered)
{
    /// <summary>
    /// Compares the region's catalogue entries with the whole ledger. An entry is logged when its (entry, region)
    /// pair is anywhere in the ledger. Missing entries are ordered by reported systems, most first. Uncovered are
    /// the first entries logged in the region that the catalogue does not list there.
    /// </summary>
    public static CodexDiscoveries For(CodexLedger ledger, CodexTable catalogue, int regionId)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(catalogue);

        var inRegion = ledger.Entries.Where(entry => entry.RegionId == regionId).ToList();
        var first = inRegion
            .GroupBy(entry => entry.EntryId)
            .ToDictionary(group => group.Key, group => group.First());

        var known = catalogue.InRegion(regionId)
            .Select(found => new CodexDiscoveryItem(found.Entry, found.Systems, first.GetValueOrDefault(found.Entry.EntryId)))
            .ToList();
        var listed = known.Select(item => item.Entry.EntryId).ToHashSet();

        return new CodexDiscoveries(
            regionId,
            known,
            [.. known.Where(item => item.Logged is not null)],
            [.. known.Where(item => item.Logged is null)],
            [.. first.Values.Where(entry => !listed.Contains(entry.EntryId))]);
    }
}
