using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>One system of the campaign faction as the Campaign page's Systems section draws it.</summary>
/// <param name="Lead">The faction's influence less the nearest rival's, as a fraction of 1; null when either is unknown.</param>
/// <param name="Frozen">A war, civil war or election is active or pending here, so influence does not move.</param>
/// <param name="State">The active states, then each pending one followed by "pending"; "None" when there are none.</param>
/// <param name="IntentIsDefault">No intent is set, so <paramref name="Intent"/> is the default for the system.</param>
/// <param name="Old">The galaxy report the row was read from is older than <see cref="FactionRow.OldAfter"/>.</param>
public sealed record CampaignSystemRow(
    string System,
    bool Controls,
    double? Influence,
    double? Lead,
    bool Frozen,
    string State,
    SystemIntent Intent,
    bool IntentIsDefault,
    DateTimeOffset? Updated,
    bool OwnReading,
    bool Old);

/// <summary>Builds the Systems rows of the campaign faction from its galaxy report, the Commander's readings and the intents set.</summary>
public static class CampaignSystemRows
{
    public const string NoState = "None";

    /// <summary>
    /// One row per system in the report, most influence first. A system the Commander has seen since the report takes
    /// its figures from that reading, the rival's included.
    /// </summary>
    public static IReadOnlyList<CampaignSystemRow> Of(
        FactionReport report,
        SystemStandings standings,
        IReadOnlyDictionary<string, SystemIntent> intents,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(intents);

        var systems = report.Systems
            .GroupBy(system => system.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return
        [
            .. FactionRows.Of(report, standings)
                .Select(row => RowOf(report.Name, row, systems[row.System], standings, intents, now)),
        ];
    }

    private static CampaignSystemRow RowOf(
        string faction,
        FactionRow row,
        FactionSystem system,
        SystemStandings standings,
        IReadOnlyDictionary<string, SystemIntent> intents,
        DateTimeOffset now)
    {
        var rivals = CampaignFronts.Rivals(faction, row, system, standings);
        var lead = rivals.Count == 0 ? null : CampaignFronts.Lead(row, rivals[0]);
        var set = intents.TryGetValue(row.System, out var intent);

        return new CampaignSystemRow(
            row.System,
            row.Controls,
            row.Influence,
            lead,
            row.InConflict,
            StateOf(row),
            set ? intent : Campaign.DefaultIntent(row.Controls),
            !set,
            row.Updated,
            row.OwnReading,
            row.IsOld(now));
    }

    private static string StateOf(FactionRow row)
    {
        var states = row.Active
            .Concat(row.Pending.Where(state => !row.Active.Contains(state)).Select(state => $"{state} pending"))
            .ToList();

        return states.Count == 0 ? NoState : string.Join(", ", states);
    }
}
