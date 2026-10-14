using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.Core.Adventures;

public enum ShipObjectiveKind
{
    ShowOwned,
    ShowPlan,
    ShowShips,
    Plan,
}

/// <summary>A button under a <see cref="TriggerKind.Board"/> objective: its label, and the fleet entry or hull it opens.</summary>
public sealed record ShipObjectiveButton(ShipObjectiveKind Kind, string Label, FleetEntry? Entry, string HullName);

public static class ShipObjective
{
    /// <summary>The button for a board objective given the fleet, or null for any other objective or a hull with no name.</summary>
    public static ShipObjectiveButton? For(AdventureTrigger trigger, IReadOnlyList<FleetEntry> fleet)
    {
        if (trigger.Kind != TriggerKind.Board || EliteSpecifications.HullName(trigger.ShipType) is not { } name)
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
