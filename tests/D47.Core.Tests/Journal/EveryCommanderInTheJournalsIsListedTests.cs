using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class EveryCommanderInTheJournalsIsListedTests
{
    [Fact]
    public void TwoCommandersEachGetTheirNewestFilesValues()
    {
        var install = new MemoryInstall();

        Write(
            install,
            "Journal.2026-08-01T100000.01.log",
            """{ "timestamp":"2026-08-01T10:00:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Old Name", "Ship":"python" }""",
            """{ "timestamp":"2026-08-01T10:05:00Z", "event":"Docked", "StarSystem":"Old System" }""");

        Write(
            install,
            "Journal.2026-08-02T100000.01.log",
            """{ "timestamp":"2026-08-02T10:00:00Z", "event":"Commander", "FID":"F1", "Name":"Alpha" }""",
            """{ "timestamp":"2026-08-02T10:00:01Z", "event":"LoadGame", "FID":"F1", "Commander":"Alpha", "Ship":"Anaconda", "Ship_Localised":"Anaconda" }""",
            """{ "timestamp":"2026-08-02T10:10:00Z", "event":"FSDJump", "StarSystem":"Sol" }""",
            """{ "timestamp":"2026-08-02T10:20:00Z", "event":"Music", "MusicTrack":"Exploration" }""");

        Write(
            install,
            "Journal.2026-08-03T100000.01.log",
            """{ "timestamp":"2026-08-03T10:00:00Z", "event":"LoadGame", "FID":"F2", "Commander":"Bravo", "Ship":"SideWinder" }""",
            """{ "timestamp":"2026-08-03T10:01:00Z", "event":"Loadout", "Ship":"python", "ShipID":7 }""",
            """{ "timestamp":"2026-08-03T10:02:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra" }""",
            """{ "timestamp":"2026-08-03T10:30:00Z", "event":"CarrierJump", "StarSystem":"Colonia" }""");

        var (commanders, examined) = CommanderBackfill.FromHistory(
            install.Files,
            Files(install), NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(3, examined);
        Assert.Equal(2, commanders.Count);

        var alpha = commanders["F1"];
        Assert.Equal("Alpha", alpha.Name);
        Assert.Equal(DateTimeOffset.Parse("2026-08-02T10:20:00Z"), alpha.LastSeen);
        Assert.Equal("Sol", alpha.StarSystem);
        Assert.Equal("Anaconda", alpha.Ship);

        var bravo = commanders["F2"];
        Assert.Equal("Bravo", bravo.Name);
        Assert.Equal(DateTimeOffset.Parse("2026-08-03T10:30:00Z"), bravo.LastSeen);
        Assert.Equal("Colonia", bravo.StarSystem);
        Assert.Equal("Python", bravo.Ship);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AHistoryWalkOffersTheCommandersOnceItIsDone()
    {
        var install = new MemoryInstall();

        Write(
            install,
            "Journal.2026-08-01T100000.01.log",
            """{ "timestamp":"2026-08-01T10:00:00Z", "event":"LoadGame", "FID":"F1", "Commander":"Alpha", "Ship":"python" }""");

        var backfill = new HistoryBackfill { Directory = install.Root, FileSystem = install.Files, Loggers = NullLoggerFactory.Instance };

        Assert.Null(backfill.Commanders);

        backfill.Run(TestContext.Current.CancellationToken);

        Assert.Equal(["F1"], backfill.Commanders!.Keys);
        Assert.Equal(1, backfill.CommanderFilesExamined);
    }

    private static string[] Files(MemoryInstall install) =>
        [.. install.Files.Enumerate(install.Root, JournalFolder.FilePattern).OrderBy(Path.GetFileName, StringComparer.Ordinal)];

    private static void Write(MemoryInstall install, string name, params string[] lines) =>
        install.Files.WriteLines(Path.Combine(install.Root, name), lines);
}
