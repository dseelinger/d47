using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class TheMissionBoardKeepsItsMissionsThroughAnOperationTests
{
    private const long Own = 1066795391;
    private const long Operation = 1090000001;

    private static readonly string[] Lines =
    [
        """{"timestamp":"2026-10-20T10:00:00Z","event":"LoadGame","Commander":"X"}""",
        """{"timestamp":"2026-10-20T10:00:01Z","event":"MissionAccepted","MissionID":1066795391,"Name":"Mission_Courier","LocalisedName":"Courier Job","DestinationSystem":"Kweleutahe","DestinationStation":"Merchiston Dock","Reward":79014,"Expiry":"2026-10-25T11:00:00Z"}""",
        """{"timestamp":"2026-10-20T10:05:00Z","event":"GameModeChange","GameMode":"Operation"}""",
        """{"timestamp":"2026-10-20T10:05:01Z","event":"LoadGame","Commander":"X"}""",
        """{"timestamp":"2026-10-20T10:05:02Z","event":"Missions","Active":[{"MissionID":1090000001,"Name":"Tactical Takedown","PassengerMission":false,"Expires":0}],"Failed":[],"Complete":[]}""",
        """{"timestamp":"2026-10-20T10:30:00Z","event":"GameModeChange","GameMode":"MainGame"}""",
        """{"timestamp":"2026-10-20T10:30:01Z","event":"Missions","Active":[{"MissionID":1066795391,"Name":"Mission_Courier","PassengerMission":false,"Expires":432000}],"Failed":[],"Complete":[]}""",
    ];

    private static MissionBoard Fold(int count) => OperationMarker
        .Mark(Lines.Take(count).Select(Parse))
        .Aggregate(MissionBoard.Empty, (board, line) => board.Apply(line));

    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void TheEntrySnapshotDoesNotReplaceTheCommandersMissions()
    {
        var board = Fold(5);

        var own = board.For(Own);
        Assert.NotNull(own);
        Assert.True(own.HasDetail);
        Assert.Equal("Merchiston Dock, Kweleutahe", own.Destination);
        Assert.Equal(79014, own.Reward);
        Assert.NotNull(own.Expiry);
        Assert.Null(board.For(Operation));
        Assert.Single(board.Missions);
    }

    [Fact]
    public void TheExitSnapshotIsAppliedAndKeepsTheDetail()
    {
        var board = Fold(7);

        var own = board.For(Own);
        Assert.NotNull(own);
        Assert.Equal("Merchiston Dock, Kweleutahe", own.Destination);
        Assert.Equal(79014, own.Reward);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T10:30:01Z"), own.Expiry);
        Assert.Null(board.For(Operation));
        Assert.NotNull(board.SnapshotAt);
    }
}
