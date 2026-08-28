namespace D47.Core.Journal;

/// <summary>
/// One hired pilot, as the journal reports them (Phase 11, "Ship Crew").
/// </summary>
/// <param name="Name">
/// Theirs, chosen by the game. Untrusted only in the weak sense — it is game-generated rather
/// than player-authored — but it is still text from outside, so it is used as a key and spoken,
/// never as an instruction.
/// </param>
/// <param name="CrewId">The stable identity. A crew member can be fired and a name reused.</param>
/// <param name="CombatRank">
/// As Elite words it: Harmless through Elite. Reported rather than interpreted, because how
/// good a Harmless pilot actually is is not something the journal states.
/// </param>
/// <param name="Active">
/// Whether they are the one currently in the fighter bay. Elite allows exactly one, which is
/// why this is a flag on the member rather than a field on the roster.
/// </param>
/// <param name="PostedTo">
/// The ship that was active when they were last assigned, or null if d47 has not seen that
/// happen. <b>This is derived, not reported.</b> Elite's CrewAssign event names no ship: the
/// hired crew are a pool belonging to the Commander, and the active one flies in whatever hull
/// the Commander is sitting in. "Per-ship rosters" is honoured as far as the journal allows —
/// where d47 saw the assignment, it remembers the hull — and it says so rather than inventing a
/// posting for crew hired before d47 was watching.
/// </param>
public sealed record CrewMember(
    string Name,
    long CrewId,
    string? CombatRank = null,
    bool Active = false,
    string? PostedTo = null)
{
    /// <summary>What the roster calls them out loud. The name is all Elite gives.</summary>
    public string Describe() => CombatRank is { Length: > 0 } rank ? $"{Name} ({rank})" : Name;
}

/// <summary>
/// The Commander's hired NPC pilots, folded from the journal.
/// <para>
/// Nothing here is invented. Elite reports hiring, firing, assignment and rank, and that is the
/// whole of what a crew roster can honestly contain — there is no engineer, no gunner and no
/// navigator in this game, and shipping a fictional roster of them would be exactly the
/// confident wrong answer the guardrails exist to prevent. What the journal states, d47 says.
/// </para>
/// </summary>
public sealed record ShipCrew
{
    public static readonly ShipCrew Empty = new();

    /// <summary>Everyone on the books, in hire order.</summary>
    public IReadOnlyList<CrewMember> Members { get; init; } = [];

    /// <summary>The one in the fighter bay, or null when nobody is assigned.</summary>
    public CrewMember? Active => Members.FirstOrDefault(m => m.Active);

    public bool Any => Members.Count > 0;

    /// <summary>
    /// Those posted to one hull. Empty for a ship d47 never saw an assignment aboard, which is
    /// the honest answer rather than "everyone".
    /// </summary>
    public IReadOnlyList<CrewMember> For(string? ship) =>
        ship is null ? [] : [.. Members.Where(m => string.Equals(m.PostedTo, ship, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Folds one event. <paramref name="activeShip"/> is what the Commander is flying right now,
    /// which is the only way an assignment can be tied to a hull at all — see
    /// <see cref="CrewMember.PostedTo"/>.
    /// </summary>
    public ShipCrew Apply(JournalEvent journalEvent, string? activeShip = null) => journalEvent.Kind switch
    {
        "CrewHire" when Read(journalEvent) is { } hired => this with
        {
            // Replaced rather than added if the id is already known: Elite writes CrewHire again
            // on some session restores, and a roster that doubled up would report two of them.
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
    /// The name and id every crew event carries, or null when one of them is missing — which is
    /// a schema change rather than an error, and is skipped like any other unrecognised shape
    /// (Phase 2, "Survive a journal schema change").
    /// </summary>
    private static (string Name, long CrewId)? Read(JournalEvent journalEvent) =>
        journalEvent.String("Name") is { Length: > 0 } name && journalEvent.Long("CrewID") is { } id
            ? (name, id)
            : null;
}
