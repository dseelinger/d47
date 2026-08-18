using D47.Core.Knowledge;

namespace D47.Core.Lore;

/// <summary>
/// Everything d47 knows about a system that is not astrography — the shipped table and the
/// Commander's own additions, read as one thing (list.md Phase 23).
/// <para>
/// The two halves stay distinguishable all the way to the sentence: <see cref="LoreEntry.Tier"/>
/// travels with every entry and <see cref="LoreEntry.Spoken"/> is the only place a tier turns
/// into words. Merging here and hedging later is what would put the guarantee back in the
/// persona's hands.
/// </para>
/// <para>
/// <b>A shipped row is never edited or removed by an addition.</b> A Commander who writes about
/// Sol gets a second entry, not an overwrite — the table is part of the build, and a book that
/// let local state rewrite it would ship one thing and say another.
/// </para>
/// </summary>
public sealed class LoreBook(LoreStore store)
{
    public LoreStore Store => store;

    /// <summary>
    /// Everything known about one system, shipped fact first. Empty for almost everywhere, which
    /// is the case this is asked about ten times a second.
    /// </summary>
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

    /// <summary>
    /// Records something new. <paramref name="corroborated"/> is what a lookup <em>appeared</em>
    /// to support at the time and is a label rather than a verdict — see <see cref="LoreTier"/>,
    /// which is also where it is written down that nothing promotes one tier to another later.
    /// </summary>
    /// <param name="arrival">
    /// How this got here. Supplied by the call site rather than inferred, because the two call
    /// sites are the whole distinction: the panel knows a person typed it and the tool handler
    /// cannot know that.
    /// </param>
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
            // Only the Commander's own hands produce anything above their own word, and even
            // then only with a lookup behind it. A model-written entry is the model's, always.
            Tier = corroborated && arrival == LoreArrival.Panel ? LoreTier.Corroborated : LoreTier.Commander,
            Arrival = arrival,
            FrontierId = frontierId,
            AddedAt = now,
        });

    /// <summary>
    /// What d47 says when asked about a system rather than arriving in one — every entry, in
    /// tier order, each in its own voice. Null when there is nothing.
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
