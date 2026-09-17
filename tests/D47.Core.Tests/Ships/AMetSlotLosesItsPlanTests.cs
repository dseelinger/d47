using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A slot whose plan the fitted module already carries out entirely has its plan deleted — the tick
/// disappears because the plan does, and there is nothing left for a revision to supersede (#255).
/// </summary>
public class AMetSlotLosesItsPlanTests
{
    private const int ShipId = 33;

    private sealed record Bench(GameStateStore Game, ShipPlanService Ships);

    private static Bench Set(TempInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Loadout());

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(ShipId, "python");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));

        return new Bench(game, ships);
    }

    [Fact]
    public void AMetSlotsPlanIsDeleted()
    {
        using var install = new TempInstall();
        var (game, ships) = Set(install);

        game.Apply(Engineered());

        ships.DropMetSlots();

        Assert.Null(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    [Fact]
    public void AnUnmetSlotsPlanIsKept()
    {
        using var install = new TempInstall();
        var (_, ships) = Set(install);

        ships.DropMetSlots();

        Assert.NotNull(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    [Fact]
    public void APartlyMetSlotKeepsItsPlan()
    {
        using var install = new TempInstall();
        var (game, ships) = Set(install);

        var build = ships.ForShip(ShipId)!;

        ships.Plan(
            build.Id,
            new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer", "Faster Boot Sequence"));

        // The blueprint is rolled, and the experimental is not.
        game.Apply(Engineered());

        ships.DropMetSlots();

        Assert.NotNull(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    private static JournalEvent Loadout() => Event(
        $$"""
        {"timestamp":"2026-09-10T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}
        """);

    private static JournalEvent Engineered() => Event(
        """
        {"timestamp":"2026-09-10T09:10:00Z","event":"Loadout","Ship":"python","ShipID":SHIPID,
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,
           "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
                          "Engineer":"Felicity Farseer","EngineerID":300100}}]}
        """.Replace("SHIPID", ShipId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
