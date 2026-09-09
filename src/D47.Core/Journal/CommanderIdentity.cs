namespace D47.Core.Journal;

/// <summary>Who a journal file's events belong to.</summary>
public sealed record CommanderIdentity(string FrontierId, string Name)
{
    /// <summary>
    /// The "Commander" and "LoadGame" events both carry identity, under different field names ("Name"
    /// vs "Commander").
    /// </summary>
    public static CommanderIdentity? From(JournalEvent journalEvent)
    {
        if (journalEvent.Kind == "Commander" &&
            journalEvent.String("FID") is { } fid &&
            journalEvent.String("Name") is { } name)
        {
            return new CommanderIdentity(fid, name);
        }

        if (journalEvent.Kind == "LoadGame" &&
            journalEvent.String("FID") is { } loadFid &&
            journalEvent.String("Commander") is { } loadName)
        {
            return new CommanderIdentity(loadFid, loadName);
        }

        return null;
    }
}
