using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A ship d47 has remembered stays in <c>loadouts.json</c> until the journal says it was sold or replaced,
/// and a ship a build names is found however far back its last <c>Loadout</c> is (#475).
/// </summary>
public class ARememberedShipStaysRememberedTests : IDisposable
{
    private static readonly int[] Fleet = [3, 5, 8, 13, 21, 24, 30, 37, 40, 44, 51];

    private readonly string _root = Directory.CreateTempSubdirectory("d47-remembered").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LoadoutStore Store() => new(Path.Combine(_root, "loadouts.json"), NullLogger<LoadoutStore>.Instance);

    private static readonly DateTimeOffset Folded = new(2026, 9, 25, 3, 10, 19, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Commander(string timestamp) =>
        $$"""{"timestamp":"{{timestamp}}","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static string Boarding(string timestamp, int id, string hull = "cobramkv") =>
        $$"""{ "timestamp":"{{timestamp}}", "event":"Loadout", "Ship":"{{hull}}", "ShipID":{{id}}, "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":1,"Health":1.0}] }""";

    private string Journal(string stamp, params string[] lines)
    {
        var file = Path.Combine(_root, $"Journal.{stamp}.01.log");
        File.WriteAllLines(file, lines);
        return file;
    }

    /// <summary>A file holding every ship in <see cref="Fleet"/>, written the way a running d47 writes it.</summary>
    private LoadoutStore StoredFleet(params int[] ids)
    {
        var flown = new GameStateStore();

        flown.Apply(Event(Commander("2026-09-24T20:00:00Z")));

        foreach (var id in ids)
        {
            flown.Apply(Event(Boarding("2026-09-24T21:00:00Z", id)));
        }

        Store().Save(flown.All, Folded);

        var store = Store();
        store.Load();
        return store;
    }

    private IReadOnlyCollection<int> OnDisk()
    {
        var reading = Store();
        reading.Load();
        return [.. (reading.For("F1")?.Ships.Keys ?? [])];
    }

    /// <summary>
    /// The start the application log recorded: the priming tick folds the current journal and saves
    /// before the history walk has handed over what the file held.
    /// </summary>
    [Fact]
    public void ASaveBeforeTheRestoreKeepsEveryShipTheFileHeld()
    {
        var store = StoredFleet(Fleet);

        IReadOnlyDictionary<string, ShipLoadouts>? history = null;

        var gameState = new GameStateStore
        {
            RestoreLoadouts = fid => history?.GetValueOrDefault(fid),
        };

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));
        gameState.Apply(Event(Boarding("2026-09-25T03:29:41Z", 37)));

        store.Save(gameState.All, gameState.Active!.Loadouts.Ships[37].SeenAt);

        Assert.Equal(Fleet, OnDisk().Order());

        var journal = Journal(
            "2026-09-25T032940",
            Commander("2026-09-25T03:29:40Z"),
            Boarding("2026-09-25T03:29:41Z", 37));

        history = LoadoutBackfill.FromHistory(
            [journal], NullLogger.Instance, store.All, cancellation: TestContext.Current.CancellationToken);

        gameState.RestoreLate();
        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Equal(Fleet, OnDisk().Order());
    }

    [Fact]
    public void ASaleBeforeTheRestoreStillTakesTheShipOutOfTheFile()
    {
        var store = StoredFleet(42, 51);

        var gameState = new GameStateStore();

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));
        gameState.Apply(Event(Boarding("2026-09-25T03:29:41Z", 51)));
        gameState.Apply(Event("""{"timestamp":"2026-09-25T03:30:00Z","event":"ShipyardSell","SellShipID":42}"""));

        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Equal([51], OnDisk());
    }

    [Fact]
    public void ASaleAfterTheRestoreTakesTheShipOutOfTheFile()
    {
        var store = StoredFleet(42, 51);

        var gameState = new GameStateStore { RestoreLoadouts = store.For };

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));
        gameState.Apply(Event("""{"timestamp":"2026-09-25T03:30:00Z","event":"ShipyardSell","SellShipID":42}"""));

        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Equal([51], OnDisk());
    }

    /// <summary>A ship sold while d47 was closed does not come back through the merge.</summary>
    [Fact]
    public void ASaleTheHistoryWalkSawIsNotWrittenBack()
    {
        var store = StoredFleet(42, 51);

        var journal = Journal(
            "2026-09-25T032940",
            Commander("2026-09-25T03:29:40Z"),
            """{"timestamp":"2026-09-25T03:29:50Z","event":"ShipyardSell","SellShipID":42}""",
            Boarding("2026-09-25T03:30:00Z", 51));

        var history = LoadoutBackfill.FromHistory(
            [journal], NullLogger.Instance, store.All, cancellation: TestContext.Current.CancellationToken);

        var gameState = new GameStateStore { RestoreLoadouts = history.GetValueOrDefault };

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));

        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Equal([51], OnDisk());
    }

    [Fact]
    public void ANewCommanderEmptiesTheFile()
    {
        var store = StoredFleet(42, 51);

        var gameState = new GameStateStore();

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));
        gameState.Apply(Event("""{"timestamp":"2026-09-25T03:30:00Z","event":"NewCommander","FID":"F1","Name":"Jameson"}"""));

        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Empty(OnDisk());
    }

    /// <summary>A rescan's picture replaces the file rather than merging into it.</summary>
    [Fact]
    public void ARescanStillReplacesTheFile()
    {
        var store = StoredFleet(42, 51);

        var gameState = new GameStateStore();

        gameState.Apply(Event(Commander("2026-09-25T03:29:40Z")));
        gameState.Apply(Event(Boarding("2026-09-25T03:29:41Z", 51)));

        gameState.ReplaceLoadouts(new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal)
        {
            ["F1"] = gameState.Active!.Loadouts,
        });

        store.Save(gameState.All, Folded.AddHours(1));

        Assert.Equal([51], OnDisk());
    }

    /// <summary>
    /// Forty journals: the Cartage boarded in the first, the Cobra in every one after. The file already has
    /// a stamp, so the window walks the floor of 25 and never reaches the first.
    /// </summary>
    private (LoadoutStore Store, DateTimeOffset Since) CartageBeyondTheWindow(params (int Day, string Line)[] extra)
    {
        for (var day = 1; day <= 40; day++)
        {
            var stamp = new DateTime(2026, 1, 1).AddDays(day - 1);
            var at = $"{stamp:yyyy-MM-dd}T10:00:00Z";

            List<string> lines = [Commander(at), Boarding($"{stamp:yyyy-MM-dd}T10:01:00Z", day == 1 ? 48 : 37, day == 1 ? "type8" : "cobramkv")];
            lines.AddRange(extra.Where(entry => entry.Day == day).Select(entry => entry.Line));

            Journal($"{stamp:yyyy-MM-dd}T100000", [.. lines]);
        }

        return (StoredFleet(37), new DateTimeOffset(2026, 2, 9, 10, 1, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AShipABuildNamesIsFoundBeyondTheWindow()
    {
        var (store, since) = CartageBeyondTheWindow();

        var unasked = LoadoutBackfill.FromHistory(
            _root, NullLogger.Instance, store.All, since, cancellation: TestContext.Current.CancellationToken);

        Assert.Null(unasked["F1"].For(48));

        var found = LoadoutBackfill.FromHistory(
            _root, NullLogger.Instance, store.All, since, [("F1", 48)], TestContext.Current.CancellationToken);

        Assert.Equal("type8", found["F1"].For(48)!.Loadout.Type);
        Assert.NotNull(found["F1"].For(37));

        // A build from before the file carried a Commander matches whoever flew it.
        var unadopted = LoadoutBackfill.FromHistory(
            _root, NullLogger.Instance, store.All, since, [(string.Empty, 48)], TestContext.Current.CancellationToken);

        Assert.NotNull(unadopted["F1"].For(48));
    }

    [Fact]
    public void AShipSoldAfterItsLastLoadoutIsNotLookedFor()
    {
        var (store, since) = CartageBeyondTheWindow(
            (3, """{ "timestamp":"2026-01-03T11:00:00Z", "event":"ShipyardSell", "ShipType":"type8", "SellShipID":48 }"""));

        var found = LoadoutBackfill.FromHistory(
            _root, NullLogger.Instance, store.All, since, [("F1", 48)], TestContext.Current.CancellationToken);

        Assert.Null(found["F1"].For(48));
    }

    [Fact]
    public void AShipSoldInsideTheWindowIsNotBroughtBack()
    {
        var (store, since) = CartageBeyondTheWindow(
            (39, """{ "timestamp":"2026-02-08T11:00:00Z", "event":"ShipyardSell", "ShipType":"type8", "SellShipID":48 }"""));

        var found = LoadoutBackfill.FromHistory(
            _root, NullLogger.Instance, store.All, since, [("F1", 48)], TestContext.Current.CancellationToken);

        Assert.Null(found["F1"].For(48));
    }
}
