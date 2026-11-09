using System.Globalization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>A kind of front, declared in rank order: the first outranks the rest.</summary>
public enum FrontKind
{
    Conflict,
    Coup,
    Retreat,
    ThinMargin,
    BadState,
    UnwantedExpansion,
    Expansion,
    Stale,
}

/// <summary>One problem or opportunity for the campaign faction in one system, with the sentence that says why.</summary>
/// <param name="Opponent">The other faction when one is known: the conflict's opponent, the coup threat, or the nearest rival.</param>
/// <param name="Stake">The asset the campaign faction has at stake in a conflict, from the journal; empty when none is known.</param>
/// <param name="Margin">How far the faction is from the bad outcome, in the kind's own unit; smaller is more urgent.</param>
/// <param name="Updated">When the data the front was read from was reported, or null when the service gave no time.</param>
/// <param name="OwnReading">Whether the data came from the Commander's own journal rather than the galaxy report.</param>
public sealed record Front(
    FrontKind Kind,
    string System,
    string Reason,
    SystemIntent Intent,
    bool Controls,
    string? Opponent,
    string Stake,
    double? Margin,
    DateTimeOffset? Updated,
    bool OwnReading)
{
    /// <summary>The conflict as the faction page draws it, for a <see cref="FrontKind.Conflict"/> front.</summary>
    public FactionConflict? Conflict { get; init; }

    /// <summary>The bad states, active or pending, for a <see cref="FrontKind.BadState"/> front.</summary>
    public IReadOnlyList<string> States { get; init; } = [];
}

/// <summary>Lists the campaign faction's fronts from its galaxy report and the Commander's readings, ranked.</summary>
public static class CampaignFronts
{
    private static readonly HashSet<string> BadStates = new(
        new[] { "Lockdown", "CivilUnrest", "Famine", "Blight", "Drought", "Outbreak", "InfrastructureFailure", "PirateAttack", "TerroristAttack", "Bust" }
            .Select(FactionStates.Display),
        StringComparer.Ordinal);

    private static readonly string ExpansionState = FactionStates.Display("Expansion");

    private static readonly string RetreatState = FactionStates.Display("Retreat");

    /// <summary>The kind as the Commander hears it.</summary>
    public static string Describe(FrontKind kind) => kind switch
    {
        FrontKind.Conflict => "Conflict",
        FrontKind.Coup => "Coup threat",
        FrontKind.Retreat => "Retreat",
        FrontKind.ThinMargin => "Thin margin",
        FrontKind.BadState => "Bad state",
        FrontKind.UnwantedExpansion => "Unwanted expansion",
        FrontKind.Expansion => "Expansion under way",
        _ => "Stale report",
    };

    /// <summary>
    /// Every front, ranked by kind, then by what is at stake — a named conflict stake, then a controlled system, then
    /// population — then by margin, smallest first, then by system name.
    /// </summary>
    public static IReadOnlyList<Front> Of(FactionReport report, SystemStandings standings, Campaign campaign, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(campaign);

        var systems = report.Systems
            .GroupBy(system => system.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var conflicts = FactionConflictRows.Of(report, standings)
            .GroupBy(conflict => conflict.System, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var fronts = new List<(Front Front, long Population)>();

        foreach (var row in FactionRows.Of(report, standings))
        {
            var system = systems[row.System];
            var population = system.Population ?? 0;

            foreach (var front in FrontsIn(report.Name, row, system, standings, campaign, conflicts.GetValueOrDefault(row.System), now))
            {
                fronts.Add((front, population));
            }
        }

        return
        [
            .. fronts
                .OrderBy(entry => entry.Front.Kind)
                .ThenByDescending(entry => entry.Front.Stake.Length > 0)
                .ThenByDescending(entry => entry.Front.Controls)
                .ThenByDescending(entry => entry.Population)
                .ThenBy(entry => entry.Front.Margin ?? double.MaxValue)
                .ThenBy(entry => entry.Front.System, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Front),
        ];
    }

    private static IEnumerable<Front> FrontsIn(
        string faction,
        FactionRow row,
        FactionSystem system,
        SystemStandings standings,
        Campaign campaign,
        FactionConflict? conflict,
        DateTimeOffset now)
    {
        var intent = campaign.IntentFor(row.System, row.Controls);
        var rivals = Rivals(faction, row, system, standings);
        var nearest = rivals.Count == 0 ? default : rivals[0];

        Front Make(FrontKind kind, string reason, string? opponent, double? margin, string stake = "") =>
            new(kind, row.System, reason, intent, row.Controls, opponent, stake, margin, row.Updated, row.OwnReading);

        if (conflict is not null)
        {
            yield return Make(
                FrontKind.Conflict,
                ConflictReason(conflict),
                conflict.Kind == OpponentKind.Named ? conflict.Opponent : null,
                conflict.Visited ? conflict.OursWon - conflict.TheirsWon : null,
                conflict.OurStake) with { Conflict = conflict };
        }

        var coup = row.Controls && rivals.FirstOrDefault(rival => rival.Influence >= FrontThresholds.CoupApproach) is { Name: not null } threat
            ? threat
            : default;

        if (coup.Name is not null)
        {
            yield return Make(
                FrontKind.Coup,
                $"{coup.Name} is at {Percent(coup.Influence)}, {(coup.Influence >= FrontThresholds.Coup ? "past" : "near")} the "
                + $"{Percent(FrontThresholds.Coup)} at which it can take control by a coup.",
                coup.Name,
                FrontThresholds.Coup - coup.Influence);
        }

        if (Status(row, RetreatState) is { } retreat)
        {
            yield return Make(FrontKind.Retreat, $"Retreat is {retreat} here.", null, Lead(row, nearest));
        }

        if (row.Controls && !row.InConflict && coup.Name is null && FrontThresholds.Margin(intent) is { } margin && Lead(row, nearest) is { } lead && lead < margin)
        {
            yield return Make(
                FrontKind.ThinMargin,
                $"The lead over {nearest.Name} is {Points(lead)}, under the {Points(margin)} kept for {CampaignCapability.Describe(intent)}.",
                nearest.Name,
                lead);
        }

        var bad = row.Active.Where(BadStates.Contains).Select(state => (state, "active"))
            .Concat(row.Pending.Where(BadStates.Contains).Select(state => (state, "pending")))
            .ToList();

        if (bad.Count > 0)
        {
            yield return Make(
                FrontKind.BadState,
                string.Join(" ", bad.Select(entry => $"{entry.state} is {entry.Item2}.")),
                null,
                Lead(row, nearest)) with { States = [.. bad.Select(entry => entry.state)] };
        }

        if (intent == SystemIntent.StayUnder75 && row.Influence is { } influence && influence >= FrontThresholds.ExpansionApproach)
        {
            yield return Make(
                FrontKind.UnwantedExpansion,
                $"Influence is {Percent(influence)}, near the {Percent(FrontThresholds.Expansion)} at which the faction expands, and the intent is to keep under it.",
                null,
                FrontThresholds.Expansion - influence);
        }

        if (Status(row, ExpansionState) is { } expansion)
        {
            yield return Make(FrontKind.Expansion, $"Expansion is {expansion} here.", null, Lead(row, nearest));
        }

        if (intent != SystemIntent.LeaveAlone && row.Updated is { } updated && now - updated > FrontThresholds.StaleAfter)
        {
            yield return Make(
                FrontKind.Stale,
                $"The newest report of this system is {(int)(now - updated).TotalDays} days old.",
                null,
                -(now - updated).TotalDays);
        }
    }

    /// <summary>The other factions' influence, largest first, all from the same source as the row's own influence.</summary>
    private static List<(string? Name, double Influence)> Rivals(string faction, FactionRow row, FactionSystem system, SystemStandings standings)
    {
        IEnumerable<(string? Name, double? Influence)> others = row.OwnReading && standings.Latest(row.System) is { } reading
            ? reading.Factions
                .Where(other => !string.Equals(other.Name, faction, StringComparison.OrdinalIgnoreCase))
                .Select(other => ((string?)other.Name, (double?)other.Influence))
            : system.Others.Select(other => ((string?)other.Name, other.Influence));

        return
        [
            .. others
                .Where(other => other.Influence is not null)
                .Select(other => (other.Name, other.Influence!.Value))
                .OrderByDescending(other => other.Item2)
                .ThenBy(other => other.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static double? Lead(FactionRow row, (string? Name, double Influence) nearest) =>
        row.Influence is { } ours && nearest.Name is not null ? ours - nearest.Influence : null;

    private static string? Status(FactionRow row, string state) =>
        row.Active.Contains(state) ? "active" : row.Pending.Contains(state) ? "pending" : null;

    private static string ConflictReason(FactionConflict conflict)
    {
        var against = conflict.Kind switch
        {
            OpponentKind.Named => conflict.Opponent,
            OpponentKind.OneOf => $"one of {conflict.Candidates.Count} factions",
            _ => "a faction not yet known",
        };

        var reason = $"{conflict.Type} against {against}, {(conflict.Active ? "active" : "pending")}.";

        if (conflict.Visited)
        {
            reason += $" Days won {conflict.OursWon} to {conflict.TheirsWon}; {FrontThresholds.ConflictDaysToWin} of {FrontThresholds.ConflictDays} wins it.";
        }

        if (conflict.OurStake.Length > 0)
        {
            reason += $" At stake: {conflict.OurStake}.";
        }

        return reason;
    }

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Points(double fraction)
    {
        var points = (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture);

        return points == "1" ? "1 point" : $"{points} points";
    }
}
