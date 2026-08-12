namespace D47.Core.Journal;

/// <summary>One Commander's derived state. A plain holder — behaviour lives in <see cref="Apply"/>.</summary>
public sealed class CommanderGameState(CommanderIdentity identity)
{
    public CommanderIdentity Identity { get; private set; } = identity;

    public JournalLocation Location { get; private set; } = JournalLocation.Unknown;

    public void Apply(JournalEvent journalEvent)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity && identity.FrontierId == Identity.FrontierId)
        {
            Identity = identity; // The name can change (rename); the FID is the stable key.
        }

        Location = Location.Apply(journalEvent);
    }
}
