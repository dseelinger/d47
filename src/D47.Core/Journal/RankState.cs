namespace D47.Core.Journal;

/// <summary>One career's ladder, as the journal states it (Phase 34, "Goals that outlive a checklist").</summary>
/// <param name="Career">The journal's own key — <c>Combat</c>, <c>Trade</c>, <c>Explore</c>.</param>
/// <param name="Rank">0 through 8, and past 8 for the Elite grades Odyssey added.</param>
public sealed record RankStanding(string Career, int Rank)
{
    /// <summary>The rank at which a career ladder is finished, and the only one with a name.</summary>
    public const int Elite = 8;

    /// <summary>Percent into the current rank, from the <c>Progress</c> event.</summary>
    public int? Percent { get; init; }

    public bool IsElite => Rank >= Elite;

    /// <summary>How a Commander hears it.</summary>
    public string Describe() => IsElite
        ? Rank == Elite
            ? "Elite"
            : $"Elite, {Rank - Elite} grade{(Rank - Elite == 1 ? string.Empty : "s")} past it"
        : Percent is { } percent
            ? $"rank {Rank} of {Elite}, {percent}% into it"
            : $"rank {Rank} of {Elite}";
}

/// <summary>
/// Where the Commander stands in every career, folded from <c>Rank</c>, <c>Progress</c> and
/// <c>Promotion</c> (Phase 34).
/// </summary>
public sealed record RankState
{
    /// <summary>The journal's key for each career an arc is offered for.</summary>
    public static readonly IReadOnlyList<string> Careers =
        ["Combat", "Trade", "Explore", "Soldier", "Exobiologist", "CQC"];

    public static readonly RankState Empty = new();

    /// <summary>When the game last said anything about rank.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<RankStanding> Standings { get; init; } = [];

    public bool IsKnown => TakenAt is not null;

    public RankStanding? For(string career) =>
        Standings.FirstOrDefault(standing =>
            string.Equals(standing.Career, career, StringComparison.OrdinalIgnoreCase));

    public RankState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "Rank" => Snapshot(journalEvent),
            "Progress" => WithPercents(journalEvent),
            "Promotion" => Promoted(journalEvent),
            _ => this,
        };
    }

    /// <summary>The startup snapshot.</summary>
    private RankState Snapshot(JournalEvent journalEvent)
    {
        var standings = Careers
            .Select(career => (Career: career, Rank: journalEvent.Raw.Int(career)))
            .Where(pair => pair.Rank is not null)
            .Select(pair => new RankStanding(pair.Career, pair.Rank!.Value)
            {
                // Carried across, because Rank and Progress arrive as a pair one line apart and the snapshot
                // would otherwise blank a percent it says nothing about.
                Percent = For(pair.Career)?.Percent,
            })
            .ToList();

        return standings.Count == 0
            ? this
            : new RankState { TakenAt = journalEvent.Timestamp, Standings = standings };
    }

    /// <summary>The percents.</summary>
    private RankState WithPercents(JournalEvent journalEvent)
    {
        var seen = false;
        var standings = new List<RankStanding>(Standings);

        foreach (var career in Careers)
        {
            if (journalEvent.Raw.Int(career) is not { } percent)
            {
                continue;
            }

            seen = true;
            var index = standings.FindIndex(standing =>
                string.Equals(standing.Career, career, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                standings[index] = standings[index] with { Percent = percent };
            }
            else
            {
                standings.Add(new RankStanding(career, 0) { Percent = percent });
            }
        }

        return seen
            ? new RankState { TakenAt = journalEvent.Timestamp, Standings = standings }
            : this;
    }

    /// <summary>One career moved.</summary>
    private RankState Promoted(JournalEvent journalEvent)
    {
        var promoted = Careers
            .Select(career => (Career: career, Rank: journalEvent.Raw.Int(career)))
            .Where(pair => pair.Rank is not null)
            .ToList();

        if (promoted.Count == 0)
        {
            return this;
        }

        var standings = new List<RankStanding>(Standings);

        foreach (var (career, rank) in promoted)
        {
            var index = standings.FindIndex(standing =>
                string.Equals(standing.Career, career, StringComparison.OrdinalIgnoreCase));

            var moved = new RankStanding(career, rank!.Value) { Percent = null };

            if (index >= 0)
            {
                standings[index] = moved;
            }
            else
            {
                standings.Add(moved);
            }
        }

        return new RankState { TakenAt = journalEvent.Timestamp, Standings = standings };
    }
}
