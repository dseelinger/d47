using D47.Core.Checklists;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A slot that names a module writes a module line, with or without engineering (#1829).</summary>
public class APlannedModuleReachesTheChecklistTests
{
    private const string FiveA = "int_engine_size5_class5";

    private static IReadOnlyList<ChecklistItem> Items(params BuildRequest[] requests) =>
        EngineeringPlan.Items(ChecklistScope.Ship(12), "krait_mkii", requests);

    private static (ShipPlanService Ships, ChecklistService Checklists, ShipBuild Build) Owned()
    {
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance);
        var ships = new ShipPlanService(store, checklists, () => null);

        return (ships, checklists, ships.BuildFor(12, "krait_mkii", "Test build"));
    }

    [Fact]
    public void AModuleWithNoEngineeringWritesTheModuleLineOnly()
    {
        var item = Assert.Single(Items(new BuildRequest("MainEngines", Module: "Thrusters", Variant: FiveA)));

        Assert.Equal(ChecklistIntentKind.Module, item.Intent!.Kind);
        Assert.Equal("Thrusters", item.Intent.Module);
        Assert.Equal(FiveA, item.Intent.Variant);
        Assert.Null(item.Intent.Grade);
        Assert.Equal("Buy 5A Thrusters for MainEngines", item.Text);
    }

    [Fact]
    public void AVariantAloneWritesTheModuleLine()
    {
        var item = Assert.Single(Items(new BuildRequest("MainEngines", Variant: FiveA)));

        Assert.Equal(ChecklistIntentKind.Module, item.Intent!.Kind);
    }

    [Fact]
    public void AModuleAndABlueprintWriteBothLinesUnderDifferentKeys()
    {
        var items = Items(new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, Module: "Thrusters", Variant: FiveA));

        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.Intent!.Kind == ChecklistIntentKind.Module);
        Assert.Contains(items, item => item.Intent!.Kind == ChecklistIntentKind.Blueprint && item.Intent.Grade == 5);
        Assert.Equal(2, items.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void PromotingModuleOnlySlotsPutsThemOnTheListAndSaysHowMany()
    {
        var (ships, checklists, build) = Owned();

        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters") { Variant = FiveA });
        ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Pulse Laser"));

        var said = ships.Promote(build.Id);
        checklists.Accept();

        Assert.Contains("2 items are on your checklist", said, StringComparison.Ordinal);
        Assert.Equal(2, checklists.Document.Items.Count(item => item.Intent?.Kind == ChecklistIntentKind.Module));
    }

    [Fact]
    public void NothingIsPlannedIsSaidOnlyWhenNoSlotNamesAnything()
    {
        var (ships, _, build) = Owned();

        ships.Plan(build.Id, new SlotPlan("MainEngines"));

        Assert.Contains("Nothing is planned", ships.Promote(build.Id), StringComparison.Ordinal);
    }

    [Fact]
    public void ClearingASlotsModuleDropsItsLineOnTheNextPromote()
    {
        var (ships, checklists, build) = Owned();

        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters") { Variant = FiveA });
        ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Pulse Laser"));
        ships.Promote(build.Id);
        checklists.Accept();

        ships.Plan(build.Id, new SlotPlan("MainEngines"));
        ships.Promote(build.Id);
        checklists.Accept();

        var left = Assert.Single(checklists.Document.Items, item => item.Intent?.Kind == ChecklistIntentKind.Module);

        Assert.Equal("Hardpoint1", left.Intent!.Subject);
    }
}
