using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ACommanderErasesAreKnownFromTheJournalsTests
{
    private static string New(string day, string fid) =>
        "{\"timestamp\":\"2026-10-" + day + "T10:00:00Z\",\"event\":\"NewCommander\",\"Name\":\"X\",\"FID\":\"" + fid + "\"}";

    private static CommanderErases Erases(MemoryFileSystem files) => new(files, NullLogger<CommanderErases>.Instance);

    [Fact]
    public void TheHistoryGivesEachCommandersLatestErase()
    {
        var files = new MemoryFileSystem();
        files.WriteText("a.log", New("01", "F") + "\n" + New("02", "G") + "\n");
        files.WriteText("b.log", New("05", "F") + "\n");

        var erases = Erases(files);
        erases.FoldHistory(["a.log", "b.log"], TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeOffset.Parse("2026-10-05T10:00:00Z"), erases.Of("F"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-02T10:00:00Z"), erases.Of("G"));
        Assert.Null(erases.Of("H"));
    }

    [Fact]
    public void AnEarlierEraseDoesNotMoveTheTimeBack()
    {
        var files = new MemoryFileSystem();
        files.WriteText("a.log", New("05", "F") + "\n" + New("01", "F") + "\n");

        var erases = Erases(files);
        erases.FoldHistory(["a.log"], TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeOffset.Parse("2026-10-05T10:00:00Z"), erases.Of("F"));
    }

    [Fact]
    public void ALiveNewCommanderMovesTheTimeForwardAndRaisesTheChangeOnce()
    {
        var files = new MemoryFileSystem();
        files.WriteText("a.log", New("01", "F") + "\n");

        var erases = Erases(files);
        erases.FoldHistory(["a.log"], TestContext.Current.CancellationToken);

        var raised = new List<(string Fid, DateTimeOffset At)>();
        erases.Changed += (fid, at) => raised.Add((fid, at));

        Assert.True(JournalEvent.TryParse(New("09", "F"), NullLogger.Instance, out var later));
        erases.Apply([later!, later!]);

        Assert.Equal(DateTimeOffset.Parse("2026-10-09T10:00:00Z"), erases.Of("F"));
        Assert.Equal([("F", DateTimeOffset.Parse("2026-10-09T10:00:00Z"))], raised);
    }
}
