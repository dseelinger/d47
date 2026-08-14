namespace D47.Core.Journal;

/// <summary>How far along the Commander is with one engineer.</summary>
/// <param name="Id">
/// The engineer id, which is what ties this to the directory. The key rather than the name,
/// because a name is a string two sources can spell differently and an id is not.
/// </param>
/// <param name="Progress">
/// The game's own word: <c>Known</c>, <c>Invited</c> or <c>Unlocked</c>. Kept verbatim rather
/// than mapped to a flag, because the three are genuinely different states and the middle one is
/// the interesting one — an invitation is a referral that has already happened.
/// </param>
public sealed record EngineerStanding(int Id, string Name, string Progress)
{
    /// <summary>1 to 5, once unlocked.</summary>
    public int? Rank { get; init; }

    /// <summary>Percent towards the next rank.</summary>
    public int? RankProgress { get; init; }

    public bool IsUnlocked => string.Equals(Progress, "Unlocked", StringComparison.OrdinalIgnoreCase);

    public bool IsInvited => string.Equals(Progress, "Invited", StringComparison.OrdinalIgnoreCase);

    public string Describe()
    {
        if (!IsUnlocked)
        {
            return IsInvited ? "invited, not yet unlocked" : "known about, no invitation yet";
        }

        var rank = Rank is { } grade ? $"unlocked at grade {grade}" : "unlocked";

        return RankProgress is > 0 ? $"{rank}, {RankProgress}% to the next" : rank;
    }
}

/// <summary>
/// The Commander's standing with every engineer (list.md Phase 14, "Engineers" — the current
/// unlock status half), folded from <c>EngineerProgress</c>.
/// <para>
/// <b>The event comes in two shapes and they mean different things.</b> At startup Elite writes
/// one carrying an <c>Engineers</c> array — a complete snapshot — and during play it writes one
/// naming a single engineer whose standing has just changed. Treating the second as a snapshot
/// would wipe the other thirty-seven the first time somebody ranked up; treating the first as a
/// delta would keep an engineer the Commander no longer has. So the array replaces and the single
/// merges, which is the same distinction <see cref="MaterialsInventory"/> draws between its
/// snapshot and its deltas.
/// </para>
/// </summary>
public sealed record EngineerProgressState
{
    public static readonly EngineerProgressState Empty = new();

    /// <summary>When a snapshot was last seen. Null means the game has not said yet.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<EngineerStanding> Standings { get; init; } = [];

    /// <summary>
    /// Whether anything has been heard at all. The difference between "no engineers unlocked" and
    /// "nothing has told me yet", and only one of those is an answer about the Commander.
    /// </summary>
    public bool IsKnown => TakenAt is not null;

    public IReadOnlyList<EngineerStanding> Unlocked => [.. Standings.Where(standing => standing.IsUnlocked)];

    public IReadOnlyList<EngineerStanding> Invited => [.. Standings.Where(standing => standing.IsInvited)];

    public EngineerStanding? For(int id) => Standings.FirstOrDefault(standing => standing.Id == id);

    public EngineerProgressState Apply(JournalEvent journalEvent)
    {
        if (journalEvent.Kind != "EngineerProgress")
        {
            return this;
        }

        var snapshot = journalEvent.Items("Engineers").Select(Read).Where(standing => standing is not null).ToList();

        if (snapshot.Count > 0)
        {
            return new EngineerProgressState
            {
                TakenAt = journalEvent.Timestamp,
                Standings = [.. snapshot.Select(standing => standing!)],
            };
        }

        if (Read(journalEvent.Raw) is not { } changed)
        {
            return this;
        }

        // Merged by id, so a rank-up replaces that engineer's row and leaves the rest alone. A
        // single-engineer event arriving before any snapshot still establishes what it says
        // rather than being dropped for having nothing to merge into.
        return new EngineerProgressState
        {
            TakenAt = journalEvent.Timestamp,
            Standings =
            [
                .. Standings.Where(standing => standing.Id != changed.Id),
                changed,
            ],
        };
    }

    private static EngineerStanding? Read(System.Text.Json.JsonElement element)
    {
        var id = element.Int("EngineerID");
        var name = element.String("Engineer");
        var progress = element.String("Progress");

        // All three or nothing. A standing with no id cannot be tied to the directory, and one
        // with no progress word is a state nobody can name.
        return id is null || name is null || progress is null
            ? null
            : new EngineerStanding(id.Value, name, progress)
            {
                Rank = element.Int("Rank"),
                RankProgress = element.Int("RankProgress"),
            };
    }
}
