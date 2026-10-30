using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public class FullyFoundSystemsAreFoldedByTheBackfillTests
{
    [Fact]
    public void TheWalkKeepsSystemsFoundWholeInEarlierJournalsPerCommander()
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(
            Path.Combine(install.Root, "Journal.2026-10-01T090000.01.log"),
            [
                """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame","FID":"F1"}""",
                """{"timestamp":"2026-10-01T09:05:00Z","event":"FSSAllBodiesFound","SystemAddress":1,"Count":3}""",
                """{"timestamp":"2026-10-01T09:06:00Z","event":"FSSDiscoveryScan","SystemAddress":2,"Progress":0.4,"BodyCount":9}""",
            ]);
        install.Files.WriteLines(
            Path.Combine(install.Root, "Journal.2026-10-02T090000.01.log"),
            [
                """{"timestamp":"2026-10-02T09:00:00Z","event":"LoadGame","FID":"F1"}""",
                """{"timestamp":"2026-10-02T09:05:00Z","event":"FSSDiscoveryScan","SystemAddress":3,"Progress":1,"BodyCount":4}""",
                """{"timestamp":"2026-10-02T10:00:00Z","event":"LoadGame","FID":"F2"}""",
                """{"timestamp":"2026-10-02T10:05:00Z","event":"FSSAllBodiesFound","SystemAddress":4,"Count":2}""",
            ]);

        var found = new FullyFoundSystems(install.Files, NullLogger.Instance);
        var backfill = new HistoryBackfill
        {
            Directory = install.Root,
            FileSystem = install.Files,
            Loggers = NullLoggerFactory.Instance,
            FullyFound = found,
        };

        backfill.Run(TestContext.Current.CancellationToken);

        Assert.True(found.Contains("F1", 1));
        Assert.False(found.Contains("F1", 2));
        Assert.True(found.Contains("F1", 3));
        Assert.False(found.Contains("F1", 4));
        Assert.True(found.Contains("F2", 4));
    }
}
