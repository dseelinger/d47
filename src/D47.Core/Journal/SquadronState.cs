namespace D47.Core.Journal;

/// <summary>Whether the Commander is in a squadron, from the events that join, create, leave and end one.</summary>
public sealed record SquadronState
{
    public static readonly SquadronState None = new();

    public bool IsMember { get; init; }

    public string? Name { get; init; }

    public long? SquadronId { get; init; }

    /// <summary>The ID where one has been seen, otherwise the name; null outside a squadron.</summary>
    public string? Key => !IsMember ? null : SquadronId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? Name;

    /// <summary>
    /// <c>SquadronStartup</c> is written at each login while in a squadron, so the load that precedes it
    /// ends any membership the last session left.
    /// </summary>
    public SquadronState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "SquadronStartup" or "JoinedSquadron" or "SquadronCreated" => Joined(journalEvent),
            "LoadGame" or "LeftSquadron" or "KickedFromSquadron" or "DisbandedSquadron" => None,
            _ => this,
        };
    }

    private SquadronState Joined(JournalEvent journalEvent)
    {
        var name = journalEvent.String("SquadronName");
        var sameSquadron = name is null || string.Equals(name, Name, StringComparison.Ordinal);

        return new SquadronState
        {
            IsMember = true,
            Name = name ?? Name,
            SquadronId = journalEvent.Long("SquadronID") ?? (sameSquadron ? SquadronId : null),
        };
    }
}
