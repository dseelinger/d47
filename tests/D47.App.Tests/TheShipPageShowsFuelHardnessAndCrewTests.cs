using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheShipPageShowsFuelHardnessAndCrewTests
{
    private const string Loadout =
        """{"timestamp":"2026-08-26T00:55:33Z","event":"Loadout","Ship":"adder","ShipID":7,"ShipName":"a","ShipIdent":"AD-1","HullValue":1,"ModulesValue":1,"Rebuy":1,"FuelCapacity":{"Main":16.0,"Reserve":0.36},"Modules":[]}""";

    private static ShipsMode Mode(GameStatus? status, bool flown = true)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-fuel-tests"));


        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-26T00:55:33Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     Loadout,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        ships.BuildFor(7, "adder");

        return new ShipsMode(ships, checklists, () => live, status: status is null ? null : () => status);
    }

    private static GameStatus InShip(double fuel) => new()
    {
        ReadAt = DateTimeOffset.Now,
        Flags = StatusFlags.InMainShip,
        FuelMain = fuel,
    };

    private static IReadOnlyList<string> Tiles(ShipsMode mode)
    {
        var ship = Assert.Single(mode.Items());

        return [.. mode.Details(ship.Key).SelectMany(line => line.Stats).Select(stat => $"{stat.Label}={stat.Value}")];
    }

    [Fact]
    public void TheFlownShipShowsItsFuelLevelAgainstItsTank()
    {
        Assert.Contains("Fuel=12.5 of 16 t", Tiles(Mode(InShip(12.5))));
    }

    [Fact]
    public void AShipWithNoLiveStatusShowsItsTankAlone()
    {
        Assert.Contains("Fuel=16 t tank", Tiles(Mode(null)));
    }

    [Fact]
    public void EveryHullShowsItsHardnessAndCrewSeats()
    {
        var tiles = Tiles(Mode(null));

        Assert.Contains("Hardness=35", tiles);
        Assert.Contains("Crew seats=2", tiles);
    }

    [Fact]
    public void ADriveInTheChooserShowsItsOptimalMassAndFuelPerJump()
    {
        var figures = ShipsMode.Figures(EliteSpecifications.Module("int_hyperdrive_size5_class5")!);

        Assert.Contains("optimal mass 1050 t", figures);
        Assert.Contains("max fuel per jump 5 t", figures);
    }

    [Fact]
    public void ArmourInTheChooserShowsHullBoostAndAllFourResistances()
    {
        var figures = ShipsMode.Figures(EliteSpecifications.Module("adder_armour_grade3")!);

        Assert.Contains("hull +250%", figures);
        Assert.Contains("kinetic -20%", figures);
        Assert.Contains("thermal 0%", figures);
        Assert.Contains("explosive -40%", figures);
        Assert.Contains("caustic 0%", figures);
    }
}
