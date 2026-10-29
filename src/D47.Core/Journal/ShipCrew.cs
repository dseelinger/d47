namespace D47.Core.Journal;

/// <summary>One hired pilot, as the journal reports them (Phase 11, "Ship Crew").</summary>
/// <param name="Name">Theirs, chosen by the game.</param>
/// <param name="CrewId">The stable identity.</param>
/// <param name="CombatRank">0 (Harmless) through 8 (Elite); null where unrecorded.</param>
/// <param name="Active">Whether they are the one currently in the fighter bay.</param>
/// <param name="PostedTo">
/// The ship that was active when they were last assigned, or null if d47 has not seen that happen.
/// </param>
public sealed record CrewMember(
    string Name,
    long CrewId,
    int? CombatRank = null,
    bool Active = false,
    string? PostedTo = null)
{
    /// <summary>The highest rank a hired pilot reaches.</summary>
    public const int RankTop = RankStanding.Elite;

    /// <summary>The rank's name, Harmless through Elite; null where unrecorded.</summary>
    public string? CombatRankName => CombatRank is { } rank ? (rank == RankTop ? RankStanding.EliteName(rank) : CareerRankNames.Name("Combat", rank)) : null;

    /// <summary>The rank in the one spoken form, "Combat Rank 2 of 8, Novice"; null where unrecorded.</summary>
    public string? SayRank() => CombatRank is { } rank ? new RankStanding("Combat", rank).Say(withPercent: false, top: RankTop) : null;

    /// <summary>What the roster calls them out loud.</summary>
    public string Describe() => SayRank() is { } rank ? $"{Name} ({rank})" : Name;

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
                new CrewMember(hired.Name, hired.CrewId, Rank(journalEvent, "CombatRank")),
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

        "NpcCrewRank" when journalEvent.Long("NpcCrewId") is { } rankedId => this with
        {
            Members =
            [
                .. Members.Select(member => member.CrewId == rankedId
                    ? member with { CombatRank = Rank(journalEvent, "RankCombat") ?? member.CombatRank }
                    : member),
            ],
        },

        _ => this,
    };

    private static int? Rank(JournalEvent journalEvent, string property) =>
        journalEvent.Int(property) is >= 0 and <= CrewMember.RankTop and { } rank ? rank : null;

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
