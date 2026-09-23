using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>One career's ladder, as the journal states it (Phase 34, "Goals that outlive a checklist").</summary>
/// <param name="Career">The journal's own key — <c>Combat</c>, <c>Trade</c>, <c>Explore</c>.</param>
/// <param name="Rank">0 through 13 — 8 is Elite, and 9 through 13 are Elite I through Elite V.</param>
public sealed record RankStanding(string Career, int Rank)
{
    /// <summary>The rank at which a career ladder reaches Elite.</summary>
    public const int Elite = 8;

    /// <summary>The rank at which a career ladder is finished — Elite V.</summary>
    public const int EliteTop = 13;

    /// <summary>Percent into the current rank, from the <c>Progress</c> event.</summary>
    public int? Percent { get; init; }

    /// <summary>Whether this is one of <see cref="RankState.Careers"/>; a navy rank has no Elite.</summary>
    public bool IsCareer => RankState.Careers.Contains(Career, StringComparer.OrdinalIgnoreCase);

    public bool IsElite => IsCareer && Rank >= Elite;

    /// <summary>Whether a career ladder has run all the way to Elite V.</summary>
    public bool IsMaxRank => IsCareer && Rank >= EliteTop;

    /// <summary>How a Commander hears it.</summary>
    public string Describe() => !IsCareer
        ? NavyName() is { } navyNamed
            ? Percent is { } navyPercent ? $"{navyNamed}, {navyPercent}% into it" : navyNamed
            : Percent is { } numberedNavyPercent ? $"rank {Rank}, {numberedNavyPercent}% into it" : $"rank {Rank}"
        : IsElite
        ? EliteName(Rank)
        : CareerRankNames.Name(Career, Rank) is { } named
            ? Percent is { } namedPercent ? $"{named}, {namedPercent}% into it" : named
            : Percent is { } numberedPercent
                ? $"rank {Rank} of {Elite}, {numberedPercent}% into it"
                : $"rank {Rank} of {Elite}";

    /// <summary>The named rung of a navy ladder; the journal and <see cref="NavalRanks"/> share the 0 to 14 scale.</summary>
    private string? NavyName() => Career switch
    {
        _ when string.Equals(Career, "Empire", StringComparison.OrdinalIgnoreCase) => NavalRanks.EmpireName(Rank),
        _ when string.Equals(Career, "Federation", StringComparison.OrdinalIgnoreCase) => NavalRanks.FederationName(Rank),
        _ => null,
    };

    private static string EliteName(int rank) => rank == Elite ? "Elite" : $"Elite {Grade(rank - Elite)}";

    private static string Grade(int grade) => grade switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        5 => "V",
        _ => grade.ToString(CultureInfo.InvariantCulture),
    };
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

    /// <summary>Every key folded: the careers and the two navy ranks.</summary>
    private static readonly IReadOnlyList<string> Folded = [.. Careers, "Empire", "Federation"];

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
        var standings = Folded
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

        foreach (var career in Folded)
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
        var promoted = Folded
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
