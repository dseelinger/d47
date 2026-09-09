using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The fleet recovered from journals d47 was not running for.</summary>
public class FleetBackfillTests
{
    private const string Fid = "F1234567";

    [Fact]
    public void NoJournalsAtAllLeavesTheFleetUnknownRatherThanEmpty()
    {
        using var install = new TempInstall();

        var fleets = FleetBackfill.FromHistory(install.Root, NullLogger.Instance);

        Assert.Empty(fleets);
    }

    /// <summary>
    /// "Unknown" and "no ships" are different answers and the surface renders them differently, so a
    /// folder with journals but no shipyard visit must not report a fleet of nothing.
    /// </summary>
    [Fact]
    public void JournalsWithNoShipyardVisitLeaveTheFleetUnknown()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Loadout);

        var fleets = FleetBackfill.FromHistory(install.Root, NullLogger.Instance);

        Assert.Empty(fleets);
    }

    /// <summary>The reported fault, in one test: the snapshot is in an older file, so read it.</summary>
    [Fact]
    public void ASnapshotInAnOlderFileIsFound()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Stored(("Anaconda", 51), ("Python", 52)));
        Write(install, "Journal.2026-08-02T100000.01.log", LoadGame, Loadout);

        var fleet = FleetBackfill.FromHistory(install.Root, NullLogger.Instance)[Fid];

        Assert.True(fleet.IsKnown);
        Assert.Equal([51, 52], fleet.Ships.Select(ship => ship.ShipId).Order());
    }

    /// <summary>The reason the snapshot alone is not enough.</summary>
    [Fact]
    public void AShipSoldAfterTheSnapshotDoesNotComeBack()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Stored(("Anaconda", 51), ("Python", 52)));
        Write(
            install,
            "Journal.2026-08-02T100000.01.log",
            LoadGame,
            """{ "timestamp":"2026-08-02T10:05:00Z", "event":"ShipyardSell", "ShipType":"python", "SellShipID":52 }""");

        var fleet = FleetBackfill.FromHistory(install.Root, NullLogger.Instance)[Fid];

        Assert.Equal([51], fleet.Ships.Select(ship => ship.ShipId));
    }

    /// <summary>Swapping moves a ship out of storage and another into it.</summary>
    [Fact]
    public void SwappingTakesOneShipOutOfStorageAndPutsOneIn()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Docked, Stored(("Anaconda", 51)));
        Write(
            install,
            "Journal.2026-08-02T100000.01.log",
            LoadGame,
            Docked,
            """{ "timestamp":"2026-08-02T10:05:00Z", "event":"ShipyardSwap", "ShipType":"anaconda", "ShipID":51, "StoreOldShip":"PantherMkII", "StoreShipID":41 }""");

        var fleet = FleetBackfill.FromHistory(install.Root, NullLogger.Instance)[Fid];

        // The Anaconda is now being flown, so it leaves the registry; the Panther takes its place.
        Assert.Equal([41], fleet.Ships.Select(ship => ship.ShipId));
        Assert.Equal("Elsewhere Station", fleet.Ships[0].StationName);
    }

    [Fact]
    public void BuyingStoresTheShipSteppedOutOf()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Docked, Stored(("Anaconda", 51)));
        Write(
            install,
            "Journal.2026-08-02T100000.01.log",
            LoadGame,
            Docked,
            """{ "timestamp":"2026-08-02T10:05:00Z", "event":"ShipyardBuy", "ShipType":"typex", "StoreOldShip":"CobraMkV", "StoreShipID":37 }""",
            """{ "timestamp":"2026-08-02T10:05:01Z", "event":"ShipyardNew", "ShipType":"typex", "NewShipID":60 }""");

        var fleet = FleetBackfill.FromHistory(install.Root, NullLogger.Instance)[Fid];

        Assert.Equal([37, 51], fleet.Ships.Select(ship => ship.ShipId).Order());
        Assert.DoesNotContain(fleet.Ships, ship => ship.ShipId == 60);
    }

    /// <summary>
    /// Two Commanders share one journal folder and neither may be handed the other's ships — the
 /// isolation rule the whole store is built on.
    /// </summary>
    [Fact]
    public void TwoCommandersDoNotShareAFleet()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Stored(("Anaconda", 51)));
        Write(
            install,
            "Journal.2026-08-02T100000.01.log",
            """{ "timestamp":"2026-08-02T10:00:00Z", "event":"LoadGame", "FID":"F7654321", "Commander":"Other" }""",
            """{ "timestamp":"2026-08-02T10:10:00Z", "event":"StoredShips", "StarSystem":"Somewhere", "StationName":"Somewhere Station", "ShipsHere":[{"ShipID":1,"ShipType":"Sidewinder","Value":1}], "ShipsRemote":[] }""");

        var fleets = FleetBackfill.FromHistory(install.Root, NullLogger.Instance);

        Assert.Equal([51], fleets[Fid].Ships.Select(ship => ship.ShipId));
        Assert.Equal([1], fleets["F7654321"].Ships.Select(ship => ship.ShipId));
    }

    /// <summary>The documented bound, pinned so it is a decision rather than a surprise.</summary>
    [Fact]
    public void ASnapshotOlderThanTheSearchWindowIsNotRecovered()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T000000.01.log", LoadGame, Stored(("Anaconda", 51)));

        // Comfortably past the look-back, so the snapshot above falls outside the window.
        for (var day = 2; day <= 40; day++)
        {
            Write(install, $"Journal.2026-09-{day:00}T100000.01.log", LoadGame, Loadout);
        }

        var fleets = FleetBackfill.FromHistory(install.Root, NullLogger.Instance);

        Assert.Empty(fleets);
    }

    /// <summary>A delta with no snapshot under it is not a fleet of one — it is a guess.</summary>
    [Fact]
    public void ASellWithNoSnapshotUnderItInventsNothing()
    {
        var fleet = FleetRegistry.Empty.Apply(
            Event("""{ "timestamp":"2026-08-02T10:05:00Z", "event":"ShipyardSell", "ShipType":"python", "SellShipID":52 }"""));

        Assert.False(fleet.IsKnown);
        Assert.Empty(fleet.Ships);
    }

    private const string LoadGame =
        """{ "timestamp":"2026-08-01T10:00:00Z", "event":"LoadGame", "FID":"F1234567", "Commander":"Fixture" }""";

    private const string Loadout =
        """{ "timestamp":"2026-08-01T10:00:01Z", "event":"Loadout", "Ship":"anaconda", "ShipID":51, "ShipName":"Flamebrand" }""";

    private const string Docked =
        """{ "timestamp":"2026-08-01T10:00:02Z", "event":"Docked", "StarSystem":"Elsewhere", "StationName":"Elsewhere Station" }""";

    private static string Stored(params (string Type, int Id)[] ships) =>
        $$"""{ "timestamp":"2026-08-01T10:10:00Z", "event":"StoredShips", "StarSystem":"Somewhere", "StationName":"Somewhere Station", "ShipsHere":[{{
            string.Join(",", ships.Select(s => $$"""{"ShipID":{{s.Id}},"ShipType":"{{s.Type}}","Value":1}"""))
        }}], "ShipsRemote":[] }""";

    private static JournalEvent Event(string line) =>
        JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException($"Unparseable fixture line: {line}");

    private static void Write(TempInstall install, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(install.Root, name), lines);
}
