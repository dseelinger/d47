using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A build whose ship the journal shows sold is a sold ship, and deleting it cleans up the checklist.</summary>
public class ASoldShipCanBeDeletedTests
{
    private const int Sold = 12;

    private const int Flying = 14;

    private static (ShipPlanService Ships, ChecklistService Checklists) Set(bool fleetKnown, bool planned)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        if (fleetKnown)
        {
            game.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"StoredShips","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial","ShipsHere":[],"ShipsRemote":[]}"""));
        }

        game.Apply(Event($$"""{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"anaconda","ShipID":{{Flying}},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}"""));

        var files = new MemoryFileSystem();
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), files, NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                files,
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(Sold, "python");

        if (planned)
        {
            ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
            ships.Promote(build.Id);
        }

        return (ships, checklists);
    }

    [Fact]
    public void ABuildMissingFromAKnownFleetIsSold()
    {
        var (ships, _) = Set(fleetKnown: true, planned: true);

        var entry = ships.Entry(ships.ForShip(Sold)!.Id)!;

        Assert.True(entry.Sold);
        Assert.Equal("sold", entry.Where());
    }

    [Fact]
    public void ABuildWithNoPlannedSlotsIsSoldToo()
    {
        var (ships, _) = Set(fleetKnown: true, planned: false);

        Assert.True(ships.Entry(ships.ForShip(Sold)!.Id)!.Sold);
    }

    [Fact]
    public void WithTheFleetUnknownNothingIsSold()
    {
        var (ships, _) = Set(fleetKnown: false, planned: true);

        var entry = ships.Entry(ships.ForShip(Sold)!.Id)!;

        Assert.False(entry.Sold);
        Assert.Equal("not bought yet", entry.Where());
    }

    [Fact]
    public void AnIntendedHullIsNotSold()
    {
        var (ships, _) = Set(fleetKnown: true, planned: true);

        var intended = ships.Intend("Python")!;

        Assert.False(ships.Entry(intended.Id)!.Sold);
        Assert.Equal("not bought yet", ships.Entry(intended.Id)!.Where());
    }

    [Fact]
    public void DeletingItRemovesTheBuildAndItsChecklistLines()
    {
        var (ships, checklists) = Set(fleetKnown: true, planned: true);
        var scope = ChecklistScope.Ship(Sold);

        Assert.Contains(checklists.Document.Items, item => item.Kind == ChecklistItemKind.Derived && item.Scope.Same(scope));

        ships.Delete(ships.ForShip(Sold)!.Id);

        Assert.Null(ships.ForShip(Sold));
        Assert.DoesNotContain(checklists.Document.Items, item => item.Kind == ChecklistItemKind.Derived && item.Scope.Same(scope));
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
