using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The live missions and their detail, folded from the journal and recovered from older files after a
/// restart. Replayed from <c>tests/fixtures/missions</c>: three sessions, the newest holding only a
/// <c>Missions</c> snapshot.
/// </summary>
public sealed class TheMissionBoardSurvivesARestartTests
{
    private const string Fid = "F100";

    private const long Courier = 1066795391;
    private const long Massacre = 1066820845;
    private const long Delivery = 1041612601;
    private const long NoAccept = 1030000000;
    private const long Completed = 1070000001;
    private const long Abandoned = 1070000002;

    [Fact]
    public void AMissionAcceptedInAnOlderSessionIsNamedInFullAfterARestart()
    {
        var state = Restarted();

        var courier = state.Missions.For(Courier);

        Assert.NotNull(courier);
        Assert.True(courier.HasDetail);
        Assert.Equal("Courier Job Available", courier.Title);
        Assert.Equal("Merchiston Dock, Kweleutahe", courier.Destination);
        Assert.Equal("League of HR 6649 Front", courier.Faction);
        Assert.Equal(79014, courier.Reward);

        // The snapshot's relative expiry, not the accept's absolute one.
        Assert.Equal(At("2026-09-24T11:00:30Z"), courier.Expiry);
    }

    [Fact]
    public void TheCurrentSessionAloneKnowsOnlyTheNames()
    {
        var store = new GameStateStore();
        Replay(store, Newest);

        var courier = store.Active!.Missions.For(Courier);

        Assert.NotNull(courier);
        Assert.False(courier.HasDetail);
        Assert.Null(courier.Destination);
    }

    [Fact]
    public void ARedirectReplacesTheDestination()
    {
        var massacre = Restarted().Missions.For(Massacre);

        Assert.NotNull(massacre);
        Assert.True(massacre.Redirected);
        Assert.Equal("Morelos Point, Mot", massacre.Destination);
    }

    [Fact]
    public void ACargoDepotRecordsDeliveryProgressAgainstTheMission()
    {
        var delivery = Restarted().Missions.For(Delivery);

        Assert.NotNull(delivery);
        Assert.Equal(new MissionCargo(99, 40, 99), delivery.Cargo);
        Assert.Equal("polymers", delivery.Commodity);
        Assert.Equal("Polymers", delivery.CommodityLocalised);
        Assert.Equal(99, delivery.Count);
    }

    [Fact]
    public void CompletedAndAbandonedMissionsAreOffTheBoard()
    {
        var board = Restarted().Missions;

        Assert.Null(board.For(Completed));
        Assert.Null(board.For(Abandoned));
        Assert.Equal(4, board.Missions.Count);
    }

    [Theory]
    [InlineData("MissionCompleted")]
    [InlineData("MissionFailed")]
    [InlineData("MissionAbandoned")]
    public void EndingAMissionRemovesIt(string ending)
    {
        var board = Restarted().Missions.Apply(Event(
            $$"""{ "timestamp":"2026-09-24T10:05:00Z", "event":"{{ending}}", "Name":"Mission_Courier", "MissionID":{{Courier}} }"""));

        Assert.Null(board.For(Courier));
        Assert.Equal(3, board.Missions.Count);
    }

    [Fact]
    public void AMissionRedirectedLiveKeepsItsNewDestination()
    {
        var board = Restarted().Missions.Apply(Event(
            $$"""{ "timestamp":"2026-09-24T10:05:00Z", "event":"MissionRedirected", "MissionID":{{Courier}}, "Name":"Mission_Courier", "NewDestinationStation":"Ray Gateway", "NewDestinationSystem":"Diaguandri", "OldDestinationStation":"Merchiston Dock", "OldDestinationSystem":"Kweleutahe" }"""));

        Assert.Equal("Ray Gateway, Diaguandri", board.For(Courier)!.Destination);
    }

    [Fact]
    public void AMissionWithNoAcceptAnywhereIsOnTheBoardByNameAndClaimsNothing()
    {
        var bare = Restarted().Missions.For(NoAccept);

        Assert.NotNull(bare);
        Assert.False(bare.HasDetail);
        Assert.Equal("Mission_Collect", bare.Title);
        Assert.Null(bare.Destination);
        Assert.Null(bare.Reward);
        Assert.Null(bare.Faction);
    }

    [Fact]
    public void TheSnapshotDropsWhatItDoesNotList()
    {
        var board = Restarted().Missions.Apply(Event(
            $$"""{ "timestamp":"2026-09-25T10:00:30Z", "event":"Missions", "Active":[ { "MissionID":{{Delivery}}, "Name":"Mission_Delivery_Boom", "PassengerMission":false, "Expires":60 } ], "Failed":[], "Complete":[] }"""));

        Assert.Equal([Delivery], board.Missions.Select(mission => mission.Id));
        Assert.True(board.For(Delivery)!.HasDetail);
        Assert.Equal(At("2026-09-25T10:01:30Z"), board.For(Delivery)!.Expiry);
    }

    [Fact]
    public void TheSearchStopsOnceEveryLiveMissionIsFound()
    {
        using var install = new TempInstall();

        var oldest = Write(install, "Journal.2026-09-10T100000.01.log", LoadGame("2026-09-10"), Accept(9001, "2026-09-10"));
        Write(install, "Journal.2026-09-12T100000.01.log", LoadGame("2026-09-12"), Accept(Courier, "2026-09-12"));
        Write(install, "Journal.2026-09-14T100000.01.log", LoadGame("2026-09-14"));
        Write(install, "Journal.2026-09-16T100000.01.log", LoadGame("2026-09-16"), Snapshot(Courier, "2026-09-16"));

        var opened = new List<string>();

        var boards = MissionBackfill.FromHistory(
            [.. Directory.EnumerateFiles(install.Root, "Journal.*.log").Order(StringComparer.Ordinal)],
            NullLogger.Instance,
            path =>
            {
                opened.Add(Path.GetFileName(path));
                return File.ReadAllText(path);
            },
            TestContext.Current.CancellationToken);

        Assert.True(boards[Fid].For(Courier)!.HasDetail);
        Assert.Equal(3, opened.Count);
        Assert.DoesNotContain(Path.GetFileName(oldest), opened);
    }

    [Fact]
    public void TheSituationCarriesTheMissionsBlock()
    {
        var block = Situation.Describe(Restarted(), now: At("2026-09-24T10:30:30Z"));

        Assert.NotNull(block);
        Assert.Contains("Missions: 4 active, 2,988,562 cr in rewards (1 with no reward on record)", block, StringComparison.Ordinal);
        Assert.Contains("Mission: Courier Job Available, to Merchiston Dock, Kweleutahe, 30m left", block, StringComparison.Ordinal);
        Assert.Contains("Mission: Mission_Collect, no destination or reward on record, 1h 30m left", block, StringComparison.Ordinal);
        Assert.Contains("Mission: Imperial Navy Strike Contract, to Morelos Point, Mot, 13h 23m left", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Boom time delivery", block, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExpiredMissionIsCountedButNotNamedAheadOfLiveOnes()
    {
        var block = Situation.Describe(Restarted(), now: At("2026-09-24T11:30:30Z"));

        Assert.NotNull(block);
        Assert.Contains("Missions: 4 active (1 past expiry)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Courier Job Available", block, StringComparison.Ordinal);
        Assert.Contains("Mission: Boom time delivery of 99 units of Polymers, to Crown Barracks, Wadjuk, 23h 30m left", block, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyBoardAddsNoMissionsBlock()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2026-09-24T10:00:05Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }"""));
        store.Apply(Event("""{ "timestamp":"2026-09-24T10:00:06Z", "event":"Location", "StarSystem":"Sol", "Docked":false }"""));
        store.Apply(Event("""{ "timestamp":"2026-09-24T10:00:30Z", "event":"Missions", "Active":[], "Failed":[], "Complete":[] }"""));

        var block = Situation.Describe(store.Active, now: At("2026-09-24T10:30:30Z"));

        Assert.NotNull(block);
        Assert.DoesNotContain("Mission", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The newest session replayed live, then the walk's board offered late, as the app does when the walk
    /// finishes after the priming tick.
    /// </summary>
    private static CommanderGameState Restarted()
    {
        var recovered = MissionBackfill.FromHistory(Fixtures, NullLogger.Instance, TestContext.Current.CancellationToken);

        var store = new GameStateStore { RestoreMissions = recovered.GetValueOrDefault };
        Replay(store, Newest);
        store.RestoreLate();

        return store.Active!;
    }

    private static void Replay(GameStateStore store, string file)
    {
        var reader = new JournalReader(file, NullLogger.Instance);

        while (reader.Poll() is { Count: > 0 } batch)
        {
            foreach (var journalEvent in batch)
            {
                store.Apply(journalEvent);
            }
        }
    }

    private static string Fixtures
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return Path.Combine(directory.FullName, "tests", "fixtures", "missions");
        }
    }

    private static string Newest => Path.Combine(Fixtures, "Journal.2026-09-24T100000.01.log");

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }

    private static DateTimeOffset At(string timestamp) =>
        DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);

    private static string LoadGame(string day) =>
        $$"""{ "timestamp":"{{day}}T10:00:00Z", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Fixture" }""";

    private static string Accept(long id, string day) =>
        $$"""{ "timestamp":"{{day}}T10:10:00Z", "event":"MissionAccepted", "Faction":"Party of Yoru", "Name":"Mission_Courier", "LocalisedName":"Courier Job Available", "DestinationSystem":"Wadjuk", "DestinationStation":"Crown Barracks", "Expiry":"2026-09-30T00:00:00Z", "Reward":1000, "MissionID":{{id}} }""";

    private static string Snapshot(long id, string day) =>
        $$"""{ "timestamp":"{{day}}T10:00:30Z", "event":"Missions", "Active":[ { "MissionID":{{id}}, "Name":"Mission_Courier", "PassengerMission":false, "Expires":3600 } ], "Failed":[], "Complete":[] }""";

    private static string Write(TempInstall install, string name, params string[] lines)
    {
        var path = Path.Combine(install.Root, name);
        File.WriteAllLines(path, lines);
        return path;
    }
}
