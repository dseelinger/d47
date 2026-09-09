namespace D47.Core.Journal;

/// <summary>One hired pilot, as the journal reports them (Phase 11, "Ship Crew").</summary>
/// <param name="Name">Theirs, chosen by the game.</param>
/// <param name="CrewId">The stable identity.</param>
/// <param name="CombatRank">As Elite words it: Harmless through Elite.</param>
/// <param name="Active">Whether they are the one currently in the fighter bay.</param>
/// <param name="PostedTo">
/// The ship that was active when they were last assigned, or null if d47 has not seen that happen.
/// </param>
public sealed record CrewMember(
    string Name,
    long CrewId,
    string? CombatRank = null,
    bool Active = false,
    string? PostedTo = null)
{
    /// <summary>What the roster calls them out loud.</summary>
    public string Describe() => CombatRank is { Length: > 0 } rank ? $"{Name} ({rank})" : Name;
}

/// <summary>The Commander's hired NPC pilots, folded from the journal.</summary>
public sealed record ShipCrew
{
    public static readonly ShipCrew Empty = new();

    /// <summary>Everyone on the books, in hire order.</summary>
    public IReadOnlyList<CrewMember> Members { get; init; } = [];

    /// <summary>The one in the fighter bay, or null when nobody is assigned.</summary>
    public CrewMember? Active => Members.FirstOrDefault(m => m.Active);

    public bool Any => Members.Count > 0;

    /// <summary>Those posted to one hull.</summary>
    public IReadOnlyList<CrewMember> For(string? ship) =>
        ship is null ? [] : [.. Members.Where(m => string.Equals(m.PostedTo, ship, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Folds one event.</summary>
    public ShipCrew Apply(JournalEvent journalEvent, string? activeShip = null) => journalEvent.Kind switch
    {
        "CrewHire" when Read(journalEvent) is { } hired => this with
        {
            // Replaced rather than added if the id is already known: Elite writes CrewHire again on some
            // session restores, and a roster that doubled up would report two of them.
            Members =
            [
                .. Members.Where(m => m.CrewId != hired.CrewId),
                new CrewMember(hired.Name, hired.CrewId, journalEvent.String("CombatRank")),
            ],
        },

        "CrewFire" when Read(journalEvent) is { } fired => this with
        {
            Members = [.. Members.Where(m => m.CrewId != fired.CrewId)],
        },

        // Role is "Active" or "OnShoreLeave" — Elite's own words, and the only two it uses.
        "CrewAssign" when Read(journalEvent) is { } assigned => this with
        {
            Members =
            [
                .. Members.Select(member => member.CrewId != assigned.CrewId
                    ? member with { Active = false }
                    : member with
                    {
                        Active = string.Equals(
                            journalEvent.String("Role"), "Active", StringComparison.OrdinalIgnoreCase),
                        PostedTo = activeShip ?? member.PostedTo,
                    }),
            ],
        },

        "NpcCrewRank" when Read(journalEvent) is { } ranked => this with
        {
            Members =
            [
                .. Members.Select(member => member.CrewId == ranked.CrewId
                    ? member with { CombatRank = journalEvent.String("RankCombat") ?? member.CombatRank }
                    : member),
            ],
        },

        _ => this,
    };

    /// <summary>
    /// The name and id every crew event carries, or null when one of them is missing — which is a
    /// schema change rather than an error, and is skipped like any other unrecognised shape (Phase 2,
    /// "Survive a journal schema change").
    /// </summary>
    private static (string Name, long CrewId)? Read(JournalEvent journalEvent) =>
        journalEvent.String("Name") is { Length: > 0 } name && journalEvent.Long("CrewID") is { } id
            ? (name, id)
            : null;
}
