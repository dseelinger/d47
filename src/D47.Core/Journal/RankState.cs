namespace D47.Core.Journal;

/// <summary>
/// One career's ladder, as the journal states it (list.md Phase 34, "Goals that outlive a
/// checklist").
/// <para>
/// <b>The journal gives a number and never a word</b>, so this carries a number. Naming rank 6 in
/// Exploration would mean shipping a table of Frontier's rank words, and CLAUDE.md says a game-data
/// table is derived by a generator with its provenance recorded rather than hand-written — there is
/// no licence-clean generator source for the ladders the way there is for ships, blueprints and
/// engineers. <see cref="ShipCrew"/> shows the other half of that rule working: a crew member's
/// rating is a word Elite <em>wrote</em>, so it is passed through untouched.
/// </para>
/// <para>
/// Only <see cref="Elite"/> is named, and it is named by this phase rather than read off a table:
/// it is the arc's definition of done.
/// </para>
/// </summary>
/// <param name="Career">The journal's own key — <c>Combat</c>, <c>Trade</c>, <c>Explore</c>.</param>
/// <param name="Rank">0 through 8, and past 8 for the Elite grades Odyssey added.</param>
public sealed record RankStanding(string Career, int Rank)
{
    /// <summary>The rank at which a career ladder is finished, and the only one with a name.</summary>
    public const int Elite = 8;

    /// <summary>Percent into the current rank, from the <c>Progress</c> event. Null until it says.</summary>
    public int? Percent { get; init; }

    public bool IsElite => Rank >= Elite;

    /// <summary>How a Commander hears it. A number out of eight, for the reason above.</summary>
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
/// <c>Promotion</c> (list.md Phase 34).
/// <para>
/// <b>Three events, and they are not interchangeable.</b> <c>Rank</c> is a complete snapshot
/// written at startup, <c>Progress</c> is the percent into each of those same ranks, and
/// <c>Promotion</c> names one career that has just moved and carries no percent — the same
/// snapshot-versus-delta distinction <see cref="EngineerProgressState"/> draws, and getting it
/// wrong the other way would leave a Commander freshly promoted to Elite still reading as one
/// short of it until they restarted the game.
/// </para>
/// <para>
/// A promotion resets the percent to null rather than to zero. Zero is a figure Elite did not
/// state, and the next <c>Progress</c> event will state one.
/// </para>
/// </summary>
public sealed record RankState
{
    /// <summary>The journal's key for each career an arc is offered for.</summary>
    /// <remarks>
    /// <c>CQC</c> is read like everything else and simply has no arc — see the Phase 34 plan.
    /// <c>Empire</c> and <c>Federation</c> are navy ranks rather than career ladders and have no
    /// Elite at the top of them, so they are not in this list at all.
    /// </remarks>
    public static readonly IReadOnlyList<string> Careers =
        ["Combat", "Trade", "Explore", "Soldier", "Exobiologist", "CQC"];

    public static readonly RankState Empty = new();

    /// <summary>When the game last said anything about rank. Null means it has not.</summary>
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

    /// <summary>
    /// The startup snapshot. Replaces outright — it is every career at once, so merging would keep
    /// a figure the game has just contradicted.
    /// </summary>
    private RankState Snapshot(JournalEvent journalEvent)
    {
        var standings = Careers
            .Select(career => (Career: career, Rank: journalEvent.Raw.Int(career)))
            .Where(pair => pair.Rank is not null)
            .Select(pair => new RankStanding(pair.Career, pair.Rank!.Value)
            {
                // Carried across, because Rank and Progress arrive as a pair one line apart and
                // the snapshot would otherwise blank a percent it says nothing about.
                Percent = For(pair.Career)?.Percent,
            })
            .ToList();

        return standings.Count == 0
            ? this
            : new RankState { TakenAt = journalEvent.Timestamp, Standings = standings };
    }

    /// <summary>
    /// The percents. Establishes a standing where none exists yet, because <c>Progress</c> can be
    /// the first of the pair to be read on a continuation file.
    /// </summary>
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

    /// <summary>
    /// One career moved. Names exactly one and carries no percent, so everything else is left
    /// alone and that one loses the percent it had — which is now about a rank the Commander has
    /// left.
    /// </summary>
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
