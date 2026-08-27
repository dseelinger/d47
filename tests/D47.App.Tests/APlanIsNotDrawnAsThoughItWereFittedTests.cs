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
    /// <para>
    /// Since the row became a table (docs/plans/change-requests.md 38) this is not a flag any
    /// more: the Current side is the journal's and the journal said nothing, so it is silent while
    /// the Plan side names the Fuel Scoop. The state the report was about is the one the two
    /// columns cannot help but show.
    /// </para>
    /// </summary>
    [Fact]
    public void APlannedModuleInAnEmptySlotSaysTheSlotIsEmpty()
    {
        var (mode, _) = Flying(new SlotPlan("Slot04_Size5", Module: "Fuel Scoop"));

        var row = Row(mode, "Slot04_Size5");

        Assert.NotNull(row.Parts);
        Assert.True(row.Parts.Current.Silent);
        Assert.Equal("empty", row.Parts.Vacant);
        Assert.Equal("Fuel Scoop", row.Parts.Plan?.Module);
    }

    /// <summary>And a plan in a slot that really has something in it does not.</summary>
    [Fact]
    public void APlannedModuleInAFullSlotDoesNot()
    {
        var (mode, _) = Flying(new SlotPlan("Slot01_Size8", Module: "Cargo Rack"));

        var row = Row(mode, "Slot01_Size8");

        Assert.NotNull(row.Parts);
        Assert.False(row.Parts.Current.Silent);
        Assert.Equal("8E Cargo Rack", row.Parts.Current.Module);
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

    // ---- The blueprint, in the Commander's words (GitHub issue 39) ----------------------------

    /// <summary>
    /// A fitted-but-unplanned slot used to print the journal's symbol, so one page carried two
    /// spellings of one blueprint: <c>Heavy Duty Hull Reinforcement</c> on the planned slots and
    /// <c>HullReinforcement_HeavyDuty</c> on this one.
    /// </summary>
    [Fact]
    public void AFittedRollIsNamedTheWayTheCommanderNamesIt()
    {
        var (mode, _) = Rolled("PowerDistributor_PrioritySystems", 5, experimental: null);

        var row = Row(mode, "PowerDistributor");

        Assert.NotNull(row.Parts);
        Assert.Equal("System Focused", row.Parts.Current.Blueprint);
        Assert.DoesNotContain("_", row.Parts.Current.Blueprint!, StringComparison.Ordinal);
    }

    // ---- A roll that disagrees with the plan (GitHub issue 42) --------------------------------

    /// <summary>
    /// <b>The case a plan exists to catch.</b> The right module, engineered to the right grade,
    /// with the wrong blueprint — and d47 was silent, because the row printed the plan's own words
    /// back.
    /// </summary>
    [Fact]
    public void ARollThatDisagreesWithThePlanIsReported()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: null,
            new SlotPlan("PowerDistributor", "Weapon Focused", 5));

        var row = Row(mode, "PowerDistributor");

        Assert.NotNull(row.Parts);

        // One row, two columns, and neither borrows from the other: the hull rolled System
        // Focused and the plan asks for Weapon Focused, and both are said.
        Assert.Equal("System Focused", row.Parts.Current.Blueprint);
        Assert.Equal("Weapon Focused", row.Parts.Plan?.Blueprint);
        Assert.False(row.Parts.Met);
        Assert.True(row.Marked);
    }

    /// <summary>
    /// And it is said out loud on the slot drill, which is where it belongs: the row's roll text is
    /// never trimmed and has to fit at 620 pixels, and naming both blueprints measured 395 in a 368
    /// row. Both facts were already on the drill in separate blocks; what was missing was anything
    /// saying they disagree.
    /// </summary>
    [Fact]
    public void TheDisagreementIsSaidOutLoudOnTheSlotDrill()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: null,
            new SlotPlan("PowerDistributor", "Weapon Focused", 5));

        var ship = Assert.Single(mode.Items());
        var said = mode.Fitted(ship.Key, "PowerDistributor").Select(line => line.Text).ToArray();

        Assert.Contains(said, text => text.Contains("asks for Weapon Focused", StringComparison.Ordinal));
        Assert.Contains(said, text => text.Contains("rolled System Focused", StringComparison.Ordinal));
    }

    /// <summary>And a roll that agrees says nothing of the kind.</summary>
    [Fact]
    public void ARollThatAgreesRaisesNothingOnTheDrill()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: null,
            new SlotPlan("PowerDistributor", "System Focused", 5));

        var ship = Assert.Single(mode.Items());
        var said = mode.Fitted(ship.Key, "PowerDistributor").Select(line => line.Text).ToArray();

        Assert.DoesNotContain(said, text => text.Contains("asks for", StringComparison.Ordinal));
    }

    /// <summary>
    /// A roll that agrees says it once, not twice: the plan is met, so the second column collapses
    /// rather than repeating the words already in the first (docs/plans/change-requests.md 38).
    /// </summary>
    [Fact]
    public void ARollThatAgreesWithThePlanSaysItOnce()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: null,
            new SlotPlan("PowerDistributor", "System Focused", 5));

        var row = Row(mode, "PowerDistributor");

        Assert.NotNull(row.Parts);
        Assert.True(row.Parts.Met);
        Assert.False(row.Marked);
    }

    /// <summary>
    /// And an unplanned roll has nothing to disagree with, so it is not reported as a
    /// disagreement — which would put a mark on every slot a Commander engineered without
    /// planning it first.
    /// </summary>
    [Fact]
    public void AnUnplannedRollIsNotADisagreement()
    {
        var (mode, _) = Rolled("PowerDistributor_PrioritySystems", 5, experimental: null);

        var row = Row(mode, "PowerDistributor");

        Assert.NotNull(row.Parts);
        Assert.Null(row.Parts.Plan);
        Assert.False(row.Parts.Met);
        Assert.False(row.Marked);
    }

    // ---- The experimental, in the Commander's words (GitHub issue 86) ------------------------

    /// <summary>
    /// <b>The dot came back, by the third road.</b> Reported 2026-08-26 against a Kestrel whose
    /// four hull reinforcements were rolled Heavy Duty G5 with Deep Plating exactly as planned:
    /// <i>"Didn't this get taken care of? All my HRPs are correctly engineered."</i> They were.
    /// <para>
    /// The plan stores <c>Deep Plating</c>, the Commander's words; Elite writes
    /// <c>special_hullreinforcement_chunky</c>, which is the same effect. The blueprint comparison
    /// learned that join for issue 39 and the experimental comparison did not, so a slot finished
    /// exactly as planned kept a mark that reads as outstanding work.
    /// </para>
    /// </summary>
    [Fact]
    public void AnExperimentalRolledAsPlannedClearsTheMark()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: "special_hullreinforcement_chunky",
            new SlotPlan("PowerDistributor", "System Focused", 5, Experimental: "Deep Plating"));

        var row = Row(mode, "PowerDistributor");

        Assert.NotNull(row.Parts);
        Assert.True(row.Parts.Met);
        Assert.False(row.Marked, "the roll is exactly what the plan asked for, so nothing is outstanding");
    }

    /// <summary>
    /// And the other half, which is what makes the mark worth anything: an experimental the plan
    /// asks for and the roll has <em>not</em> got is still outstanding. One of the Kestrel's four
    /// hull reinforcements really was bare, and its dot is correct.
    /// </summary>
    [Fact]
    public void AnExperimentalThePlanAsksForAndTheRollHasNotGotIsStillMarked()
    {
        var (mode, _) = Rolled(
            "PowerDistributor_PrioritySystems",
            5,
            experimental: null,
            new SlotPlan("PowerDistributor", "System Focused", 5, Experimental: "Deep Plating"));

        Assert.True(Row(mode, "PowerDistributor").Marked);
    }

    /// <summary>
    /// One ship whose power distributor carries a real roll, plus whatever plan the test wants.
    /// </summary>
    private static (ShipsMode Mode, ShipPlanService Ships) Rolled(
        string blueprint,
        int grade,
        string? experimental,
        params SlotPlan[] plans)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-roll-vs-plan-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        var loadout =
            """{"timestamp":"2026-08-24T09:00:00Z","event":"Loadout","Ship":"type9","ShipID":53,"ShipName":"oxen","ShipIdent":"OX-1","HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{"Slot":"PowerDistributor","Item":"int_powerdistributor_size7_class5","On":true,"Priority":1,"Health":1.0,"Engineering":{"Engineer":"The Dweller","EngineerID":1,"BlueprintID":1,"BlueprintName":"BLUEPRINT","Level":99,"Quality":1.0,EXPERIMENTAL"Modifiers":[]}}]}"""
                .Replace("BLUEPRINT", blueprint, StringComparison.Ordinal)
                .Replace("99", grade.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)

                // Elite writes the symbol here, never the name, which is the whole of issue 86.
                .Replace(
                    "EXPERIMENTAL",
                    experimental is { Length: > 0 } effect ? $"\"ExperimentalEffect\":\"{effect}\"," : string.Empty,
                    StringComparison.Ordinal);

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     loadout,
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
}
