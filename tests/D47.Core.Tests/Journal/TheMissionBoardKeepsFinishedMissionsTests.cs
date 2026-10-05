using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="MissionBoard.Finished"/>: missions that ended since the latest snapshot.</summary>
public sealed class TheMissionBoardKeepsFinishedMissionsTests
{
    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }

    private static string Accept(long id) =>
        $$"""{ "timestamp":"2026-09-30T10:00:00Z", "event":"MissionAccepted", "Faction":"Party of Yoru", "Name":"Mission_Courier", "LocalisedName":"Courier", "DestinationSystem":"Wadjuk", "DestinationStation":"Crown Barracks", "Expiry":"2026-10-30T00:00:00Z", "Reward":1000, "MissionID":{{id}} }""";

    private static string End(string kind, long id, int minute, string extra = "") =>
        $$"""{ "timestamp":"2026-09-30T11:{{minute:00}}:00Z", "event":"{{kind}}", "MissionID":{{id}}{{extra}} }""";

    private static MissionBoard Fold(params string[] lines) =>
        lines.Aggregate(MissionBoard.Empty, (board, line) => board.Apply(Event(line)));

    [Fact]
    public void ACompletedAFailedAndAnAbandonedMissionEachAppearNewestFirstAndLeaveTheBoard()
    {
        var board = Fold(
            Accept(1), Accept(2), Accept(3),
            End("MissionCompleted", 1, 1, ", \"Reward\":5000"),
            End("MissionFailed", 2, 2),
            End("MissionAbandoned", 3, 3));

        Assert.Empty(board.Missions);
        Assert.Equal([3L, 2L, 1L], board.Finished.Select(f => f.Mission.Id));
        Assert.Equal(
            [MissionOutcome.Abandoned, MissionOutcome.Failed, MissionOutcome.Completed],
            board.Finished.Select(f => f.Outcome));
        Assert.Equal([null, null, 5000L], board.Finished.Select(f => f.Paid));
    }

    [Fact]
    public void ACompletedMissionWhoseAcceptWasReadKeepsItsDestinationAndFaction()
    {
        var board = Fold(Accept(1), End("MissionCompleted", 1, 1, ", \"Reward\":5000"));

        var finished = Assert.Single(board.Finished);
        Assert.Equal("Crown Barracks, Wadjuk", finished.Mission.Destination);
        Assert.Equal("Party of Yoru", finished.Mission.Faction);
        Assert.Equal(5000, finished.Paid);
    }

    [Fact]
    public void AMissionNeverAcceptedHereIsDescribedByItsEndEvent()
    {
        var board = Fold(End("MissionCompleted", 9, 1, ", \"Name\":\"Mission_Delivery\", \"Reward\":700"));

        var finished = Assert.Single(board.Finished);
        Assert.Equal("Mission_Delivery", finished.Mission.Name);
        Assert.Equal(700, finished.Paid);
    }

    [Fact]
    public void AMissionsSnapshotEmptiesTheFinishedList()
    {
        var board = Fold(
            Accept(1),
            End("MissionFailed", 1, 1),
            """{ "timestamp":"2026-09-30T12:00:00Z", "event":"Missions", "Active":[], "Failed":[], "Complete":[] }""");

        Assert.Empty(board.Finished);
    }
}
