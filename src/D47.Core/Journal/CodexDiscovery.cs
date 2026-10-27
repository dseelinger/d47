namespace D47.Core.Journal;

/// <summary>A codex entry the Commander logged for the first time.</summary>
public sealed record CodexDiscovery(long EntryId, int? RegionId, string Name)
{
    /// <summary>The discovery a <c>CodexEntry</c> with <c>IsNewEntry</c> records; null for anything else.</summary>
    public static CodexDiscovery? Of(JournalEvent journalEvent) =>
        journalEvent.Kind == "CodexEntry"
        && journalEvent.Bool("IsNewEntry")
        && journalEvent.Long("EntryID") is { } entryId
        && journalEvent.String("Name_Localised") is { Length: > 0 } name
            ? new CodexDiscovery(entryId, CodexLedger.RegionIdOf(journalEvent.String("Region")), name)
            : null;
}
