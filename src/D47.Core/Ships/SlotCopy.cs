using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// Copying a slot's plan to another slot on the same ship (remediation.md 15, item 1).
/// <para>
/// Reported as "I expected to drag Medium Hardpoint 3 onto Small Hardpoint 1 and couldn't". The
/// rules live here rather than in the panel because they are facts about the game — which slots
/// can take which modules, and what a module resolves to at a smaller size — and because a drag
/// that silently does nothing is indistinguishable from a broken feature, so they are worth
/// testing without a window.
/// </para>
/// <para>
/// <b>There is no partial-copy case to have a policy about.</b> <c>Blueprints.tsv</c> carries no
/// size, class or rating: a blueprint belongs to a module <em>name</em>, and downsizing preserves
/// the name. So the engineering and the experimental always transfer, and the question the item
/// reserved — copy what transfers, or refuse — describes something that cannot happen.
/// </para>
/// </summary>
public static class SlotCopy
{
    /// <summary>
    /// Whether a plan may be dragged from one slot to another, and what it would become.
    /// <para>
    /// Null where the drop is not allowed, which is what a target that never highlights is drawn
    /// from: an invalid target discovered while the mouse is still down beats a dialog afterwards.
    /// </para>
    /// </summary>
    public static SlotPlan? Resolve(SlotPlan plan, ShipSlot from, ShipSlot to)
    {
        // Same kind only, and Core Internal is neither draggable nor a target — a power plant
        // socket and a hardpoint have nothing to say to each other.
        if (from.Kind != to.Kind
            || from.Kind == ShipSlotKind.Core
            || string.Equals(from.Name, to.Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var moved = plan with { Slot = to.Name };

        // A plan with no exact variant needs no resizing: "a pulse laser, I do not mind which" is
        // met by whatever the target slot takes.
        if (plan.Variant is not { Length: > 0 } variant)
        {
            return moved;
        }

        return Resize(variant, to) is { } fitted
            ? moved with { Variant = fitted.Symbol, Module = fitted.Name }
            : null;
    }

    /// <summary>
    /// The same module at the largest size the target slot will take, or null where it does not
    /// come small enough.
    /// <para>
    /// <b>It searches rather than clamps</b>, and that is not a refinement. Ten module names have
    /// an interior hole in their size run — six limpet controllers are odd sizes only, 1, 3, 5 and
    /// 7; the Planetary Vehicle Hangar is 2, 4 and 6 — so
    /// <c>min(slotSize, moduleMax)</c> resolves a size 7 Collector Limpet Controller onto a size 4
    /// slot as size 4, which does not exist.
    /// </para>
    /// <para>
    /// And 20 of 60 hardpoint names do not come in size 1 at all: a Plasma Accelerator is 2 to 4,
    /// a Pacifier Frag-Cannon size 3 only. Dropped on a Small hardpoint they resolve to nothing,
    /// which is the one real failure this can have.
    /// </para>
    /// </summary>
    public static ModuleSpecification? Resize(string variant, ShipSlot to)
    {
        if (EliteSpecifications.Module(variant) is not { } module)
        {
            return null;
        }

        // What the target slot will actually take, which already accounts for its restrictions and
        // for a module that must fill its slot exactly.
        var offered = EliteSpecifications.ModulesFor(to);

        return offered
            .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => Same(candidate.Mount, module.Mount))
            .Where(candidate => string.Equals(candidate.Rating, module.Rating, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Class ?? 0)
            .FirstOrDefault()

            // The rating is preserved where the target has one, and given up rather than the copy
            // being refused where it does not: a Commander dragging a 2F pulse laser onto a small
            // hardpoint wants a small pulse laser, and which letter it comes in is the lesser of
            // the two facts they stated.
            ?? offered
                .Where(candidate => string.Equals(candidate.Name, module.Name, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => Same(candidate.Mount, module.Mount))
                .OrderByDescending(candidate => candidate.Class ?? 0)
                .FirstOrDefault();
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
