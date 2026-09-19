using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>
/// What is actually fitted in a checklist item's slot — shared between engineer availability (<see
/// cref="EngineersHere"/>) and engineering cost (<see cref="EngineeringPlan"/>), so a blueprint name that
/// belongs to several module kinds is costed and offered against the one actually there.
/// </summary>
internal static class FittedModule
{
    /// <summary>The specification of what is fitted in this item's slot, or null where d47 cannot tell.</summary>
    public static ModuleSpecification? Of(ChecklistItem item, CommanderGameState? state) =>
        Fitted(item, state) is { } fitted ? EliteSpecifications.Module(fitted.Item) : null;

    /// <summary>Whether there is anything in this item's slot for an engineer to work on (GitHub issue 41).</summary>
    public static bool IsFitted(ChecklistItem item, CommanderGameState? state) =>
        LoadoutFor(item, state) is null || Fitted(item, state) is not null;

    /// <summary>The module in this item's slot, or null where the slot is empty or the ship unseen.</summary>
    private static ShipModule? Fitted(ChecklistItem item, CommanderGameState? state)
    {
        if (item.Intent?.Subject is not { Length: > 0 } slot)
        {
            return null;
        }

        return LoadoutFor(item, state) is not { } loadout
            ? null
            : loadout.Modules.FirstOrDefault(module =>
                string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Which loadout this item's modules are read from: the live one for the ship being flown, and for
    /// a line that is not about a ship in particular because there is nothing better to offer it; the
    /// remembered one for any other ship.
    /// </summary>
    private static ShipLoadout? LoadoutFor(ChecklistItem item, CommanderGameState? state)
    {
        if (state is null)
        {
            return null;
        }

        if (item.Scope.Group != ChecklistGroup.Ship)
        {
            return state.Ship;
        }

        return ChecklistEvaluator.IsActive(item.Scope, state.Ship)
            ? state.Ship
            : int.TryParse(
                item.Scope.Key,
                global::System.Globalization.NumberStyles.Integer,
                global::System.Globalization.CultureInfo.InvariantCulture,
                out var shipId)
                ? state.Loadouts.For(shipId)?.Loadout
                : null;
    }
}
