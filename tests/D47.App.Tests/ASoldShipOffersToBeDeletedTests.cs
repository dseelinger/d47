using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A build whose ship the journal shows sold offers Delete this ship, not Clear this plan or SLEF.</summary>
public class ASoldShipOffersToBeDeletedTests
{
    private static (ShipsMode Mode, ShipPlanService Ships) Set(bool fleetKnown, bool planned)
    {
        var game = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     fleetKnown
                         ? """{"timestamp":"2026-08-18T09:00:00Z","event":"StoredShips","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial","ShipsHere":[],"ShipsRemote":[]}"""
                         : """{"timestamp":"2026-08-18T09:00:00Z","event":"Music","MusicTrack":"NoTrack"}""",
                     """{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"anaconda","ShipID":14,"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            game.Apply(parsed!);
        }

        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-sold-ship-tests"));
        var checklists = TestSurface.Checklists(paths.Data);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => game.Active);
        var build = ships.BuildFor(12, "python");

        if (planned)
        {
            ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5));
        }

        var mode = new ShipsMode(ships, checklists, () => game.Active, setClipboardText: _ => Task.FromResult(true));

        return (mode, ships);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASoldShipIsDeletedNotCleared(bool planned)
    {
        var (mode, ships) = Set(fleetKnown: true, planned);
        var id = ships.ForShip(12)!.Id;

        Assert.Equal("Delete this ship", mode.DropLabel(id));
        Assert.Null(mode.ExportLabel(id));
        Assert.Contains(mode.Details(id), line => line.Text == "Sold.");

        mode.Drop(id);

        Assert.Null(ships.ForShip(12));
    }

    [Fact]
    public void WithTheFleetUnknownThePageIsAsItWas()
    {
        var (mode, ships) = Set(fleetKnown: false, planned: true);
        var id = ships.ForShip(12)!.Id;

        Assert.Equal("Clear this plan", mode.DropLabel(id));
        Assert.Equal("Copy as SLEF", mode.ExportLabel(id));
        Assert.DoesNotContain(mode.Details(id), line => line.Text == "Sold.");
    }
}
