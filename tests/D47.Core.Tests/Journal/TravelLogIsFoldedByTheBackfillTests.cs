using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public class TravelLogIsFoldedByTheBackfillTests
{
    [Fact]
    public void TheWalkReadsStepsBackFromTwoJournalFiles()
    {
        var install = new MemoryInstall();
        install.Files.WriteLines(
            Path.Combine(install.Root, "Journal.2026-10-01T090000.01.log"),
            [
                """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame","FID":"F1"}""",
                """{"timestamp":"2026-10-01T09:05:00Z","event":"FSDJump","StarSystem":"Alpha","SystemAddress":1,"StarPos":[1,2,3]}""",
            ]);
        install.Files.WriteLines(
            Path.Combine(install.Root, "Journal.2026-10-02T090000.01.log"),
            [
                """{"timestamp":"2026-10-02T09:00:00Z","event":"LoadGame","FID":"F1"}""",
                """{"timestamp":"2026-10-02T09:05:00Z","event":"CarrierJump","StarSystem":"Beta","SystemAddress":2,"StarPos":[4,5,6]}""",
            ]);

        var log = new TravelLog(install.Files, NullLogger.Instance);
        var backfill = new HistoryBackfill
        {
            Directory = install.Root,
            FileSystem = install.Files,
            Loggers = NullLoggerFactory.Instance,
            Travel = log,
        };

        backfill.Run(TestContext.Current.CancellationToken);

        Assert.True(log.HistoryFolded);
        Assert.Equal(["Alpha", "Beta"], log.Steps("F1").Select(step => step.System));
        Assert.Equal(TravelArrival.CarrierJump, log.Steps("F1")[1].How);
    }
}
