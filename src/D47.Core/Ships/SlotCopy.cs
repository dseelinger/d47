using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>Copying a slot's plan to another slot on the same ship (remediation.md 15, item 1).</summary>
public static class SlotCopy
{
    /// <summary>Whether a plan may be dragged from one slot to another, and what it would become.</summary>
    public static SlotPlan? Resolve(SlotPlan plan, ShipSlot from, ShipSlot to)
    {
        // Same kind only, and Core Internal is neither draggable nor a target — a power plant socket and a
        // hardpoint have nothing to say to each other.
        if (from.Kind != to.Kind
            || from.Kind == ShipSlotKind.Core
            || string.Equals(from.Name, to.Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var moved = plan with { Slot = to.Name };

        // A plan with no exact variant needs no resizing: "a pulse laser, I do not mind which" is met by
        // whatever the target slot takes.
        if (plan.Variant is not { Length: > 0 } variant)
        {
            return moved;
        }

        return Resize(variant, to) is { } fitted
            ? moved with { Variant = fitted.Symbol, Module = fitted.Name }
            : null;
    }

    /// <summary>
    /// The same module at the largest size the target slot will take, or null where it does not come
    /// small enough.
    /// </summary>
    public static ModuleSpecification? Resize(string variant, ShipSlot to)
    {
        if (EliteSpecifications.Module(variant) is not { } module)
        {
            return null;
        }

        // What the target slot will actually take, which already accounts for its restrictions and for a
        // module that must fill its slot exactly.
        var offered = EliteSpecifications.ModulesFor(to);

        return offered
            .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => Same(candidate.Mount, module.Mount))
            .Where(candidate => string.Equals(candidate.Rating, module.Rating, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Class ?? 0)
            .FirstOrDefault()

            // The rating is preserved where the target has one, and given up rather than the copy being
            // refused where it does not: a Commander dragging a 2F pulse laser onto a small hardpoint wants a
            // small pulse laser, and which letter it comes in is the lesser of the two facts they stated.
            ?? offered
                .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => Same(candidate.Mount, module.Mount))
                .OrderByDescending(candidate => candidate.Class ?? 0)
                .FirstOrDefault();
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
