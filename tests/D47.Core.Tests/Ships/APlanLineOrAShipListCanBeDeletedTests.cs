using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Deleting a plan's line or a ship's whole list, and what boarding asks afterwards.</summary>
public class APlanLineOrAShipListCanBeDeletedTests
{
    private sealed record Bench(
        ShipBuildStore Store, ChecklistService Checklists, ShipPlanService Ships, ShipDriftWatch Drift, string BuildId);

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

        return new Bench(store, checklists, ships, new ShipDriftWatch(ships, checklists), build.Id);
    }

    private static JournalEvent Boarding(int ship)
    {
        Assert.True(JournalEvent.TryParse(
            $$"""
              {"timestamp":"2026-08-20T09:00:00Z","event":"Loadout","Ship":"python","ShipID":{{ship}},"MaxJumpRange":20.0,"UnladenMass":350.0,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}
              """,
            NullLogger.Instance,
            out var journalEvent));

        return journalEvent!;
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
    public void BoardingAfterDeletingAPlanLineDoesNotOfferItBack()
    {
        var bench = Promoted(new MemoryInstall());

        bench.Checklists.Delete(PlanLine(bench).Id);

        Assert.Null(bench.Drift.Observe([Boarding(12)]));
        Assert.Empty(bench.Checklists.Proposals.Pending);
    }

    [Fact]
    public void ChangingThePlanAfterADeleteIsOfferedAgain()
    {
        var bench = Promoted(new MemoryInstall());

        bench.Checklists.Delete(PlanLine(bench).Id);
        bench.Ships.Plan(bench.BuildId, new SlotPlan("PowerPlant", "Armoured", 3));

        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));
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
        Assert.Null(bench.Drift.Observe([Boarding(12)]));
        Assert.Empty(bench.Checklists.Proposals.Pending);
    }

    [Fact]
    public void DeletingAShipsListWithdrawsThePlanWaitingForIt()
    {
        var bench = Promoted(new MemoryInstall());
        var list = bench.Checklists.Lists().Single(found => found.Scope.Same(ChecklistScope.Ship(12)));

        bench.Checklists.Delete(PlanLine(bench).Id);
        bench.Ships.Plan(bench.BuildId, new SlotPlan("PowerPlant", "Armoured", 3));
        Assert.NotNull(bench.Drift.Observe([Boarding(12)]));
        Assert.NotEmpty(bench.Checklists.Proposals.Pending);

        bench.Checklists.DeleteList(list);

        Assert.Empty(bench.Checklists.Proposals.Pending);
    }
}
