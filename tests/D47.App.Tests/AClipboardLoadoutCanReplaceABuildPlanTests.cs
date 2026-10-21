using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Import from clipboard press reads the clipboard, asks before replacing, and reports.</summary>
public class AClipboardLoadoutCanReplaceABuildPlanTests
{
    private const string Python = """
        [{"header":{},"data":{"Ship":"python","Modules":[
          {"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":2},
          {"Slot":"PowerPlant","Item":"int_powerplant_size6_class5","On":true,"Priority":0}]}}]
        """;

    private static (ShipsMode Mode, ShipPlanService Ships) Bench(string? clipboard)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-import-clipboard-tests"));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        return (new ShipsMode(ships, checklists, () => null, clipboardText: () => Task.FromResult(clipboard)), ships);
    }

    [Fact]
    public async Task ABuildWithNoPlanTakesTheImportWithoutAsking()
    {
        var (mode, ships) = Bench(Python);
        var build = ships.Intend("Python")!;

        Assert.Equal("Import from clipboard", mode.ImportLabel(build.Id));

        var offer = await mode.ReadImport(build.Id);

        Assert.Null(offer.Refusal);
        Assert.Null(offer.Question);

        mode.Import(build.Id, offer);

        Assert.Equal(2, ships.Store.Find(build.Id)!.Slots.Count);
    }

    [Fact]
    public async Task APlannedBuildAsksWithTheCountsAndKeepingItChangesNothing()
    {
        var (mode, ships) = Bench(Python);
        var build = ships.Intend("Python")!;

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Beam Laser"));

        var offer = await mode.ReadImport(build.Id);

        Assert.Contains("1 slot", offer.Question, StringComparison.Ordinal);
        Assert.Contains("2 slots", offer.Question, StringComparison.Ordinal);
        Assert.Equal(["Hardpoint1"], ships.Store.Find(build.Id)!.Slots.Select(slot => slot.Slot));

        mode.Import(build.Id, offer);

        Assert.Equal(["MainEngines", "PowerPlant"], ships.Store.Find(build.Id)!.Slots.Select(slot => slot.Slot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("just some words")]
    [InlineData("""[{"header":{},"data":{"Ship":"anaconda","Modules":[]}}]""")]
    public async Task ABadClipboardIsRefusedAndChangesNothing(string? clipboard)
    {
        var (mode, ships) = Bench(clipboard);
        var build = ships.Intend("Python")!;

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Beam Laser"));

        var offer = await mode.ReadImport(build.Id);

        Assert.False(string.IsNullOrEmpty(offer.Refusal));
        Assert.Equal(["Hardpoint1"], ships.Store.Find(build.Id)!.Slots.Select(slot => slot.Slot));
    }

    [Fact]
    public async Task AnOwnedShipWithNoBuildGetsOneBeforeTheImport()
    {
        var (mode, ships) = Bench(Python);
        var build = ships.BuildFor(12, "Python", "Reaper");

        var offer = await mode.ReadImport(build.Id);

        mode.Import(build.Id, offer);

        Assert.Equal(2, ships.ForShip(12)!.Slots.Count);
    }
}
