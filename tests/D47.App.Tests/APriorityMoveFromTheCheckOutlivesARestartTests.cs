using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Ships mode behind the D47 check's MOVE TO Pn and UNDO MOVES: a move is written to ships.json, read
/// back after a restart, and undone.
/// </summary>
public class APriorityMoveFromTheCheckOutlivesARestartTests
{
    private const string Item = "new:12";

    private const string Hardpoint = "MediumHardpoint1";

    private static ShipsMode Mode(string root, CommanderGameState state)
    {
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);

        // What the first tick after startup does.
        store.Poll();

        return new ShipsMode(new ShipPlanService(store, checklists, () => state), checklists, () => state);
    }

    /// <summary>A Cobra Mk V the Commander is sitting in, with its priority groups read.</summary>
    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        string[] lines =
        [
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":12,"ShipName":"Predestinatio","MaxJumpRange":30.0,"CargoCapacity":32,"UnladenMass":190.0,"FuelCapacity":{"Main":16.0,"Reserve":0.49},"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size4_class5","On":true,"Priority":1,"Health":1.0},{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"MediumHardpoint1","Item":"hpt_multicannon_fixed_medium","On":true,"Priority":1,"Health":1.0},{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":1,"Health":1.0}]}""",
        ];

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static int? PriorityOf(LoadoutPower? power, string slot) =>
        power?.Gauge?.Modules.Single(module => module.Slot == slot).Priority;

    [Fact]
    public void AMoveIsStillThereAfterARestartAndUndoMovesForgetsIt()
    {
        var root = TempFolders.Create("d47-priority-move-tests");
        var state = Flying();
        var mode = Mode(root, state);

        var before = PriorityOf(mode.Power(Item), Hardpoint);

        Assert.NotEqual(5, before);
        Assert.False(mode.Power(Item)!.HasMoves);

        Assert.True(mode.MovePriority(Item, Hardpoint, 5));

        var restarted = Mode(root, state);

        Assert.Equal(5, PriorityOf(restarted.Power(Item), Hardpoint));
        Assert.True(restarted.Power(Item)!.HasMoves);

        Assert.True(restarted.ClearPriorityMoves(Item));

        Assert.Equal(before, PriorityOf(restarted.Power(Item), Hardpoint));
        Assert.False(restarted.Power(Item)!.HasMoves);
        Assert.False(restarted.ClearPriorityMoves(Item));
    }
}
