using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AJournalMarkNamesWhereTheTailHasReadTests
{
    private const string Root = @"C:\d47-test";

    private readonly MemoryFileSystem _files = new();

    private static string Line(int n) =>
        $$"""{"timestamp":"2026-01-01T00:00:00Z","event":"Music","MusicTrack":"T{{n}}"}""" + "\n";

    [Fact]
    public void AReaderWithALimitStopsAtTheEndOfThatLineWhileTheFileGrows()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(path, Line(1) + Line(2) + Line(3));
        var until = Encoding.UTF8.GetByteCount(Line(1) + Line(2));
        var reader = new JournalReader(path, _files, NullLogger.Instance, until);

        Assert.Equal(2, reader.Poll().Count);
        _files.AppendText(path, Line(4) + Line(5));

        Assert.Empty(reader.Poll());
        Assert.Empty(reader.Poll());
        Assert.Equal(until, reader.Position);
    }

    [Fact]
    public void AReaderWithALimitInsideALineLeavesThatLineUnread()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(path, Line(1) + Line(2));
        var until = Encoding.UTF8.GetByteCount(Line(1)) + 10;

        var events = new JournalReader(path, _files, NullLogger.Instance, until).Poll();

        Assert.Single(events);
    }

    [Fact]
    public void AReaderWithNoLimitReadsEverythingAsBefore()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(path, Line(1));
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        Assert.Single(reader.Poll());
        _files.AppendText(path, Line(2));

        Assert.Single(reader.Poll());
        Assert.Equal(_files.Stat(path)!.Value.Length, reader.Position);
    }

    [Fact]
    public void TheSpineMarkNamesTheTailedFileAndItsLength()
    {
        var path = Path.Combine(Root, "Journal.2026-01-01T000000.01.log");
        var spine = new JournalSpine(Root, _files, new GameStateStore(), NullLoggerFactory.Instance);
        Assert.Null(spine.Mark);

        _files.WriteText(path, Line(1) + Line(2));
        spine.Poll();

        Assert.Equal(new JournalMark(spine.CurrentFile!, _files.Stat(path)!.Value.Length), spine.Mark);
    }
}
