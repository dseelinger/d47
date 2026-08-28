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
/// The Commander's standing with every engineer (Phase 14, "Engineers" — the current
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

        if (Merged(journalEvent.Raw) is not { } changed)
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

    /// <summary>
    /// One delta, folded onto the row already held — <b>field by field, because an absent field in
    /// a delta means "unchanged" and not "unknown"</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/32">#32</a>).
    /// <para>
    /// <b>This is the snapshot-versus-delta distinction carried down into the fields.</b> The class
    /// already drew it at the level of the row — the array replaces, the single merges — and
    /// <see cref="Read"/>'s all-three-or-nothing rule is right for a snapshot row and was wrong
    /// here: a rank-up carries no <c>Progress</c> at all, so every one of them was read as
    /// unparseable and dropped, and d47 held the rank it was unlocked at until the next restart.
    /// Reported 2026-08-24 as <i>"repeated once per module, even though my relationship with the
    /// engineer is 5"</i> — Selene Jean went 1 to 5 in four and a half minutes and d47 kept the 1.
    /// </para>
    /// <para>
    /// <b>Three shapes exist and no more</b>, counted across all 926 journals: <c>Rank</c> alone,
    /// 172 times (the rank-up, and the whole of what was lost); <c>Progress</c> alone, 64; and the
    /// two together, 42, which is the unlock. <b>No delta has ever carried
    /// <c>RankProgress</c></b> — 0 of 278 — which is what decides the next paragraph.
    /// </para>
    /// <para>
    /// <b>A rank that moves takes the percentage with it.</b> The held <c>RankProgress</c> is
    /// progress towards the rank the Commander has just reached, so carrying it forward would
    /// state it as progress towards the one after — swapping a silent loss for a silent lie. It
    /// goes to null rather than to zero, because zero is a claim the game has not made and null is
    /// the one this class already means by "not said yet"; <see cref="EngineerStanding.Describe"/>
    /// draws them identically, so the honest one costs nothing. The next snapshot fills it in.
    /// </para>
    /// <para>
    /// <b>A rank arriving for an engineer with no row is an unlock</b>, and that is an inference
    /// rather than something the event says: a rank exists only once unlocked, so a delta naming
    /// one for an engineer never seen has nothing else it could mean. Rare — every observed
    /// unlock carried its own <c>Progress</c> — but a delta before the first snapshot is exactly
    /// the case the merge cannot ask anybody about.
    /// </para>
    /// </summary>
    private EngineerStanding? Merged(System.Text.Json.JsonElement element)
    {
        var id = element.Int("EngineerID");

        if (id is null)
        {
            return null;
        }

        var held = For(id.Value);
        var rank = element.Int("Rank");

        var name = element.String("Engineer") ?? held?.Name;
        var progress = element.String("Progress")
                       ?? held?.Progress
                       ?? (rank is not null ? "Unlocked" : null);

        if (name is null || progress is null)
        {
            return null;
        }

        return new EngineerStanding(id.Value, name, progress)
        {
            Rank = rank ?? held?.Rank,
            RankProgress = rank is { } moved && moved != held?.Rank
                ? null
                : element.Int("RankProgress") ?? held?.RankProgress,
        };
    }

    private static EngineerStanding? Read(System.Text.Json.JsonElement element)
    {
        var id = element.Int("EngineerID");
        var name = element.String("Engineer");
        var progress = element.String("Progress");

        // All three or nothing. A standing with no id cannot be tied to the directory, and one
        // with no progress word is a state nobody can name. <b>Snapshot rows only</b> — a delta
        // goes through <see cref="Merged"/>, which is what #32 turned on.
        return id is null || name is null || progress is null
            ? null
            : new EngineerStanding(id.Value, name, progress)
            {
                Rank = element.Int("Rank"),
                RankProgress = element.Int("RankProgress"),
            };
    }
}
