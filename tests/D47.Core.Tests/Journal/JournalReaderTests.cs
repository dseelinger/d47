using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalReaderTests
{
    private const string Root = @"C:\d47-test\journals";

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void ParsesAValidLineIntoAnEvent()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");

        var events = new JournalReader(path, _files, NullLogger.Instance).Poll();

        Assert.Single(events);
        Assert.Equal("Commander", events[0].Kind);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), events[0].Timestamp);
    }

    [Fact]
    public void MalformedLineIsSkippedAndLoggedButDoesNotStopTheTailLoop()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n" +
            "not json at all\n" +
            """{"timestamp":"2026-01-01T00:00:02Z","event":"Location","StarSystem":"Somewhere"}""" + "\n");

        var logger = new RecordingLogger();
        var events = new JournalReader(path, _files, logger).Poll();

        Assert.Equal(2, events.Count);
        Assert.Equal("Commander", events[0].Kind);
        Assert.Equal("Location", events[1].Kind);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void UnrecognisedEventNameStillParses()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(path, """{"timestamp":"2026-01-01T00:00:00Z","event":"SomethingFromTheFuture","field":1}""" + "\n");

        var events = new JournalReader(path, _files, NullLogger.Instance).Poll();

        Assert.Single(events);
        Assert.Equal("SomethingFromTheFuture", events[0].Kind);
    }

    [Fact]
    public void APartialLineIsNotParsedUntilItIsComplete()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        var full = """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n";

        // Simulate Elite mid-write: everything except the trailing newline.
        _files.WriteText(path, full[..^1]);
        var reader = new JournalReader(path, _files, NullLogger.Instance);

        Assert.Empty(reader.Poll());

        _files.AppendText(path, "\n");
        var events = reader.Poll();

        Assert.Single(events);
        Assert.Equal("Commander", events[0].Kind);
    }

    [Fact]
    public void RepeatedPollsOnlyReturnNewEvents()
    {
        var path = Path.Combine(Root, "Journal.test.log");
        _files.WriteText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");

        var reader = new JournalReader(path, _files, NullLogger.Instance);
        Assert.Single(reader.Poll());
        Assert.Empty(reader.Poll());

        _files.AppendText(path, """{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Somewhere"}""" + "\n");
        var second = reader.Poll();

        Assert.Single(second);
        Assert.Equal("Location", second[0].Kind);
    }

    [Fact]
    public void SameContentProducesTheSameEventsWhetherPolledOnceOrManyTimes()
    {
        // The "1x vs 100x" replay property: the reader owns no clock, so the number of Poll() calls used to
        // consume a file must never change the resulting events, only how many calls it took to see them all.
        string[] lines =
        [
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}""",
            """{"timestamp":"2026-01-01T00:00:02Z","event":"FSDJump","StarSystem":"Beta"}""",
        ];

        // "100x": everything written at once, one Poll() drains it all.
        var fastPath = Path.Combine(Root, "fast.log");
        _files.WriteText(fastPath, string.Join('\n', lines) + "\n");
        var fastEvents = new JournalReader(fastPath, _files, NullLogger.Instance).Poll();

        // "1x": each line appended and polled in turn, as the tick loop would see it written live.
        var slowPath = Path.Combine(Root, "slow.log");
        _files.WriteText(slowPath, "");
        var slowReader = new JournalReader(slowPath, _files, NullLogger.Instance);
        var slowEvents = new List<JournalEvent>();
        foreach (var line in lines)
        {
            _files.AppendText(slowPath, line + "\n");
            slowEvents.AddRange(slowReader.Poll());
        }

        Assert.Equal(fastEvents.Select(e => e.Kind), slowEvents.Select(e => e.Kind));
        Assert.Equal(fastEvents.Select(e => e.Timestamp), slowEvents.Select(e => e.Timestamp));
    }
}

[Trait("Category", "Integration")]
public class AJournalEliteHoldsOpenStillReadsTests
{
    [Fact]
    public void ReadingWorksWhileAnotherHandleHasTheFileOpenForWriting()
    {
        // Elite keeps the file open with similar sharing for the whole session.
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, "");

        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var line = Encoding.UTF8.GetBytes(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");
        writer.Write(line);
        writer.Flush();

        var events = new JournalReader(path, new DiskFileSystem(), NullLogger.Instance).Poll();

        Assert.Single(events);
    }
}
