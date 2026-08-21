using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Two things a slot row was getting wrong about a plan (reported 2026-08-20).
/// <para>
/// <b>"Module Reinforcement packages can't be engineered"</b>, and d47's own tables always agreed:
/// <c>Blueprints.tsv</c> names 38 engineerable module groups and it is not one of them. The
/// chooser already refused it; the <em>drag</em> did not. <see cref="SlotCopy"/> resizes the
/// module and carries the roll, which is sound while the plan names a module — but a plan naming
/// none ("grade 5 Heavy Duty Hull Reinforcement, I do not mind which module") dropped on a
/// compartment holding a Module Reinforcement Package produced a row reading
/// <c>5D Module Reinforcement · Heavy Duty Hull Reinforcement (G5)</c>.
/// </para>
/// <para>
/// <b>"Size five is missing the 5D designation."</b> Same row, other half: a planned module was
/// named from the plan's <c>Module</c> — the group — while a fitted one came through
/// <c>ModuleName</c>, which carries the class and rating. Two different amounts of fact in one
/// column.
/// </para>
/// </summary>
public class ARollIsNotDraggedOntoAModuleThatCannotTakeItTests
{
    /// <summary>
    /// An Anaconda the Commander is sitting in, with a hull reinforcement in one size 5
    /// compartment and a <b>module</b> reinforcement in another. Both real slots on that hull, and
    /// this is the arrangement the report came from.
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

        // The plan as the panel writes one for "grade 5 Heavy Duty Hull Reinforcement here", with
        // no module named — which is the shape that had no module to check the roll against.
        ships.Plan(build.Id, new SlotPlan("Slot05_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
        });

        return (new ShipsMode(ships, checklists, () => live), ships);
    }

    private static string Item(ShipPlanService ships) => ships.Store.Builds[0].Id;

    [Fact]
    public void TheDragIsRefusedAndSaysWhy()
    {
        var (mode, ships) = InTheAnaconda();
        var item = Item(ships);

        Assert.False(mode.CanCopy(item, "Slot05_Size5", "Slot07_Size5"));

        var said = mode.Copy(item, "Slot05_Size5", "Slot07_Size5");

        // Names the module rather than the slot, because that is the fact the Commander cannot see
        // by looking at a row that shows the roll they dragged.
        Assert.Contains("Module Reinforcement", said, StringComparison.Ordinal);
        Assert.Contains("no engineering", said, StringComparison.Ordinal);

        // And nothing was written. A refusal that half-applies is worse than either answer.
        Assert.Null(ships.Store.Builds[0].For("Slot07_Size5"));
    }

    [Fact]
    public void ADragOntoAModuleThatCanTakeTheRollStillWorks()
    {
        // The rule this must not disarm. Slot06 holds nothing d47 can see, so there is no module
        // to refuse on, and Slot05's own kind and size match.
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

        // "4E Hull Reinforcement Package", not "Hull Reinforcement Package". Nothing is fitted in
        // that slot, so this is the planned name and it used to arrive without its designation.
        Assert.NotNull(row.Parts?.Module);
        Assert.StartsWith("4E ", row.Parts.Module, StringComparison.Ordinal);
        Assert.Contains("Hull Reinforcement", row.Parts.Module, StringComparison.Ordinal);
    }

    [Fact]
    public void APlanThatDeclinedTheVariantStillReadsAsTheGroup()
    {
        // The honest remainder: "a hull reinforcement, I do not mind which" has no class to print,
        // and inventing one would be inventing the plan.
        var (mode, ships) = InTheAnaconda();

        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot09_Size4")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Module = "Hull Reinforcement Package",
        });

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot09_Size4", StringComparison.Ordinal));

        Assert.Equal("Hull Reinforcement Package", row.Parts?.Module);
    }

    [Fact]
    public void WhereThePlanNamesNoModuleTheFittedOneIsNamed()
    {
        // The fallback, and the half that did not change. Slot05's plan is "grade 5 Heavy Duty
        // Hull Reinforcement, I do not mind which module" — so there is no planned module to name
        // and the row names what is in the slot, with its own class and rating.
        var (mode, ships) = InTheAnaconda();

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot05_Size5", StringComparison.Ordinal));

        Assert.StartsWith("5D ", row.Parts?.Module, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plan that names a module outranks what is fitted, on the row (reported 2026-08-20:
    /// <i>"I just changed this to 6A, but it still says 6D"</i>).
    /// <para>
    /// The row already carries the plan's roll and the dot that says a plan exists, so naming the
    /// fitted module beside them described a thing that does not exist. The fitted module is not
    /// lost — the slot drill names it under its own <c>Fitted</c> heading, beside <c>Planned</c>,
    /// which is the page where the two are meant to be told apart.
    /// </para>
    /// </summary>
    [Fact]
    public void APlannedModuleOutranksTheFittedOneOnTheRow()
    {
        var (mode, ships) = InTheAnaconda();

        // Slot07 holds a 5D Module Reinforcement. The Commander plans a hull reinforcement there
        // instead, and names which one.
        ships.Plan(ships.Store.Builds[0].Id, new SlotPlan("Slot07_Size5")
        {
            Blueprint = "Heavy Duty Hull Reinforcement",
            Grade = 5,
            Module = "Hull Reinforcement Package",
            Variant = "int_hullreinforcement_size5_class1",
        });

        var row = mode.Slots(Item(ships))
            .Single(candidate => candidate.Key.EndsWith("Slot07_Size5", StringComparison.Ordinal));

        Assert.NotNull(row.Parts?.Module);
        Assert.Contains("Hull Reinforcement", row.Parts.Module, StringComparison.Ordinal);
        Assert.DoesNotContain("Module Reinforcement", row.Parts.Module, StringComparison.Ordinal);

        // The class the Commander chose, not the class of the thing being replaced.
        Assert.StartsWith("5E ", row.Parts.Module, StringComparison.Ordinal);
    }
}
