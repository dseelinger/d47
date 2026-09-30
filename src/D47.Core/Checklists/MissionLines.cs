using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>
/// The universal lines the mission board writes: one per delivery or collect mission with a commodity
/// and a count, settled by <c>CargoDepot</c> and removed when the mission leaves the board.
/// </summary>
public static class MissionLines
{
    /// <summary>Whether a mission gets a line. Elite reports no progress for courier or passenger work.</summary>
    public static bool Carries(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return !mission.PassengerMission
            && mission.CommodityLocalised is { Length: > 0 }
            && mission.Count is > 0
            && (mission.Name.StartsWith("Mission_Delivery", StringComparison.OrdinalIgnoreCase)
                || mission.Name.StartsWith("Mission_Collect", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether every item has been handed over.</summary>
    public static bool Delivered(Mission mission) =>
        mission.Cargo is { Total: > 0 } cargo && cargo.Delivered >= cargo.Total;

    /// <summary>The line for one mission, keyed on its <c>MissionID</c>.</summary>
    public static ChecklistItem Line(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var intent = new ChecklistIntent(
            ChecklistIntentKind.Commodity, mission.Id.ToString(CultureInfo.InvariantCulture))
        {
            Detail = mission.CommodityLocalised,
            Quantity = mission.Count,
        };

        var text = $"Deliver {mission.Count} {mission.CommodityLocalised}";

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.Mission,
            Text = mission.Destination is { } destination ? $"{text} to {destination}" : text,
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    /// <summary>
    /// The lines the board asks for. A mission already delivered in full gets a line only where
    /// <paramref name="present"/> holds one, so a line removed on completion is not written back.
    /// </summary>
    public static IReadOnlyList<ChecklistItem> Wanted(MissionBoard board, Func<string, bool> present)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(present);

        return
        [
            .. board.Missions
                .Where(Carries)
                .Select(mission => (Mission: mission, Line: Line(mission)))
                .Where(entry => !Delivered(entry.Mission) || present(entry.Line.Key))
                .Select(entry => entry.Line),
        ];
    }

    /// <summary>
    /// How far a mission's delivery has got: <c>CargoDepot</c> where Elite has written one, and before
    /// that the hold's count of the commodity, joined on the symbol.
    /// </summary>
    internal static ChecklistVerdict? Verdict(ChecklistIntent intent, CommanderGameState state)
    {
        if (!long.TryParse(intent.Subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            || state.Missions.For(id) is not { } mission)
        {
            return null;
        }

        var commodity = mission.CommodityLocalised ?? intent.Detail;

        if (mission.Cargo is { Total: > 0 } cargo)
        {
            return Delivered(mission)
                ? new ChecklistVerdict(ChecklistState.Done, $"All {cargo.Total} {commodity} delivered.")
                : new ChecklistVerdict(
                    ChecklistState.Open, $"{cargo.Delivered} of {cargo.Total} {commodity} delivered.");
        }

        var wanted = mission.Count ?? intent.Quantity ?? 0;
        var held = state.Hold.Of(mission.Commodity);

        return new ChecklistVerdict(
            ChecklistState.Open,
            $"None delivered yet. {Math.Min(held, wanted)} of {wanted} {commodity} in the hold.");
    }
}
