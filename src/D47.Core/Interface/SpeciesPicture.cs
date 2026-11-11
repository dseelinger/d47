using D47.Core.Knowledge;

namespace D47.Core.Interface;

/// <summary>A species picture and whether it is the picture of the body's own variant.</summary>
public sealed record SpeciesPictureChoice(CodexPictureChoice Picture, bool IsBodysOwnVariant);

public static class SpeciesPicture
{
    /// <summary>
    /// The picture for a species. The known variant's own picture is used when there is one; otherwise the
    /// species' variants in order of systems recorded in the region, then EntryID, and the first with a picture.
    /// Null when no variant has a picture.
    /// </summary>
    public static SpeciesPictureChoice? Choose(
        string species,
        long? knownEntryId,
        int? regionId,
        IReadOnlySet<CodexPictureKey> local,
        IReadOnlyList<CodexPictureRow> stock) =>
        Choose(species, knownEntryId, regionId, local, stock, CodexCatalogue.Table);

    public static SpeciesPictureChoice? Choose(
        string species,
        long? knownEntryId,
        int? regionId,
        IReadOnlySet<CodexPictureKey> local,
        IReadOnlyList<CodexPictureRow> stock,
        CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(table);

        if (knownEntryId is { } known && CodexPictures.Find(known, regionId, local, stock) is { } own)
        {
            return new SpeciesPictureChoice(own, true);
        }

        var ordered = CodexVariants.Of(species, table)
            .OrderByDescending(entry => regionId is { } region && entry.Regions.TryGetValue(region, out var systems) ? systems : 0)
            .ThenBy(entry => entry.EntryId);

        foreach (var entry in ordered)
        {
            if (CodexPictures.Find(entry.EntryId, regionId, local, stock) is { } found)
            {
                return new SpeciesPictureChoice(found, entry.EntryId == knownEntryId);
            }
        }

        return null;
    }
}
