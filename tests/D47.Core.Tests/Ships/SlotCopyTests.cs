using D47.Core.Knowledge;
using D47.Core.Ships;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// Dragging a plan from one slot to another (remediation.md 15, item 1).
/// <para>
/// <b>The reported example is the acceptance test and it is better than an invented one</b>, for a
/// reason worth keeping: the size rule resolves the dragged module to the one the target already
/// has, so the module half of the copy is a no-op and the whole value is the engineering. An
/// implementation that skipped the copy when the resolved module matched would do nothing here and
/// look exactly like the bug that was reported.
/// </para>
/// </summary>
public class SlotCopyTests
{
    private static ShipSlot Slot(string hull, string name) =>
        EliteSpecifications.Slots(hull).First(slot =>
            string.Equals(slot.Name, name, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheReportedDragCopiesTheEngineeringOntoTheModuleAlreadyThere()
    {
        // Medium Hardpoint 3 carries a plan for a 2F Pulse Laser turreted, grade 5 Long Range,
        // Flow Control. Small Hardpoint 1 has no plan and a fitted 1G Pulse Laser turreted.
        var plan = new SlotPlan("MediumHardpoint3", "Long Range Weapon", 5)
        {
            Module = "Pulse Laser",
            Variant = "hpt_pulselaser_turret_medium",
            Experimental = "Flow Control",
        };

        var moved = SlotCopy.Resolve(
            plan,
            Slot("type9_military", "MediumHardpoint3"),
            Slot("type9_military", "SmallHardpoint1"));

        Assert.NotNull(moved);
        Assert.Equal("SmallHardpoint1", moved!.Slot);

        // The module resolves to the small one — which is what the target already has, so the
        // whole point of the drag is the three lines below it.
        Assert.Equal("hpt_pulselaser_turret_small", moved.Variant);
        Assert.Equal("Long Range Weapon", moved.Blueprint);
        Assert.Equal(5, moved.Grade);
        Assert.Equal("Flow Control", moved.Experimental);
    }

    [Fact]
    public void TheEngineeringAlwaysTransfersBecauseBlueprintsHaveNoSize()
    {
        // The decision the item reserved does not exist: `Blueprints.tsv` carries no size, class or
        // rating, so a blueprint belongs to a module *name* and downsizing preserves the name.
        var small = EliteSpecifications.Module("hpt_pulselaser_turret_small");
        var medium = EliteSpecifications.Module("hpt_pulselaser_turret_medium");

        Assert.NotNull(small);
        Assert.NotNull(medium);

        Assert.Equal(
            BlueprintCatalogue.For(medium)?.Select(recipe => recipe.Name).Order(StringComparer.Ordinal),
            BlueprintCatalogue.For(small)?.Select(recipe => recipe.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AModuleThatDoesNotComeSmallEnoughIsRefusedRatherThanShrunk()
    {
        // 20 of 60 hardpoint names do not come in size 1. A Plasma Accelerator is 2 to 4, so
        // dropping one on a Small hardpoint resolves to nothing — and that is what greys the
        // target during the drag rather than explaining itself afterwards.
        var plan = new SlotPlan("MediumHardpoint1", "Long Range Weapon", 5)
        {
            Module = "Plasma Accelerator",
            Variant = "hpt_plasmaaccelerator_fixed_medium",
        };

        Assert.Null(SlotCopy.Resolve(
            plan,
            Slot("type9_military", "MediumHardpoint1"),
            Slot("type9_military", "SmallHardpoint1")));
    }

    [Fact]
    public void LargestFittingSizeSearchesRatherThanClamps()
    {
        // **Ten module names have an interior hole in their size run.** Six limpet controllers are
        // odd sizes only, so `min(slotSize, moduleMax)` would resolve a size 7 Collector Limpet
        // Controller onto a size 4 compartment as size 4, which does not exist.
        var fitted = SlotCopy.Resize(
            "int_dronecontrol_collection_size7_class1",
            Slot("type9_military", "Slot05_Size4"));

        Assert.NotNull(fitted);
        Assert.Equal(3, fitted!.Class);
        Assert.Equal("Collector Limpet Controller", fitted.Name);
    }

    [Fact]
    public void OnlyWithinOneKindAndNeverACoreInternal()
    {
        var plan = new SlotPlan("MediumHardpoint1") { Module = "Pulse Laser" };

        // Hardpoint to utility mount is refused, and so is anything touching a core socket.
        Assert.Null(SlotCopy.Resolve(
            plan,
            Slot("type9_military", "MediumHardpoint1"),
            Slot("type9_military", "TinyHardpoint1")));

        Assert.Null(SlotCopy.Resolve(
            plan with { Slot = "PowerPlant" },
            Slot("type9_military", "PowerPlant"),
            Slot("type9_military", "PowerDistributor")));

        // And a slot cannot be dragged onto itself.
        Assert.Null(SlotCopy.Resolve(
            plan,
            Slot("type9_military", "MediumHardpoint1"),
            Slot("type9_military", "MediumHardpoint1")));
    }

    [Fact]
    public void APlanWithNoExactVariantNeedsNoResizing()
    {
        // "A pulse laser, I do not mind which" is a real plan and is met by whatever the target
        // takes, so there is nothing to resolve and nothing to refuse.
        var plan = new SlotPlan("MediumHardpoint1", "Long Range Weapon", 5) { Module = "Pulse Laser" };

        var moved = SlotCopy.Resolve(
            plan,
            Slot("type9_military", "MediumHardpoint1"),
            Slot("type9_military", "SmallHardpoint1"));

        Assert.NotNull(moved);
        Assert.Null(moved!.Variant);
        Assert.Equal("Pulse Laser", moved.Module);
    }
}
