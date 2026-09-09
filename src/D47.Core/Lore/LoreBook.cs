using D47.Core.Knowledge;

namespace D47.Core.Lore;

/// <summary>
/// Everything d47 knows about a system that is not astrography — the shipped table and the Commander's
/// own additions, read as one thing (Phase 23).
/// </summary>
public sealed class LoreBook(LoreStore store)
{
    public LoreStore Store => store;

    /// <summary>Everything known about one system, shipped fact first.</summary>
    public IReadOnlyList<LoreEntry> For(long systemAddress)
    {
        var shipped = LoreDirectory.ByAddress(systemAddress);

        var mine = store.Entries
            .Where(entry => entry.SystemAddress == systemAddress)
            .OrderBy(entry => entry.AddedAt ?? DateTimeOffset.MinValue)
            .ToArray();

        if (shipped is null)
        {
            return mine;
        }

        return mine.Length == 0 ? [shipped] : [shipped, .. mine];
    }

    /// <summary>Whether there is anything at all to say about this system.</summary>
    public bool Knows(long systemAddress) => For(systemAddress).Count > 0;

    /// <summary>Records something new.</summary>
    /// <paramref name="corroborated"/>
    /// is what a lookup appeared to support at the time and is a label rather than a verdict — see <see
    /// cref="LoreTier"/>, which is also where it is written down that nothing promotes one tier to
    /// another later.
    /// </paramref>
    public LoreEntry Add(
        long systemAddress,
        string name,
        string note,
        LoreArrival arrival,
        DateTimeOffset now,
        string? frontierId = null,
        bool corroborated = false) =>
        store.Add(new LoreEntry(systemAddress, name.Trim(), note.Trim())
        {
            // Only the Commander's own hands produce anything above their own word, and even then only with a
            // lookup behind it.
            Tier = corroborated && arrival == LoreArrival.Panel ? LoreTier.Corroborated : LoreTier.Commander,
            Arrival = arrival,
            FrontierId = frontierId,
            AddedAt = now,
        });

    /// <summary>
    /// What d47 says when asked about a system rather than arriving in one — every entry, in tier
    /// order, each in its own voice.
    /// </summary>
    public string? Describe(long systemAddress, string? name = null)
    {
        var entries = For(systemAddress);

        if (entries.Count == 0)
        {
            return null;
        }

        var heading = name ?? entries[0].Name;

        return string.Join(
            " ",
            new[] { string.IsNullOrWhiteSpace(heading) ? null : $"{heading}." }
                .Concat(entries.Select(entry => entry.Spoken()))
                .Where(line => line is not null));
    }
}
