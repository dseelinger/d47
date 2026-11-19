using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>A live <c>CodexEntry</c> judged a possible region or galaxy first. <c>Name</c> is the name the codex page shows.</summary>
public sealed record CodexFirstSeen(
    long EntryId,
    int RegionId,
    string Name,
    string? Region,
    CodexFirst First,
    string? System,
    long? SystemAddress,
    int? BodyId,
    DateTimeOffset At)
{
    /// <summary>The last of <paramref name="arrived"/> that is a first, or null.</summary>
    public static CodexFirstSeen? Newest(IEnumerable<JournalEvent> arrived, CodexLedger? ledger, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(arrived);

        return arrived.Select(journalEvent => Of(journalEvent, ledger, table)).LastOrDefault(seen => seen is not null);
    }

    /// <summary>
    /// The first <paramref name="journalEvent"/> is, or null: a new entry the Commander's whole ledger holds no earlier
    /// entry for in that region, which <see cref="CodexFirsts.Judge"/> rates a region or galaxy first. The caller leaves
    /// out the replayed backlog.
    /// </summary>
    public static CodexFirstSeen? Of(JournalEvent journalEvent, CodexLedger? ledger, CodexTable table)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);
        ArgumentNullException.ThrowIfNull(table);

        if (journalEvent.Kind != "CodexEntry"
            || !journalEvent.Bool("IsNewEntry")
            || journalEvent.Long("EntryID") is not { } entryId
            || CodexLedger.RegionIdOf(journalEvent.String("Region")) is not { } regionId
            || ledger is not { IsWhole: true }
            || ledger.Entries.Any(entry => entry.EntryId == entryId && entry.RegionId == regionId && entry.At < journalEvent.Timestamp)
            || CodexDiscovery.Of(journalEvent) is not { } discovery)
        {
            return null;
        }

        var first = CodexFirsts.Judge(table, entryId, regionId, journalEvent.String("SubCategory"));

        return first == CodexFirst.None
            ? null
            : new CodexFirstSeen(
                entryId,
                regionId,
                discovery.Name,
                journalEvent.String("Region_Localised") is { Length: > 0 } region ? region : GalacticRegions.NameOf(regionId),
                first,
                journalEvent.String("System"),
                journalEvent.Long("SystemAddress"),
                journalEvent.Int("BodyID"),
                journalEvent.Timestamp);
    }
}
