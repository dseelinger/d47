namespace D47.Core.Journal;

/// <summary>How far along the Commander is with one engineer.</summary>
/// <param name="Id">The engineer id, which is what ties this to the directory.</param>
/// <param name="Progress">
/// The game's own word: <c>Known</c>, <c>Invited</c> or <c>Unlocked</c>.
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
/// The Commander's standing with every engineer (Phase 14, "Engineers" — the current unlock status
/// half), folded from <c>EngineerProgress</c>.
/// </summary>
public sealed record EngineerProgressState
{
    public static readonly EngineerProgressState Empty = new();

    /// <summary>When a snapshot was last seen.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<EngineerStanding> Standings { get; init; } = [];

    /// <summary>Whether anything has been heard at all.</summary>
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

        // Merged by id, so a rank-up replaces that engineer's row and leaves the rest alone.
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
    /// One delta, folded onto the row already held — field by field, because an absent field in a delta
    /// means "unchanged" and not "unknown" (#32).
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

        // All three or nothing.
        return id is null || name is null || progress is null
            ? null
            : new EngineerStanding(id.Value, name, progress)
            {
                Rank = element.Int("Rank"),
                RankProgress = element.Int("RankProgress"),
            };
    }
}
