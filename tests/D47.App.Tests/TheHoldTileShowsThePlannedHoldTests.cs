using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Ships;
using Xunit;

namespace D47.App.Tests;

public class TheHoldTileShowsThePlannedHoldTests
{
    private static LoadoutStat Hold(params SlotPlan[] plans)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-planned-hold-tests"));

        var checklists = TestSurface.Checklists(paths.Data);

        var live = JournalScript.State(
            """{"timestamp":"2026-08-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-24T09:00:00Z","event":"Loadout","Ship":"cobramkv","ShipID":53,"ShipName":"oxen","ShipIdent":"OX-1","CargoCapacity":12,"HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{"Slot":"Slot01_Size3","Item":"int_cargorack_size3_class1","On":true,"Priority":1,"Health":1.0},{"Slot":"Slot02_Size2","Item":"int_cargorack_size2_class1","On":true,"Priority":1,"Health":1.0}]}""");

        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => live);

        var build = ships.BuildFor(53, live.Ship!.Type!);

        foreach (var plan in plans)
        {
            ships.Plan(build.Id, plan);
        }

        var mode = new ShipsMode(ships, checklists, () => live);

        var ship = Assert.Single(mode.Items());

        return mode.Details(ship.Key).SelectMany(line => line.Stats ?? []).Single(stat => stat.Label == "Hold");
    }

    [Fact]
    public void ARackPlannedIntoAnEmptySlotShowsFittedThenPlanned()
    {
        Assert.Equal("12 ► ~28 t", Hold(new SlotPlan("Slot03_Size4") { Variant = "int_cargorack_size4_class1" }).Value);
    }

    [Fact]
    public void APlanThatLeavesTheHoldAloneShowsTheFittedHold()
    {
        Assert.Equal("12 t", Hold(new SlotPlan("Slot03_Size4") { Variant = "int_fuelscoop_size4_class5" }).Value);
    }
}
