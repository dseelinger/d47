using D47.Core.Checklists;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Putting a build on the checklist leaves no plan line for a slot the plan no longer mentions (#2406).</summary>
public class AClearedSlotLeavesTheChecklistTests
{
    private sealed record Bench(
        ShipPlanService Ships, ChecklistService Checklists, ShipBuild Build)
    {
        public ChecklistScope Scope => Build.Scope!;

        public IReadOnlyList<ChecklistItem> Lines =>
            [.. Checklists.Document.Items.Where(item => item.Scope.Same(Scope))];

        public bool Mentions(string text) =>
            Lines.Any(item => item.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static Bench Planned()
    {
        var install = new MemoryInstall();

        var store = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance);

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

        var ships = new ShipPlanService(store, checklists, () => null);
        var build = ships.BuildFor(12, "python", "Bad Idea");

        ships.Plan(build.Id, new SlotPlan("MainEngines", Blueprint: "Dirty Drive Tuning", Grade: 5));
        ships.Plan(build.Id, new SlotPlan("PowerPlant", Blueprint: "Armoured", Grade: 3));

        return new Bench(ships, checklists, build);
    }

    [Fact]
    public void PromotingAgainAfterClearingASlotDropsItsLines()
    {
        var bench = Planned();
        bench.Ships.Promote(bench.Build.Id);

        Assert.True(bench.Mentions("Armoured"));

        bench.Checklists.AddTyped(bench.Scope, "Buy a scoop");
        bench.Ships.Clear(bench.Build.Id, "PowerPlant");
        bench.Ships.Promote(bench.Build.Id);

        Assert.False(bench.Mentions("Armoured"));
        Assert.True(bench.Mentions("Dirty Drive Tuning"));
        Assert.True(bench.Mentions("Buy a scoop"));
    }
}
