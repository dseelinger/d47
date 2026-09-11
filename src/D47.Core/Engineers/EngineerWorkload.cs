using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// Modules still to engineer, and who could finish each one (#137). Distinct from <see
/// cref="PlannedNeeds"/>, which counts rolls rather than modules and credits every candidate engineer
/// with the same roll regardless of what is already applied.
/// </summary>
public static class EngineerWorkload
{
    /// <summary>
    /// How many planned slots each engineer could finish, keyed on the journal's engineer id. A slot
    /// with an outstanding blueprint and an outstanding experimental effect still contributes at most 1
    /// to any one engineer's count.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Outstanding(
        IReadOnlyList<ShipBuild> ships,
        IReadOnlyList<OnFootBuild> onFoot,
        CommanderGameState? state)
    {
        var counts = new Dictionary<int, int>();

        foreach (var build in ships)
        {
            var remembered = state?.Loadouts.For(build.ShipId)?.Loadout;

            foreach (var slot in build.Slots.Where(slot => !slot.IsEmpty))
            {
                var fitted = remembered?.Modules.FirstOrDefault(module =>
                    string.Equals(module.Slot, slot.Slot, StringComparison.OrdinalIgnoreCase));

                Credit(counts, EngineersFor(slot, fitted, EffectiveModule(slot, fitted)));
            }
        }

        foreach (var build in onFoot)
        {
            var applied = AppliedModifications(build, state?.OnFoot);

            foreach (var slot in build.Slots.Where(slot => slot.Modification is { Length: > 0 }))
            {
                Credit(counts, EngineersForOnFoot(slot, build, applied));
            }
        }

        return counts;
    }

    private static void Credit(Dictionary<int, int> counts, IEnumerable<int> engineerIds)
    {
        foreach (var id in engineerIds.Distinct())
        {
            counts[id] = counts.GetValueOrDefault(id) + 1;
        }
    }

    /// <summary>
    /// The module actually in this slot, as a Blueprint table would name it: the exact variant the plan
    /// asked for, then the plan's own generic name, then whatever the remembered loadout has fitted
    /// there.
    /// </summary>
    private static string? EffectiveModule(SlotPlan slot, ShipModule? fitted)
    {
        if (slot.Variant is { Length: > 0 } variant && EliteSpecifications.Module(variant) is { } named)
        {
            return named.Name;
        }

        if (slot.Module is { Length: > 0 } module)
        {
            return module;
        }

        return fitted?.Item is { Length: > 0 } item ? EliteSpecifications.Module(item)?.Name : null;
    }

    /// <summary>Whether the planned blueprint is already applied at or above the planned grade.</summary>
    private static bool BlueprintApplied(SlotPlan slot, ShipModule? fitted)
    {
        if (slot.Blueprint is not { Length: > 0 } wanted)
        {
            return true;
        }

        if (fitted?.Blueprint is not { Length: > 0 } appliedSymbol
            || BlueprintCatalogue.NameOf(appliedSymbol) is not { Length: > 0 } appliedName
            || !string.Equals(appliedName, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return slot.Grade <= 0 || (fitted.BlueprintLevel ?? 0) >= slot.Grade;
    }

    /// <summary>Whether the planned experimental effect is already applied.</summary>
    private static bool ExperimentalApplied(SlotPlan slot, ShipModule? fitted) =>
        slot.Experimental is not { Length: > 0 } wanted
        || (fitted?.Experimental is { Length: > 0 } appliedName
            && string.Equals(appliedName, wanted, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Everybody who finishes this slot: rolls the outstanding blueprint, or applies the outstanding
    /// experimental effect where the blueprint is either applied already or theirs to roll too.
    /// </summary>
    private static IEnumerable<int> EngineersFor(SlotPlan slot, ShipModule? fitted, string? module)
    {
        var ids = new HashSet<int>();
        var blueprintDone = BlueprintApplied(slot, fitted);

        if (slot.Blueprint is { Length: > 0 } blueprint && !blueprintDone)
        {
            foreach (var name in PlannedNeeds.Rollers(blueprint, module, Grade(slot), slot.Engineer))
            {
                if (EngineerDirectory.ByName(name) is { } engineer)
                {
                    ids.Add(engineer.Id);
                }
            }
        }

        if (slot.Experimental is { Length: > 0 } experimental && !ExperimentalApplied(slot, fitted))
        {
            var appliers = PlannedNeeds.Rollers(experimental, module, null, slot.Engineer);

            var gate = slot.Blueprint is { Length: > 0 } wanted && !blueprintDone
                ? PlannedNeeds.Rollers(wanted, module, Grade(slot), slot.Engineer)
                    .ToHashSet(StringComparer.Ordinal)
                : null;

            foreach (var name in appliers)
            {
                if (gate is not null && !gate.Contains(name))
                {
                    continue;
                }

                if (EngineerDirectory.ByName(name) is { } engineer)
                {
                    ids.Add(engineer.Id);
                }
            }
        }

        return ids;
    }

    private static int? Grade(SlotPlan slot) => slot.Grade > 0 ? slot.Grade : null;

    /// <summary>
    /// The modification names already applied to the loadout this build costs, or null where the build
    /// is not the one currently worn — nothing can be discounted against a loadout that is not it.
    /// </summary>
    private static IReadOnlyList<string>? AppliedModifications(OnFootBuild build, OnFootLoadout? worn)
    {
        if (worn is not { IsKnown: true } || build.ItemId is not { } id)
        {
            return null;
        }

        if (build.Kind == OnFootKind.Suit)
        {
            return worn.SuitId == id
                ? [.. worn.SuitModifications.Where(mod => mod.IsNamed).Select(mod => mod.Name!)]
                : null;
        }

        return worn.Weapons.FirstOrDefault(weapon => weapon.ModuleId == id) is { } carried
            ? [.. carried.Modifications.Where(mod => mod.IsNamed).Select(mod => mod.Name!)]
            : null;
    }

    private static IEnumerable<int> EngineersForOnFoot(
        KitPlan slot, OnFootBuild build, IReadOnlyList<string>? applied)
    {
        var wanted = slot.Modification!;

        if (applied?.Any(name => string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return [];
        }

        var ids = new HashSet<int>();

        foreach (var name in PlannedNeeds.Rollers(wanted, build.Equipment, null, null))
        {
            if (EngineerDirectory.ByName(name) is { } engineer)
            {
                ids.Add(engineer.Id);
            }
        }

        return ids;
    }
}
