using System.Text;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalReaderTests
{
    [Fact]
    public void ParsesAValidLineIntoAnEvent()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");

        var events = new JournalReader(path, NullLogger.Instance).Poll();

        Assert.Single(events);
        Assert.Equal("Commander", events[0].Kind);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), events[0].Timestamp);
    }

    [Fact]
    public void MalformedLineIsSkippedAndLoggedButDoesNotStopTheTailLoop()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n" +
            "not json at all\n" +
            """{"timestamp":"2026-01-01T00:00:02Z","event":"Location","StarSystem":"Somewhere"}""" + "\n");

        var logger = new RecordingLogger();
        var events = new JournalReader(path, logger).Poll();

        Assert.Equal(2, events.Count);
        Assert.Equal("Commander", events[0].Kind);
        Assert.Equal("Location", events[1].Kind);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void UnrecognisedEventNameStillParses()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, """{"timestamp":"2026-01-01T00:00:00Z","event":"SomethingFromTheFuture","field":1}""" + "\n");

        var events = new JournalReader(path, NullLogger.Instance).Poll();

        Assert.Single(events);
        Assert.Equal("SomethingFromTheFuture", events[0].Kind);
    }

    [Fact]
    public void APartialLineIsNotParsedUntilItIsComplete()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        var full = """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n";

        // Simulate Elite mid-write: everything except the trailing newline.
        File.WriteAllText(path, full[..^1]);
        var reader = new JournalReader(path, NullLogger.Instance);

        Assert.Empty(reader.Poll());

        File.AppendAllText(path, "\n");
        var events = reader.Poll();

        Assert.Single(events);
        Assert.Equal("Commander", events[0].Kind);
    }

    [Fact]
    public void RepeatedPollsOnlyReturnNewEvents()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(
            path,
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");

        var reader = new JournalReader(path, NullLogger.Instance);
        Assert.Single(reader.Poll());
        Assert.Empty(reader.Poll());

        File.AppendAllText(path, """{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Somewhere"}""" + "\n");
        var second = reader.Poll();

        Assert.Single(second);
        Assert.Equal("Location", second[0].Kind);
    }

    [Fact]
    public void ReadingWorksWhileAnotherHandleHasTheFileOpenForWriting()
    {
        // This is the reason for FileShare.ReadWrite | Delete: Elite keeps the file open with
        // similar sharing for the whole session (architecture.md §4).
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, "Journal.test.log");
        File.WriteAllText(path, "");

        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var line = Encoding.UTF8.GetBytes(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""" + "\n");
        writer.Write(line);
        writer.Flush();

        var events = new JournalReader(path, NullLogger.Instance).Poll();

        Assert.Single(events);
    }

    [Fact]
    public void SameContentProducesTheSameEventsWhetherPolledOnceOrManyTimes()
    {
        // The "1x vs 100x" replay property: the reader owns no clock, so the number of Poll()
        // calls used to consume a file must never change the resulting events, only how many
        // calls it took to see them all.
        string[] lines =
        [
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Location","StarSystem":"Alpha"}""",
            """{"timestamp":"2026-01-01T00:00:02Z","event":"FSDJump","StarSystem":"Beta"}""",
        ];

        using var install = new TempInstall();

        // "100x": everything written at once, one Poll() drains it all.
        var fastPath = Path.Combine(install.Root, "fast.log");
        File.WriteAllText(fastPath, string.Join('\n', lines) + "\n");
        var fastEvents = new JournalReader(fastPath, NullLogger.Instance).Poll();

        // "1x": each line appended and polled in turn, as the tick loop would see it written live.
        var slowPath = Path.Combine(install.Root, "slow.log");
        File.WriteAllText(slowPath, "");
        var slowReader = new JournalReader(slowPath, NullLogger.Instance);
        var slowEvents = new List<JournalEvent>();
        foreach (var line in lines)
        {
            File.AppendAllText(slowPath, line + "\n");
            slowEvents.AddRange(slowReader.Poll());
        }

        Assert.Equal(fastEvents.Select(e => e.Kind), slowEvents.Select(e => e.Kind));
        Assert.Equal(fastEvents.Select(e => e.Timestamp), slowEvents.Select(e => e.Timestamp));
    }
}
