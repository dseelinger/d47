using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Where the carrier is, recovered from journals d47 was not running for.</summary>
public class CarrierBackfillTests
{
    private const string Fid = "F1234567";

    [Fact]
    public void NoJournalsAtAllLeavesTheCarrierUnknown()
    {
        using var install = new TempInstall();

        Assert.Empty(Carriers(install));
    }

    /// <summary>
    /// Journals with no carrier in them are a Commander who does not own one — nothing is invented, and
    /// in particular nothing is read off where their ships are parked.
    /// </summary>
    [Fact]
    public void JournalsWithNoCarrierEventsInventNothing()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-08-01T100000.01.log", LoadGame, Stored);

        Assert.Empty(Carriers(install));
    }

    /// <summary>The reported fault, in one test: the carrier moved in an older session.</summary>
    [Fact]
    public void ACarrierLocationInAnEarlierSessionIsFound()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, Stats, Location("Meene"));
        Write(install, "Journal.2026-09-07T100000.01.log", LoadGame, Stored);

        var carrier = Carriers(install)[Fid];

        Assert.Equal("Meene", carrier.StarSystem);
        Assert.Equal("BNH-T2F", carrier.CallSign);
        Assert.Equal(At("2026-09-05T16:36:00Z"), carrier.SeenAt);
    }

    /// <summary>A jump is the other way the carrier says where it is, and it dates itself too.</summary>
    [Fact]
    public void ACarrierJumpMovesTheRememberedSystem()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, Stats, Location("Meene"));
        Write(
            install,
            "Journal.2026-09-06T100000.01.log",
            LoadGame,
            """{ "timestamp":"2026-09-06T09:00:00Z", "event":"CarrierJump", "StarSystem":"Colonia" }""");

        var carrier = Carriers(install)[Fid];

        Assert.Equal("Colonia", carrier.StarSystem);
        Assert.Equal(At("2026-09-06T09:00:00Z"), carrier.SeenAt);
    }

    /// <summary>A squadron's carrier is written to the same journal, seconds later, and must never be adopted as the Commander's own — and reading a whole folder rather than one session is the circumstance that makes it easy to.</summary>
    [Fact]
    public void ASquadronCarrierIsNotAdoptedAsTheCommandersOwn()
    {
        using var install = new TempInstall();
        Write(
            install,
            "Journal.2026-09-05T100000.01.log",
            LoadGame,
            Stats,
            Location("Meene"),
            """{ "timestamp":"2026-09-05T16:37:00Z", "event":"CarrierLocation", "CarrierID":3713474048, "CarrierType":"SquadronCarrier", "StarSystem":"Somewhere Else" }""");

        var carrier = Carriers(install)[Fid];

        Assert.Equal("Meene", carrier.StarSystem);
    }

    /// <summary>
    /// Two Commanders share one journal folder and neither may be handed the other's carrier — the
    /// isolation rule the whole store is built on.
    /// </summary>
    [Fact]
    public void TwoCommandersDoNotShareACarrier()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, Stats, Location("Meene"));
        Write(
            install,
            "Journal.2026-09-06T100000.01.log",
            """{ "timestamp":"2026-09-06T10:00:00Z", "event":"LoadGame", "FID":"F7654321", "Commander":"Other" }""",
            """{ "timestamp":"2026-09-06T10:05:00Z", "event":"CarrierLocation", "CarrierID":99, "CarrierType":"FleetCarrier", "StarSystem":"Deciat" }""");

        var carriers = Carriers(install);

        Assert.Equal("Meene", carriers[Fid].StarSystem);
        Assert.Equal("Deciat", carriers["F7654321"].StarSystem);
    }

    /// <summary>Location and identity only.</summary>
    [Fact]
    public void FiguresFromTheStatsPanelAreNotRestored()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, Stats, Location("Meene"));

        var carrier = Carriers(install)[Fid];

        Assert.Null(carrier.FuelLevel);
        Assert.Null(carrier.Balance);
        Assert.Null(carrier.DockingAccess);
        Assert.Empty(carrier.Services);
    }

    /// <summary>
    /// The whole folder, unbounded — a carrier bought a year ago and left parked is still found.
    /// </summary>
    [Fact]
    public void ACarrierOlderThanTheFleetLookBackIsStillFound()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-01-01T100000.01.log", LoadGame, Stats, Location("Meene"));

        for (var day = 2; day <= 40; day++)
        {
            Write(install, $"Journal.2026-02-{day:00}T100000.01.log", LoadGame, Stored);
        }

        Assert.Equal("Meene", Carriers(install)[Fid].StarSystem);
    }

    private static IReadOnlyDictionary<string, CarrierState> Carriers(TempInstall install) =>
        CarrierBackfill.FromHistory(install.Root, NullLogger.Instance, TestContext.Current.CancellationToken);

    private const string LoadGame =
        """{ "timestamp":"2026-09-05T10:00:00Z", "event":"LoadGame", "FID":"F1234567", "Commander":"Fixture" }""";

    private const string Stats =
        """{ "timestamp":"2026-09-05T16:00:00Z", "event":"CarrierStats", "CarrierID":3715429376, "Callsign":"BNH-T2F", "Name":"Sacred Fire", "DockingAccess":"all", "FuelLevel":900, "Finance":{"CarrierBalance":12345}, "SpaceUsage":{"TotalCapacity":25000,"Cargo":10,"FreeSpace":24990} }""";

    private const string Stored =
        """{ "timestamp":"2026-09-06T10:10:00Z", "event":"StoredShips", "StarSystem":"Meene", "StationName":"BNH-T2F", "ShipsHere":[{"ShipID":1,"ShipType":"Sidewinder","Value":1}], "ShipsRemote":[] }""";

    private static string Location(string system) =>
        $$"""{ "timestamp":"2026-09-05T16:36:00Z", "event":"CarrierLocation", "CarrierID":3715429376, "CarrierType":"FleetCarrier", "StarSystem":"{{system}}" }""";

    /// <summary>The instant a fixture line carries, read the way the parser reads it.</summary>
    private static DateTimeOffset At(string timestamp) =>
        DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);

    private static void Write(TempInstall install, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(install.Root, name), lines);
}
