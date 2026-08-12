namespace D47.Core.Journal;

/// <summary>Who a journal file's events belong to. Established once, from near the top of the file.</summary>
public sealed record CommanderIdentity(string FrontierId, string Name)
{
    /// <summary>
    /// The "Commander" and "LoadGame" events both carry identity, under different field names
    /// ("Name" vs "Commander"). Either is enough. "Commander" is preferred — it is the event
    /// whose sole purpose is identity, so it is checked first.
    /// </summary>
    public static CommanderIdentity? From(JournalEvent journalEvent)
    {
        if (journalEvent.Kind == "Commander" &&
            journalEvent.TryGetString("FID", out var fid) &&
            journalEvent.TryGetString("Name", out var name))
        {
            return new CommanderIdentity(fid!, name!);
        }

        if (journalEvent.Kind == "LoadGame" &&
            journalEvent.TryGetString("FID", out var loadFid) &&
            journalEvent.TryGetString("Commander", out var loadName))
        {
            return new CommanderIdentity(loadFid!, loadName!);
        }

        return null;
    }
}
