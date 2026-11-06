using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>How a conflict row names the faction on the other side.</summary>
public enum OpponentKind
{
    /// <summary>One faction is the opponent.</summary>
    Named,

    /// <summary>Several factions share the state and none is known to be the opponent.</summary>
    OneOf,

    /// <summary>No other faction shows the state yet.</summary>
    Unknown,
}

/// <summary>One system where the faction has a war, civil war or election, as the Conflicts section draws it.</summary>
/// <param name="Opponent">The opponent's name when <paramref name="Kind"/> is <see cref="OpponentKind.Named"/>.</param>
/// <param name="Candidates">The factions that could be the opponent.</param>
/// <param name="Visited">Whether the Commander's own reading of the system holds the conflict, which fills days won and stakes.</param>
public sealed record FactionConflict(
    string System,
    string Type,
    bool Active,
    OpponentKind Kind,
    string Opponent,
    IReadOnlyList<string> Candidates,
    bool Visited,
    int OursWon,
    int TheirsWon,
    string OurStake,
    string TheirStake)
{
    public string Status => Active ? "ACTIVE" : "PENDING";

    /// <summary>"One of 3 factions".</summary>
    public string OneOfText => $"One of {Candidates.Count} factions";

    /// <summary>The candidates' names joined by " · ": the first three, then "and N more".</summary>
    public string CandidateText
    {
        get
        {
            var shown = string.Join(" · ", Candidates.Take(3));

            return Candidates.Count > 3 ? $"{shown} and {Candidates.Count - 3} more" : shown;
        }
    }

    /// <summary>"1 – 2", ours then theirs.</summary>
    public string DaysText => $"{OursWon} – {TheirsWon}";
}

/// <summary>Pairs the faction's conflicts with their opponents from the galaxy report and the Commander's readings.</summary>
public static class FactionConflictRows
{
    /// <summary>One row per system with a conflict, active before pending, then by system name.</summary>
    public static IReadOnlyList<FactionConflict> Of(FactionReport report, SystemStandings standings)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(standings);

        var systems = report.Systems
            .GroupBy(system => system.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return
        [
            .. FactionRows.Of(report, standings)
                .Where(row => row.InConflict)
                .Select(row => RowOf(report.Name, row, systems.GetValueOrDefault(row.System), standings.Latest(row.System)))
                .OrderByDescending(conflict => conflict.Active)
                .ThenBy(conflict => conflict.System, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private static FactionConflict RowOf(string faction, FactionRow row, FactionSystem? system, SystemReading? reading)
    {
        var active = row.Active.Any(FactionStates.IsConflict);
        var type = (active ? row.Active : row.Pending).First(FactionStates.IsConflict);
        var wanted = active ? "active" : "pending";

        var held = reading?.Conflicts
            .Where(c => !c.Ended && (Is(c.Faction1.Name, faction) || Is(c.Faction2.Name, faction)))
            .OrderByDescending(c => string.Equals(c.Status, wanted, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        var ours = held is null ? null : Is(held.Faction1.Name, faction) ? held.Faction1 : held.Faction2;
        var theirs = held is null ? null : ReferenceEquals(ours, held.Faction1) ? held.Faction2 : held.Faction1;

        IReadOnlyList<string> candidates = row.OwnReading && reading is not null
            ? [.. reading.Factions
                .Where(f => !Is(f.Name, faction) && Shows(active ? f.ActiveStates : f.PendingStates, type))
                .Select(f => f.Name)]
            : [.. (system?.Others ?? [])
                .Where(f => Shows(active ? f.ActiveStates : f.PendingStates, type))
                .Select(f => f.Name)];

        var (kind, opponent) = theirs is { Name.Length: > 0 }
            ? (OpponentKind.Named, theirs.Name)
            : candidates.Count == 1
                ? (OpponentKind.Named, candidates[0])
                : candidates.Count > 1 ? (OpponentKind.OneOf, string.Empty) : (OpponentKind.Unknown, string.Empty);

        return new FactionConflict(
            row.System,
            type,
            active,
            kind,
            opponent,
            candidates,
            held is not null,
            ours?.WonDays ?? 0,
            theirs?.WonDays ?? 0,
            ours?.Stake ?? string.Empty,
            theirs?.Stake ?? string.Empty);
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Shows(IEnumerable<string> states, string display) =>
        states.Any(state => string.Equals(FactionStates.Display(state), display, StringComparison.Ordinal));
}
