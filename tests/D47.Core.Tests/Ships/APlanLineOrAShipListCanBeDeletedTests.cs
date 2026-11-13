using D47.Core.Checklists;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Deleting a plan's line or a ship's whole list, and what stays out of the checklist afterwards.</summary>
public class APlanLineOrAShipListCanBeDeletedTests
{
    private sealed record Bench(
        ShipBuildStore Store, ChecklistService Checklists, ShipPlanService Ships, string BuildId);

    private static Bench Promoted(MemoryInstall install)
    {
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

        ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        ships.Promote(build.Id);
        checklists.AddNote(ChecklistScope.Ship(12), "Buy limpets");

        return new Bench(store, checklists, ships, build.Id);
    }

    private static ChecklistItem PlanLine(Bench bench) =>
        bench.Checklists.Document.In(ChecklistScope.Ship(12)).First(item => item.Kind == ChecklistItemKind.Derived);

    [Fact]
    public void ALineAPlanPutThereIsRemoved()
    {
        var bench = Promoted(new MemoryInstall());
        var line = PlanLine(bench);

        var change = bench.Checklists.Delete(line.Id);

        Assert.True(change.Changed);
        Assert.DoesNotContain(bench.Checklists.Document.Items, item => item.Id.Same(line.Id));
    }

    [Fact]
    public void SayingRemoveAndYesRemovesAPlansLine()
    {
        var bench = Promoted(new MemoryInstall());
        var line = PlanLine(bench);

        bench.Checklists.ProposeChange(line.Text, ProposalKind.Remove);
        bench.Checklists.Accept();

        Assert.DoesNotContain(bench.Checklists.Document.Items, item => item.Id.Same(line.Id));
    }

    [Fact]
    public void ADeletedPlanLineStaysDeletedThroughPlanEditsUntilTheButtonIsPressedAgain()
    {
        var bench = Promoted(new MemoryInstall());
        var line = PlanLine(bench);

        bench.Checklists.Delete(line.Id);
        bench.Ships.Plan(bench.BuildId, new SlotPlan("PowerPlant", "Armoured", 3));

        Assert.DoesNotContain(bench.Checklists.Document.Items, item => item.Id.Same(line.Id));
        Assert.DoesNotContain(
            bench.Checklists.Document.In(ChecklistScope.Ship(12)),
            item => item.Text.Contains("Armoured", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(bench.Checklists.Proposals.Pending);

        bench.Ships.Promote(bench.BuildId);

        Assert.Contains(
            bench.Checklists.Document.In(ChecklistScope.Ship(12)),
            item => item.Text.Contains("Armoured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeletingAShipsListRemovesEveryLineAndLeavesThePlanAlone()
    {
        var bench = Promoted(new MemoryInstall());
        var list = bench.Checklists.Lists().Single(found => found.Scope.Same(ChecklistScope.Ship(12)));

        var change = bench.Checklists.DeleteList(list);

        Assert.True(change.Changed);
        Assert.Empty(bench.Checklists.Document.In(ChecklistScope.Ship(12)));
        Assert.Single(bench.Ships.ForShip(12)!.Slots);
        Assert.Empty(bench.Checklists.Proposals.Pending);
    }

    [Fact]
    public void DeletingAShipsListWithdrawsThePlanWaitingForIt()
    {
        var bench = Promoted(new MemoryInstall());
        var list = bench.Checklists.Lists().Single(found => found.Scope.Same(ChecklistScope.Ship(12)));

        bench.Checklists.ProposePlan(
            ChecklistScope.Ship(12),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(12),
                "python",
                [new BuildRequest("PowerPlant", "Armoured", 3)],
                bench.Checklists.SlotFor),
            ["PowerPlant"]);
        Assert.NotEmpty(bench.Checklists.Proposals.Pending);

        bench.Checklists.DeleteList(list);

        Assert.Empty(bench.Checklists.Proposals.Pending);
    }
}
