namespace D47.Core.Journal;

/// <summary>Whether the Commander is in a squadron, from the events that join, create, leave and end one.</summary>
public sealed record SquadronState
{
    public static readonly SquadronState None = new();

    public bool IsMember { get; init; }

    public string? Name { get; init; }

    /// <summary>
    /// <c>SquadronStartup</c> is written at each login while in a squadron, so the load that precedes it
    /// ends any membership the last session left.
    /// </summary>
    public SquadronState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "SquadronStartup" or "JoinedSquadron" or "SquadronCreated" => new SquadronState
            {
                IsMember = true,
                Name = journalEvent.String("SquadronName") ?? Name,
            },
            "LoadGame" or "LeftSquadron" or "KickedFromSquadron" or "DisbandedSquadron" => None,
            _ => this,
        };
    }
}
