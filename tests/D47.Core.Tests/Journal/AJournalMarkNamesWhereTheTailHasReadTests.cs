using System.Text;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public class AJournalMarkNamesWhereTheTailHasReadTests
{
    private static string Line(int n) =>
        $$"""{"timestamp":"2026-01-01T00:00:00Z","event":"Music","MusicTrack":"T{{n}}"}""" + "\n";

    [Fact]
    public void AReaderWithALimitStopsAtTheEndOfThatLineWhileTheFileGrows()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, Line(1) + Line(2) + Line(3));
        var until = Encoding.UTF8.GetByteCount(Line(1) + Line(2));
        var reader = new JournalReader(path, NullLogger.Instance, until);

        Assert.Equal(2, reader.Poll().Count);
        File.AppendAllText(path, Line(4) + Line(5));

        Assert.Empty(reader.Poll());
        Assert.Empty(reader.Poll());
        Assert.Equal(until, reader.Position);
    }

    [Fact]
    public void AReaderWithALimitInsideALineLeavesThatLineUnread()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, Line(1) + Line(2));
        var until = Encoding.UTF8.GetByteCount(Line(1)) + 10;

        var events = new JournalReader(path, NullLogger.Instance, until).Poll();

        Assert.Single(events);
    }

    [Fact]
    public void AReaderWithNoLimitReadsEverythingAsBefore()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, Line(1));
        var reader = new JournalReader(path, NullLogger.Instance);

        Assert.Single(reader.Poll());
        File.AppendAllText(path, Line(2));

        Assert.Single(reader.Poll());
        Assert.Equal(new FileInfo(path).Length, reader.Position);
    }

    [Fact]
    public void TheSpineMarkNamesTheTailedFileAndItsLength()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.2026-01-01T000000.01.log");
        var spine = new JournalSpine(install.Root, new GameStateStore(), NullLoggerFactory.Instance);
        Assert.Null(spine.Mark);

        File.WriteAllText(path, Line(1) + Line(2));
        spine.Poll();

        Assert.Equal(new JournalMark(spine.CurrentFile!, new FileInfo(path).Length), spine.Mark);
    }
}
