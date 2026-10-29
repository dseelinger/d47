using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// With Elite closed the tailed journal can hold nothing but a Fileheader, so the Commander shown has no
/// location until the walk back through older journals restores the last one.
/// </summary>
[Trait("Category", "Integration")]
public class ACommanderShownWithNoLiveLocationTakesTheLastOneTests
{
    private const string Fid = "F1234567";

    [Fact]
    public void APickedCommanderGetsTheSystemAndPositionTheirJournalsLastShowed()
    {
        var install = new MemoryInstall();
        Write(install, "Journal.2026-10-28T100000.01.log", LoadGame(Fid), Jump("Lantern Reach", "[10.5,-2.25,3]"), Jump("Tavell's Rest", "[442.5,-69.21875,-11.3125]"), Docked("Tavell's Rest", "Hodge Port"));
        Write(install, "Journal.2026-10-29T100000.01.log", Fileheader);

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Pick(new CommanderIdentity(Fid, "Fixture"));
        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        var location = store.Active!.Location;

        Assert.Equal("Tavell's Rest", location.StarSystem);
        Assert.Equal(new StarPosition(442.5, -69.21875, -11.3125), location.StarPos);
        Assert.Equal("Hodge Port", location.StationName);
    }

    [Fact]
    public void ALiveLocationIsNotReplaced()
    {
        var install = new MemoryInstall();
        Write(install, "Journal.2026-10-28T100000.01.log", LoadGame(Fid), Jump("Tavell's Rest", "[442.5,-69.21875,-11.3125]"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame(Fid)));
        store.Apply(Event(Jump("Lantern Reach", "[10.5,-2.25,3]")));
        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        Assert.Equal("Lantern Reach", store.Active!.Location.StarSystem);
    }

    [Fact]
    public void EachCommanderTakesTheirOwnLastLocation()
    {
        var install = new MemoryInstall();
        Write(install, "Journal.2026-10-27T100000.01.log", LoadGame(Fid), Jump("Tavell's Rest", "[442.5,-69.21875,-11.3125]"));
        Write(install, "Journal.2026-10-28T100000.01.log", LoadGame("F7654321"), Jump("Lantern Reach", "[10.5,-2.25,3]"));

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Pick(new CommanderIdentity(Fid, "Fixture"));
        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        Assert.Equal("Tavell's Rest", store.Active!.Location.StarSystem);
    }

    private static HistoryBackfill Backfill(MemoryInstall install) => new()
    {
        Directory = install.Root,
        FileSystem = install.Files,
        Loggers = NullLoggerFactory.Instance,
    };

    private static GameStateStore StoreOver(HistoryBackfill backfill) => new()
    {
        RestoreLocation = fid => backfill.Locations?.GetValueOrDefault(fid),
    };

    private const string Fileheader =
        """{"timestamp":"2026-10-29T10:00:00Z","event":"Fileheader","part":1,"gameversion":"4.4.2.1"}""";

    private static string LoadGame(string fid) =>
        $$"""{"timestamp":"2026-10-28T10:00:00Z","event":"LoadGame","FID":"{{fid}}","Commander":"Fixture"}""";

    private static string Jump(string system, string position) =>
        $$"""{"timestamp":"2026-10-28T10:05:00Z","event":"FSDJump","StarSystem":"{{system}}","SystemAddress":1,"StarPos":{{position}}}""";

    private static string Docked(string system, string station) =>
        $$"""{"timestamp":"2026-10-28T10:20:00Z","event":"Docked","StationName":"{{station}}","StationType":"Coriolis","StarSystem":"{{system}}","SystemAddress":1,"MarketID":5}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Write(MemoryInstall install, string name, params string[] lines) =>
        install.Files.WriteLines(Path.Combine(install.Root, name), lines);
}
