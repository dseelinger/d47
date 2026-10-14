using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.Core.Adventures;

public enum ShipObjectiveKind
{
    ShowOwned,
    ShowPlan,
    ShowShips,
    Plan,
    ShowEngineer,
    ShowUnsold,
    ShowSuits,
}

/// <summary>A button under an objective: its label, and the fleet entry, hull or engineer it opens.</summary>
public sealed record ShipObjectiveButton(ShipObjectiveKind Kind, string Label, FleetEntry? Entry, string HullName, Engineer? Engineer = null);

public static class ShipObjective
{
    /// <summary>The button for a board, engineer, data sale or suit mod objective, or null for any other, a hull with no name or an unknown engineer.</summary>
    public static ShipObjectiveButton? For(AdventureTrigger trigger, IReadOnlyList<FleetEntry> fleet) =>
        trigger.Kind switch
        {
            TriggerKind.Board => Board(trigger, fleet),
            TriggerKind.Engineer when EngineerDirectory.ByName(trigger.Engineer) is { } engineer
                => new(ShipObjectiveKind.ShowEngineer, $"Show {engineer.Name}", null, string.Empty, engineer),
            TriggerKind.DataSale => new(ShipObjectiveKind.ShowUnsold, "Show unsold data", null, string.Empty),
            TriggerKind.SuitMod => new(ShipObjectiveKind.ShowSuits, "Show suits", null, string.Empty),
            _ => null,
        };

    private static ShipObjectiveButton? Board(AdventureTrigger trigger, IReadOnlyList<FleetEntry> fleet)
    {
        if (EliteSpecifications.HullName(trigger.ShipType) is not { } name)
        {
            return null;
        }

        var hull = trigger.ShipType!;
        var matching = fleet.Where(entry => string.Equals(entry.Hull, hull, StringComparison.OrdinalIgnoreCase)).ToList();
        var owned = matching.Where(entry => entry.IsOwned).ToList();
        var planned = matching.Where(entry => !entry.IsOwned).ToList();

        if (owned.Count == 1 && planned.Count == 0)
        {
            return new(ShipObjectiveKind.ShowOwned, $"Show my {name}", owned[0], name);
        }

        if (planned.Count == 1 && owned.Count == 0)
        {
            return new(ShipObjectiveKind.ShowPlan, $"Show the {name} plan", planned[0], name);
        }

        if (matching.Count > 1)
        {
            return new(ShipObjectiveKind.ShowShips, "Show my ships", null, name);
        }

        return EliteSpecifications.Ship(name) is null
            ? null
            : new(ShipObjectiveKind.Plan, $"Plan a {name}", null, name);
    }
}
