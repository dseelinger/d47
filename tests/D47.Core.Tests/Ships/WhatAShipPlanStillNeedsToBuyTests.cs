using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

public class WhatAShipPlanStillNeedsToBuyTests
{
    private const int ShipId = 21;
    private const string Scoop = "int_fuelscoop_size6_class5";
    private const string Drive = "int_hyperdrive_size5_class5";

    private sealed class Bench
    {
        public Bench(string? stored = null, bool fitted = true)
        {
            var install = new MemoryInstall();
            Game = new GameStateStore();

            Game.Apply(Event("""{"timestamp":"2026-09-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

            if (fitted)
            {
                Game.Apply(Event(
                    $$"""
                    {"timestamp":"2026-09-10T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
                     "Modules":[{"Slot":"Slot01_Size6","Item":"{{Scoop}}","On":true},
                                {"Slot":"Slot02_Size5","Item":"int_cargorack_size5_class1","On":true}]}
                    """));
            }

            if (stored is not null)
            {
                Game.Apply(Event(
                    $$"""
                    {"timestamp":"2026-09-10T09:06:00Z","event":"StoredModules","StationName":"Fixture Outpost",
                     "StarSystem":"Fixture Point","Items":[{{stored}}]}
                    """));
            }

            var checklists = new ChecklistService(
                new ChecklistStore(Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => Game.Active);

            Ships = new ShipPlanService(
                new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
                checklists,
                () => Game.Active);
        }

        public GameStateStore Game { get; }

        public ShipPlanService Ships { get; }

        public ShipBuild Owned(params SlotPlan[] plans)
        {
            var build = Ships.BuildFor(ShipId, "python");

            foreach (var plan in plans)
            {
                Ships.Plan(build.Id, plan);
            }

            return Ships.ForShip(ShipId)!;
        }

        public ShipBuild Intended(params SlotPlan[] plans)
        {
            var build = Ships.Intend("python")!;

            foreach (var plan in plans)
            {
                Ships.Plan(build.Id, plan);
            }

            return Ships.Store.Find(build.Id)!;
        }
    }

    private static string StoredItem(string symbol, string localised) =>
        $$"""{"Name":"${{symbol}}_name;","Name_Localised":"{{localised}}","StarSystem":"Fixture Point","TransferCost":100}""";

    [Fact]
    public void ASlotWithNoModuleInThePlanContributesNothing()
    {
        var bench = new Bench(stored: StoredItem("x_size1_class1", "Other"));
        var build = bench.Owned(new SlotPlan("Slot01_Size6", "Dirty Drive Tuning", 5));

        Assert.Empty(bench.Ships.ToBuy(build).Items);
    }

    [Fact]
    public void AFittedModuleThatMatchesThePlanContributesNothing()
    {
        var bench = new Bench(stored: StoredItem("other_size1_class1", "Other"));
        var build = bench.Owned(new SlotPlan("Slot01_Size6", Module: "Fuel Scoop") { Variant = Scoop });

        Assert.Empty(bench.Ships.ToBuy(build).Items);
    }

    [Fact]
    public void AModuleInStorageContributesNothingWhenThePlanNamesTheExactModule()
    {
        var bench = new Bench(stored: StoredItem(Drive, "Frame Shift Drive"));
        var build = bench.Owned(new SlotPlan("Slot03_Size5", Module: "Frame Shift Drive") { Variant = Drive });

        var result = bench.Ships.ToBuy(build);

        Assert.Empty(result.Items);
        Assert.True(result.StorageChecked);
    }

    [Fact]
    public void AStoredModuleOfAnotherSizeDoesNotCoverAnExactModule()
    {
        var bench = new Bench(stored: StoredItem("int_hyperdrive_size4_class5", "Frame Shift Drive"));
        var build = bench.Owned(new SlotPlan("Slot03_Size5", Module: "Frame Shift Drive") { Variant = Drive });

        var purchase = Assert.Single(bench.Ships.ToBuy(build).Items);

        Assert.Equal("5A Frame Shift Drive", purchase.Item.Display);
        Assert.Equal(Drive, purchase.Symbol);
        Assert.Equal("Slot03_Size5", purchase.Slot);
    }

    [Fact]
    public void AModuleInStorageInAnySizeCoversAPlanThatNamesOnlyTheModule()
    {
        var bench = new Bench(stored: StoredItem("int_fuelscoop_size3_class2", "Fuel Scoop"));
        var build = bench.Owned(new SlotPlan("Slot04_Size4", Module: "Fuel Scoop"));

        Assert.Empty(bench.Ships.ToBuy(build).Items);
    }

    [Fact]
    public void TwoSlotsPlanningTheSameModuleWithOneInStorageListOneToBuy()
    {
        var bench = new Bench(stored: StoredItem(Drive, "Frame Shift Drive"));
        var build = bench.Owned(
            new SlotPlan("Slot03_Size5", Module: "Frame Shift Drive") { Variant = Drive },
            new SlotPlan("Slot04_Size5", Module: "Frame Shift Drive") { Variant = Drive });

        var purchase = Assert.Single(bench.Ships.ToBuy(build).Items);

        Assert.Equal("5A Frame Shift Drive", purchase.Item.Display);
    }

    [Fact]
    public void AModuleNeitherFittedNorStoredIsListedWithItsSearchForm()
    {
        var bench = new Bench(stored: StoredItem("other_size1_class1", "Other"));
        var build = bench.Owned(new SlotPlan("Slot02_Size5", Module: "Fuel Scoop") { Variant = Scoop });

        var purchase = Assert.Single(bench.Ships.ToBuy(build).Items);

        Assert.Equal("6A Fuel Scoop", purchase.Item.Display);
        Assert.Equal(Scoop, purchase.Symbol);
    }

    [Fact]
    public void ABuildForAHullNotYetOwnedListsTheHull()
    {
        var bench = new Bench(fitted: false);
        var build = bench.Intended();

        var purchase = Assert.Single(bench.Ships.ToBuy(build).Items);

        Assert.True(purchase.Item.IsShip);
        Assert.Equal("Python", purchase.Item.Display);
        Assert.Null(purchase.Slot);
    }

    [Fact]
    public void WithNoStoredModulesSnapshotStorageIsSaidNotToBeChecked()
    {
        var bench = new Bench();
        var build = bench.Owned(new SlotPlan("Slot02_Size5", Module: "Fuel Scoop") { Variant = Scoop });

        var result = bench.Ships.ToBuy(build);

        Assert.False(result.StorageChecked);
        Assert.Single(result.Items);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
