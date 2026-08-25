using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Loadout row draws a plan as though it were fitted, and its marker never clears
/// (GitHub issue 38).
/// <para>
/// Reported twice on 2026-08-24 as <i>"something IS fitted on oxen utility mount 8"</i> and
/// <i>"something IS fitted to oxen Optional Internal Compartment 4"</i>. Nothing was: Elite omits
/// empty slots from <c>Loadout</c>, so both really were empty and the checklist was right
/// throughout. The page was not.
/// </para>
/// <para>
/// And the second symptom of the same root: <i>"these have been engineered, the orange circles
/// should be gone, right?"</i> — against two slots rolled exactly as planned.
/// </para>
/// </summary>
public class APlanIsNotDrawnAsThoughItWereFittedTests
{
    /// <summary>
    /// One ship, one Loadout, and whatever plan the test wants on top of it. The Loadout names
    /// <c>Slot01_Size6</c> and nothing else, so every other slot on the hull is genuinely empty —
    /// which is the state the report was made against.
    /// </summary>
    private static (ShipsMode Mode, ShipPlanService Ships) Flying(params SlotPlan[] plans)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-plan-vs-fitted-tests"));

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
                     """{"timestamp":"2026-08-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",

                     // One module fitted, in Slot01. Everything else on this hull is empty, and
                     // Elite says so by not mentioning it.
                     """{"timestamp":"2026-08-24T09:00:00Z","event":"Loadout","Ship":"type9","ShipID":53,"ShipName":"oxen","ShipIdent":"OX-1","HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{"Slot":"Slot01_Size8","Item":"int_cargorack_size8_class1","On":true,"Priority":1,"Health":1.0}]}""",
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

        var build = ships.BuildFor(53, live.Ship!.Type!);

        foreach (var plan in plans)
        {
            ships.Plan(build.Id, plan);
        }

        return (new ShipsMode(ships, checklists, () => live), ships);
    }

    private static LoadoutRow Row(ShipsMode mode, string slot)
    {
        var ship = Assert.Single(mode.Items());

        return Assert.Single(
            mode.Slots(ship.Key),
            row => row.Key.EndsWith($"|{slot}", StringComparison.Ordinal));
    }

    /// <summary>
    /// The report. A plan in a slot Elite never mentioned is a plan for an <b>empty</b> slot, and
    /// the row has to say so — it used to be indistinguishable from a fitted module.
    /// </summary>
    [Fact]
    public void APlannedModuleInAnEmptySlotSaysTheSlotIsEmpty()
    {
        var (mode, _) = Flying(new SlotPlan("Slot04_Size5", Module: "Fuel Scoop"));

        var row = Row(mode, "Slot04_Size5");

        Assert.NotNull(row.Parts);
        Assert.True(row.Parts.NotFitted);
    }

    /// <summary>And a plan in a slot that really has something in it does not.</summary>
    [Fact]
    public void APlannedModuleInAFullSlotDoesNot()
    {
        var (mode, _) = Flying(new SlotPlan("Slot01_Size8", Module: "Cargo Rack"));

        var row = Row(mode, "Slot01_Size8");

        Assert.NotNull(row.Parts);
        Assert.False(row.Parts.NotFitted);
    }

    /// <summary>
    /// The marker means work left to do, not "a plan exists". A plan for an empty slot is entirely
    /// outstanding.
    /// </summary>
    [Fact]
    public void AnEmptySlotWithAPlanIsMarked()
    {
        var (mode, _) = Flying(new SlotPlan("Slot04_Size5", Module: "Fuel Scoop"));

        Assert.True(Row(mode, "Slot04_Size5").Marked);
    }

    /// <summary>
    /// <b>The second half of the report.</b> A plan asking only for a module, with that module on
    /// the hull, has nothing outstanding — so the dot goes out. It never used to.
    /// </summary>
    [Fact]
    public void APlanThatHasBeenCarriedOutClearsItsMarker()
    {
        var (mode, _) = Flying(new SlotPlan("Slot01_Size8", Module: "Cargo Rack"));

        Assert.False(Row(mode, "Slot01_Size8").Marked);
    }

    /// <summary>A slot with no plan at all was never marked and still is not.</summary>
    [Fact]
    public void ASlotWithNoPlanIsNotMarked()
    {
        var (mode, _) = Flying();

        Assert.False(Row(mode, "Slot01_Size8").Marked);
    }

    /// <summary>
    /// A plan that asks for a roll the module has not had is still outstanding, which is the case
    /// the marker exists for.
    /// </summary>
    [Fact]
    public void APlanAskingForARollTheModuleHasNotHadStaysMarked()
    {
        var (mode, _) = Flying(new SlotPlan("Slot01_Size8", "CargoRack_Capacity", 5, Module: "Cargo Rack"));

        Assert.True(Row(mode, "Slot01_Size8").Marked);
    }

    /// <summary>
    /// An empty plan — a shell with nothing wanted in it — marks nothing. Otherwise every slot a
    /// Commander touched and thought better of would carry a dot forever.
    /// </summary>
    [Fact]
    public void AnEmptyPlanMarksNothing()
    {
        var (mode, _) = Flying(new SlotPlan("Slot04_Size5"));

        Assert.False(Row(mode, "Slot04_Size5").Marked);
    }
}
