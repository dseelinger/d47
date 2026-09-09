using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Two things a slot row was getting wrong about a plan.</summary>
public class ARollIsNotDraggedOntoAModuleThatCannotTakeItTests
{
    /// <summary>
    /// An Anaconda the Commander is sitting in, with a hull reinforcement in one size 5 compartment and
    /// a module reinforcement in another.
    /// </summary>
    private static (ShipsMode Mode, ShipPlanService Ships) InTheAnaconda()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-rollable-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """
                     {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"anaconda","ShipID":51,
                      "Modules":[
                        {"Slot":"Slot05_Size5","Item":"int_hullreinforcement_size5_class2","On":true,"Health":1.0},
                        {"Slot":"Slot07_Size5","Item":"int_modulereinforcement_size5_class2","On":true,"Health":1.0}]}
                     """,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        var build = ships.BuildFor(51, "anaconda", "Flamebrand");

        // The plan as the panel writes one for "grade 5 Heavy Duty Hull Reinforcement here", with no module
        // named — which is the shape that had no module to check the roll against.
        ships.Plan(build.Id, new SlotPlan("Slot05_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
        });

        return (new ShipsMode(ships, checklists, () => live), ships);
    }

    private static string Item(ShipPlanService ships) => ships.Store.Builds[0].Id;

    /// <summary> A drag carries the module the row was showing. </summary>
    [Fact]
    public void TheDragCarriesTheModuleTheRowWasShowing()
    {
        var (mode, ships) = InTheAnaconda();
        var item = Item(ships);

        Assert.True(mode.CanCopy(item, "Slot05_Size5", "Slot07_Size5"));

        _ = mode.Copy(item, "Slot05_Size5", "Slot07_Size5");

        var landed = ships.Store.Builds[0].For("Slot07_Size5");

        Assert.NotNull(landed);
        Assert.Equal("Heavy Duty Hull Reinforcement", landed.Blueprint);

        // The module the row was showing, not the module reinforcement sitting in the target.
        Assert.Contains("Hull Reinforcement", landed.Module!, StringComparison.Ordinal);
        Assert.DoesNotContain("Module Reinforcement", landed.Module!, StringComparison.Ordinal);
        Assert.NotNull(landed.Variant);
    }

    /// <summary>
    /// And where no module can be carried, the roll is still refused rather than attributed to whatever
    /// happens to be in the target.
    /// </summary>
    [Fact]
    public void WhereNoModuleCanBeCarriedTheRollIsStillRefused()
    {
        var (mode, ships) = InTheAnaconda();
        var item = Item(ships);

        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot06_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
        });

        Assert.False(mode.CanCopy(item, "Slot06_Size5", "Slot07_Size5"));

        var said = mode.Copy(item, "Slot06_Size5", "Slot07_Size5");

        Assert.Contains("Module Reinforcement", said, StringComparison.Ordinal);
        Assert.Contains("no engineering", said, StringComparison.Ordinal);
        Assert.Null(ships.Store.Builds[0].For("Slot07_Size5"));
    }

    [Fact]
    public void ADragOntoAModuleThatCanTakeTheRollStillWorks()
    {
        // The rule this must not disarm.
        var (mode, ships) = InTheAnaconda();
        var item = Item(ships);

        Assert.True(mode.CanCopy(item, "Slot05_Size5", "Slot06_Size5"));

        _ = mode.Copy(item, "Slot05_Size5", "Slot06_Size5");

        Assert.Equal("Heavy Duty Hull Reinforcement", ships.Store.Builds[0].For("Slot06_Size5")?.Blueprint);
    }

    [Fact]
    public void APlannedModuleIsNamedWithItsClassAndRating()
    {
        var (mode, ships) = InTheAnaconda();
        var build = ships.Store.Builds[0];

        // A plan that names the exact module, which is what the chooser writes.
        ships.Plan(build.Id, new SlotPlan("Slot08_Size4")
        {
            Blueprint = "Lightweight",
            Grade = 5,
            Module = "Hull Reinforcement Package",
            Variant = "int_hullreinforcement_size4_class1",
        });

        var row = mode.Slots(Item(ships)).Single(candidate => candidate.Key.EndsWith("Slot08_Size4", StringComparison.Ordinal));

        // "4E Hull Reinforcement Package", not "Hull Reinforcement Package".
        Assert.NotNull(row.Parts?.Plan?.Module);
        Assert.StartsWith("4E ", row.Parts.Plan.Module, StringComparison.Ordinal);
        Assert.Contains("HRP", row.Parts.Plan.Module, StringComparison.Ordinal);
        Assert.Equal("4E Hull Reinforcement Package", row.Parts.Plan.Long);
    }

    [Fact]
    public void APlanThatDeclinedTheVariantStillReadsAsTheGroup()
    {
        // The honest remainder: "a hull reinforcement, I do not mind which" has no class to print, and
        // inventing one would be inventing the plan.
        var (mode, ships) = InTheAnaconda();

        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot09_Size4")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Module = "Hull Reinforcement Package",
        });

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot09_Size4", StringComparison.Ordinal));

        Assert.Equal("HRP", row.Parts?.Plan?.Module);
        Assert.Equal("Hull Reinforcement Package", row.Parts!.Plan!.Long);
    }

    [Fact]
    public void WhereThePlanNamesNoModuleTheFittedOneIsNamed()
    {
        // Slot05's plan is "grade 5 Heavy Duty Hull Reinforcement, I do not mind which module", so there is
        // no planned module to name — and the Current column names what is in the slot, with its own class
        // and rating.
        var (mode, ships) = InTheAnaconda();

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot05_Size5", StringComparison.Ordinal));

        Assert.StartsWith("5D ", row.Parts?.Current.Module, StringComparison.Ordinal);
    }

    /// <summary>
    /// A row holds both the module on the hull and the module planned for the slot (the Commander's
    /// ruling, 2026-08-20: "can you hold both? one is reality, the other is the goal").
    /// </summary>
    [Fact]
    public void ARowHoldsBothTheFittedModuleAndThePlannedOne()
    {
        var (mode, ships) = InTheAnaconda();

        // Slot07 holds a 5D Module Reinforcement.
        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot07_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Module = "Hull Reinforcement Package",
            Variant = "int_hullreinforcement_size5_class1",
        });

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot07_Size5", StringComparison.Ordinal));

        // The goal, in its own column.
        Assert.StartsWith("5E ", row.Parts?.Plan?.Module, StringComparison.Ordinal);
        Assert.Contains("HRP", row.Parts!.Plan!.Module!, StringComparison.Ordinal);

        // And reality beside it, in its own — which is what the table made unconditional rather than a second
        // name grown onto a row that had room for one.
        Assert.Contains("MRP", row.Parts.Current.Module!, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowSaysOneModuleWhereThePlanAndTheHullAgree()
    {
        // No second name where there is no second thing to say.
        var (mode, ships) = InTheAnaconda();

        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot05_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Module = "Hull Reinforcement Package",
            Variant = "int_hullreinforcement_size5_class2",
        });

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot05_Size5", StringComparison.Ordinal));

        Assert.Equal("5D HRP", row.Parts?.Current.Module);
        Assert.Equal("5D HRP", row.Parts!.Plan?.Module);
    }
}
