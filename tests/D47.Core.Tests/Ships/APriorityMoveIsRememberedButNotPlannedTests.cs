using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A priority move is kept with the build and counted by the power gauge, and is never a slot plan
/// (#468).
/// </summary>
public class APriorityMoveIsRememberedButNotPlannedTests
{
    private const string Cobra =
        """
        {"timestamp":"2026-09-17T00:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":10,"UnladenMass":190.0,"MaxJumpRange":20.0,"CargoCapacity":0,"Modules":[
        {"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":0,"Health":1.0},
        {"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0},
        {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":2,"Health":1.0}]}
        """;

    private static ShipLoadout Flown(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));

        return ShipLoadout.Unknown.Apply(journalEvent!);
    }

    private static ShipBuildStore Store(TempInstall install) =>
        new(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

    private static ShipPlanService Service(TempInstall install, ShipBuildStore store) =>
        new(
            store,
            new ChecklistService(
                new ChecklistStore(
                    Path.Combine(install.Root, "checklist.json"),
                    NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => null),
            () => null);

    [Fact]
    public void AMoveSurvivesAReloadAndTheGaugeCountsTheSlotInItsMovedGroup()
    {
        using var install = new TempInstall();
        var ships = Service(install, Store(install));
        var loadout = Flown(Cobra);

        var build = ships.BuildFor(10, "cobramkv");

        Assert.True(ships.Move(build.Id, "FrameShiftDrive", 5));

        var reloaded = Store(install);

        Assert.True(reloaded.Poll());

        var read = Assert.Single(reloaded.Builds);

        Assert.Equal(5, read.PriorityMoves["FrameShiftDrive"]);

        var power = ShipGauges.Read(read, loadout).Power;

        Assert.NotNull(power);
        Assert.NotNull(power.Groups);

        var fsd = EliteSpecifications.Module("int_hyperdrive_overcharge_size4_class5")!.Power!.Value;

        Assert.Equal(fsd, power.Groups![5], 3);
        Assert.False(power.Groups.ContainsKey(3));
        Assert.Equal(5, power.Modules.Single(module => module.Slot == "FrameShiftDrive").Priority);
    }

    [Fact]
    public void ABuildWithMovesAndNoPlansStillReadsAsMeasured()
    {
        var loadout = Flown(Cobra);
        var build = new ShipBuild("F1", "ship-1", "cobramkv", 10).Move("MainEngines", 4);

        var power = ShipGauges.Read(build, loadout).Power;

        Assert.NotNull(power);
        Assert.Equal(FigureKind.Measured, power.Kind);
    }

    [Fact]
    public void AMoveIsNotASlotPlan()
    {
        using var install = new TempInstall();
        var ships = Service(install, Store(install));

        var build = ships.BuildFor(10, "cobramkv");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        var before = ships.Store.Find(build.Id)!.Slots;

        Assert.True(ships.Move(build.Id, "FrameShiftDrive", 2));

        Assert.Equal(before, ships.Store.Find(build.Id)!.Slots);
    }

    [Fact]
    public void ClearingMovesTakesEveryMoveOnThatBuildAndNothingElse()
    {
        using var install = new TempInstall();
        var ships = Service(install, Store(install));

        var cobra = ships.BuildFor(10, "cobramkv");
        var python = ships.BuildFor(11, "python");

        ships.Plan(cobra.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        ships.Move(cobra.Id, "MainEngines", 2);
        ships.Move(cobra.Id, "FrameShiftDrive", 3);
        ships.Move(python.Id, "MainEngines", 4);

        Assert.True(ships.ClearMoves(cobra.Id));

        var cleared = ships.Store.Find(cobra.Id)!;

        Assert.Empty(cleared.PriorityMoves);
        Assert.Single(cleared.Slots);
        Assert.Equal(4, ships.Store.Find(python.Id)!.PriorityMoves["MainEngines"]);
    }

    [Fact]
    public void AFileWrittenBeforeMovesExistedLoadsWithNoneAndNoProblem()
    {
        using var install = new TempInstall();

        File.WriteAllText(
            Path.Combine(install.Root, "ships.json"),
            """
            {"ships":[{"id":"ship-1","hull":"python","shipId":41,
             "slots":[{"slot":"MainEngines","blueprint":"Dirty Drives","grade":5}]}]}
            """);

        var store = Store(install);

        Assert.True(store.Poll());
        Assert.Empty(Assert.Single(store.Builds).PriorityMoves);
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void AMoveOutsideOneToFiveIsDroppedAndReported()
    {
        using var install = new TempInstall();

        File.WriteAllText(
            Path.Combine(install.Root, "ships.json"),
            """
            {"ships":[{"id":"ship-1","hull":"python","shipId":41,
             "moves":{"MainEngines":7,"FrameShiftDrive":2}}]}
            """);

        var store = Store(install);

        Assert.True(store.Poll());

        var moves = Assert.Single(store.Builds).PriorityMoves;

        Assert.Equal(2, Assert.Single(moves).Value);
        Assert.Single(store.Problems);
    }
}
