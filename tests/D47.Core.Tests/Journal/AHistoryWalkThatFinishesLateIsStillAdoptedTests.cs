using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The walk back through older journals runs after the window is up, so the priming tick meets every
/// Commander before it has an answer and the state is offered to them afterwards (#148).
/// </summary>
public class AHistoryWalkThatFinishesLateIsStillAdoptedTests
{
    private const string Fid = "F1234567";

    [Fact]
    public void NothingIsWalkedUntilRunIsCalled()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, CarrierStats, CarrierLocation("Meene"));

        var backfill = Backfill(install);

        Assert.Equal(HistoryState.Pending, backfill.State);
        Assert.True(backfill.Pending);
        Assert.Null(backfill.Fleets);
        Assert.Null(backfill.Carriers);
        Assert.Null(backfill.Loadouts);
        Assert.Null(backfill.Names);
    }

    /// <summary>The priming tick takes what the current journal takes, and nothing older.</summary>
    [Fact]
    public void ACommanderMetBeforeTheWalkFinishesGetsNoneOfIt()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, CarrierStats, CarrierLocation("Meene"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame));

        Assert.False(store.Active!.Carrier.IsKnown);
    }

    [Fact]
    public void TheCarrierArrivesOnceTheWalkIsAdopted()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, CarrierStats, CarrierLocation("Meene"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame));
        backfill.Run();
        store.RestoreLate();

        Assert.Equal(HistoryState.Done, backfill.State);
        Assert.False(backfill.Pending);
        Assert.Equal("Meene", store.Active!.Carrier.StarSystem);
    }

    /// <summary>The live journal is the newer word, and adoption must not undo it.</summary>
    [Fact]
    public void ACarrierFoldedFromTheLiveJournalIsNotReplaced()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, CarrierStats, CarrierLocation("Meene"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame));
        store.Apply(Event(CarrierStats));
        store.Apply(Event("""{"timestamp":"2026-09-08T09:00:00Z","event":"CarrierJump","StarSystem":"Colonia"}"""));

        backfill.Run();
        store.RestoreLate();

        Assert.Equal("Colonia", store.Active!.Carrier.StarSystem);
    }

    /// <summary>
    /// The loadouts are the one of the four that merges: the ship boarded this session is only in the live
    /// set, and every other ship the Commander owns is only in the recovered one.
    /// </summary>
    [Fact]
    public void AShipBoardedBeforeAdoptionKeepsItsPlaceBesideTheRecoveredOnes()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, Loadout(shipId: 7, "Python"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame));
        store.Apply(Event(Loadout(shipId: 9, "Anaconda")));

        backfill.Run();
        store.RestoreLate();

        var ships = store.Active!.Loadouts.Ships;

        Assert.Equal("Python", ships[7].Loadout.Type);
        Assert.Equal("Anaconda", ships[9].Loadout.Type);
    }

    /// <summary>Every place from both, rather than the recovered set standing on the live one.</summary>
    [Fact]
    public void NamesMetThisSessionSurviveAdoption()
    {
        using var install = new TempInstall();
        Write(
            install,
            "Journal.2026-09-05T100000.01.log",
            LoadGame,
            """{"timestamp":"2026-09-05T11:00:00Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra"}""");

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame));
        store.Apply(Event("""{"timestamp":"2026-09-08T11:00:00Z","event":"FSDJump","StarSystem":"Deciat"}"""));

        backfill.Run();
        store.RestoreLate();

        Assert.True(store.Active!.Names.Knows("Deciat"));
        Assert.True(store.Active!.Names.Knows("Shinrarta Dezhra"));
    }

    /// <summary>The answer is wanted once, however many times the window asks for it.</summary>
    [Fact]
    public void ASecondRunWalksNothing()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame, CarrierStats, CarrierLocation("Meene"));

        var backfill = Backfill(install);
        backfill.Run();

        var walked = backfill.Carriers;
        backfill.Run();

        Assert.Same(walked, backfill.Carriers);
    }

    /// <summary>A folder Elite has never written to is an empty answer rather than a failure.</summary>
    [Fact]
    public void AJournalFolderThatDoesNotExistStillFinishes()
    {
        var backfill = new HistoryBackfill
        {
            Directory = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N")),
            Loggers = NullLoggerFactory.Instance,
        };

        backfill.Run();

        Assert.Equal(HistoryState.Done, backfill.State);
        Assert.Null(backfill.Failure);
        Assert.Empty(backfill.Carriers!);
    }

    private static HistoryBackfill Backfill(TempInstall install) => new()
    {
        Directory = install.Root,
        Loggers = NullLoggerFactory.Instance,
    };

    private static GameStateStore StoreOver(HistoryBackfill backfill) => new()
    {
        RestoreFleet = fid => backfill.Fleets?.GetValueOrDefault(fid),
        RestoreLoadouts = fid => backfill.Loadouts?.GetValueOrDefault(fid),
        RestoreCarrier = fid => backfill.Carriers?.GetValueOrDefault(fid),
        RestoreNames = fid => backfill.Names?.GetValueOrDefault(fid),
    };

    private const string LoadGame =
        """{"timestamp":"2026-09-05T10:00:00Z","event":"LoadGame","FID":"F1234567","Commander":"Fixture"}""";

    private const string CarrierStats =
        """{"timestamp":"2026-09-05T16:00:00Z","event":"CarrierStats","CarrierID":3715429376,"Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","FuelLevel":900,"Finance":{"CarrierBalance":12345},"SpaceUsage":{"TotalCapacity":25000,"Cargo":10,"FreeSpace":24990}}""";

    private static string CarrierLocation(string system) =>
        $$"""{"timestamp":"2026-09-05T16:36:00Z","event":"CarrierLocation","CarrierID":3715429376,"CarrierType":"FleetCarrier","StarSystem":"{{system}}"}""";

    private static string Loadout(int shipId, string type) =>
        $$"""{"timestamp":"2026-09-05T12:00:00Z","event":"Loadout","ShipID":{{shipId}},"Ship":"{{type}}","Modules":[]}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Write(TempInstall install, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(install.Root, name), lines);
}
