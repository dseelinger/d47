using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
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

    private static Bench Set(MemoryInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(ShipPlanFixtures.Loadout(ShipId));

        var checklists = TestSurface.EmptyChecklists(install.Root, () => game.Active);

        var ships = TestSurface.EmptyShipPlans(install.Root, checklists, () => game.Active);

        var build = ships.BuildFor(ShipId, "python");

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));

        return new Bench(game, ships);
    }

    [Fact]
    public void AMetSlotsPlanIsDeleted()
    {
        var install = new MemoryInstall();
        var (game, ships) = Set(install);

        game.Apply(ShipPlanFixtures.Engineered(ShipId));

        ships.DropMetSlots();

        Assert.Null(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    [Fact]
    public void AnUnmetSlotsPlanIsKept()
    {
        var install = new MemoryInstall();
        var (_, ships) = Set(install);

        ships.DropMetSlots();

        Assert.NotNull(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    [Fact]
    public void APartlyMetSlotKeepsItsPlan()
    {
        var install = new MemoryInstall();
        var (game, ships) = Set(install);

        var build = ships.ForShip(ShipId)!;

        ships.Plan(
            build.Id,
            new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Faster Boot Sequence"));

        // The blueprint is rolled, and the experimental is not.
        game.Apply(ShipPlanFixtures.Engineered(ShipId));

        ships.DropMetSlots();

        Assert.NotNull(ships.ForShip(ShipId)!.For("MainEngines"));
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
