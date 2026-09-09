using System.Reflection;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Every ship the Commander has flown is still answerable after a restart, however long ago they last
/// sat in it.
/// </summary>
public class ALoadoutOutlivesTheBackfillWindowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-loadouts").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string File_ => Path.Combine(_root, "loadouts.json");

    private LoadoutStore Store() => new(File_, NullLogger<LoadoutStore>.Instance);

    /// <summary>How far a save says the file has been folded.</summary>
    private static readonly DateTimeOffset Folded = new(2026, 8, 20, 5, 0, 0, TimeSpan.Zero);

    /// <summary>A journal named the way Elite names them, since the walk selects on that.</summary>
    private string Journal(string stamp, params string[] lines)
    {
        var file = Path.Combine(_root, $"Journal.{stamp}.01.log");

        System.IO.File.WriteAllLines(
            file, [.. lines.Select(line => line.ReplaceLineEndings(" "))]);

        return file;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// A whole ship rather than a stub: a hull, a christened name, a figure, an engineered module and a
    /// modifier on it.
    /// </summary>
    private static string Boarding(string timestamp, string hull, int id, string module) =>
        $$$"""
          {"timestamp":"{{{timestamp}}}","event":"Loadout","Ship":"{{{hull}}}","ShipID":{{{id}}},
           "ShipName":"Kestrel","ShipIdent":"KE-01","MaxJumpRange":42.5,"HullValue":100,
           "ModulesValue":200,"Rebuy":15,"HullHealth":1.0,"UnladenMass":400.0,"CargoCapacity":64,
           "FuelCapacity":{"Main":32.0,"Reserve":1.07},
           "Modules":[{"Slot":"MainEngines","Item":"{{{module}}}","On":true,"Health":1.0,"Value":900,
                       "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":0.92,
                                      "ExperimentalEffect":"special_engine_overloaded",
                                      "Engineer":"Felicity Farseer","EngineerID":300100,
                                      "Modifiers":[{"Label":"EngineOptimalMass","Value":52.3,
                                                    "OriginalValue":45.0,"LessIsGood":0}]}}]}
          """;

    private static CommanderGameState State(LoadoutStore? store, params string[] events)
    {
        var gameState = new GameStateStore
        {
            RestoreLoadouts = fid => store?.For(fid),
        };

        gameState.Apply(Event(
            """{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var json in events)
        {
            gameState.Apply(Event(json));
        }

        return gameState.Active!;
    }

    /// <summary>The headline claim, asserted with no journals in reach at all.</summary>
    [Fact]
    public void AShipLastFlownBeyondTheWindowIsStillThereAfterARestart()
    {
        var flown = State(
            store: null,
            Boarding("2025-01-04T09:30:00Z", "type9_military", 53, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var writing = Store();
        writing.Save([flown], Folded);

        // A different process, a different store, and nothing else on disk.
        var reading = Store();
        reading.Load();

        var restored = State(reading);

        var parked = restored.Loadouts.For(53);

        Assert.NotNull(parked);
        Assert.Equal("type9_military", parked.Loadout.Type);
        Assert.Equal(new DateTimeOffset(2025, 1, 4, 9, 30, 0, TimeSpan.Zero), parked.SeenAt);
        Assert.Equal("int_engine_size7_class5", Assert.Single(parked.Loadout.Modules).Item);
    }

    /// <summary>
    /// The acceptance criterion that matters most, and the reason it does is measured: of 55 ships sold
    /// on the corpus this was built against, 17 had their id come back alive afterwards.
    /// </summary>
    [Fact]
    public void ASoldShipIsGoneFromTheFileAndItsIdDoesNotInheritTheModules()
    {
        var flown = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "type9_military", 42, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"),
            """{"timestamp":"2026-08-20T03:00:00Z","event":"ShipyardSell","SellShipID":42}""");

        var writing = Store();
        writing.Save([flown], Folded);

        var reading = Store();
        reading.Load();

        // Gone from the file, not merely gone from memory.
        Assert.Null(reading.For("F1")!.For(42));

        // And the id coming back alive on a different hull reports what is actually there rather than the
        // Type 9's modules.
        var later = State(reading, Boarding("2026-08-23T20:00:33Z", "mandalay", 42, "int_engine_size4_class5"));

        var reborn = later.Loadouts.For(42);

        Assert.NotNull(reborn);
        Assert.Equal("mandalay", reborn.Loadout.Type);
        Assert.Equal("int_engine_size4_class5", Assert.Single(reborn.Loadout.Modules).Item);
    }

    /// <summary>
    /// A purchase forgets too, which is the second and independent chance at a sale d47 was not running
    /// for.
    /// </summary>
    [Fact]
    public void BuyingAShipForgetsWhateverElseHeldItsId()
    {
        var state = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "type9_military", 42, "int_engine_size7_class5"),
            """{"timestamp":"2026-08-27T20:00:33Z","event":"ShipyardNew","ShipType":"mandalay","NewShipID":42}""");

        Assert.Null(state.Loadouts.For(42));
    }

    /// <summary>
    /// The part exchange: one <c>ShipyardBuy</c> of the 34 measured carries a <c>SellShipID</c>, which
    /// is a sale wearing another event's name.
    /// </summary>
    [Fact]
    public void APartExchangeIsASaleAndIsForgotten()
    {
        var state = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "type9_military", 42, "int_engine_size7_class5"),
            """{"timestamp":"2026-08-20T03:00:00Z","event":"ShipyardBuy","ShipType":"mandalay","SellShipID":42,"SellPrice":1}""");

        Assert.Null(state.Loadouts.For(42));
    }

    /// <summary>
    /// A sale d47 was closed for still takes effect, because the catch-up is seeded with the file
    /// rather than rebuilding over it — so the window replays the sale through the same fold the live
    /// path uses and the ship comes out of the long memory.
    /// </summary>
    [Fact]
    public void ASaleInsideTheCatchUpWindowRemovesAShipTheFileStillHeld()
    {
        var flown = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "type9_military", 42, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var store = Store();
        store.Save([flown], Folded);

        Assert.NotNull(store.For("F1")!.For(42));

        // One journal the Commander flew while d47 was closed, which sells the Type 9.
        var journal = Path.Combine(_root, "Journal.2026-08-21T100000.01.log");

        System.IO.File.WriteAllLines(journal,
        [
            """{"timestamp":"2026-08-21T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-21T10:05:00Z","event":"ShipyardSell","SellShipID":42}""",
        ]);

        var caught = LoadoutBackfill.FromHistory([journal], NullLogger.Instance, store.All);

        Assert.True(caught.TryGetValue("F1", out var ships));
        Assert.Null(ships.For(42));

        // And the ship that was not sold is still there, which is the reason for seeding.
        Assert.NotNull(ships.For(51));
    }

    /// <summary>
    /// A sale thirty journals back is still found, which is what the watermark buys: the event never
    /// went anywhere, so the walk reaches to wherever the file left off rather than to a fixed number
    /// of files.
    /// </summary>
    [Fact]
    public void ASaleFarOutsideTheOldWindowIsStillFound()
    {
        var flown = State(
            store: null,
            Boarding("2026-01-02T01:00:00Z", "type9_military", 42, "int_engine_size7_class5"),
            Boarding("2026-01-02T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var store = Store();
        store.Save([flown], new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero));

        // The sale, and then forty quiet journals after it — well past the 25 the window falls back to when nothing says otherwise.
        var journals = new List<string>
        {
            Journal("2026-01-03T100000",
                """{"timestamp":"2026-01-03T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                """{"timestamp":"2026-01-03T10:05:00Z","event":"ShipyardSell","SellShipID":42}"""),
        };

        for (var day = 4; day < 44; day++)
        {
            journals.Add(Journal(
                $"2026-01-{day:00}T100000",
                $$"""{"timestamp":"2026-01-{{day:00}}T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        }

        var window = LoadoutBackfill.Window(journals, store.FoldedThrough);

        // Wider than the floor, because the gap is wider than the floor.
        Assert.True(window.Count > 25, $"walked {window.Count} files");

        var caught = LoadoutBackfill.FromHistory(window, NullLogger.Instance, store.All);

        Assert.Null(caught["F1"].For(42));
        Assert.NotNull(caught["F1"].For(51));
    }

    [Fact]
    public void AShortGapWalksTheFloorAndNotTheHistory()
    {
        var journals = new List<string>();

        for (var day = 1; day < 60; day++)
        {
            journals.Add(Journal($"2026-01-{day:00}T100000"));
        }

        var recent = LoadoutBackfill.Window(journals, new DateTimeOffset(2026, 2, 27, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(25, recent.Count);

        // Nothing said at all — a first run, or a file somebody deleted — is the same floor.
        Assert.Equal(25, LoadoutBackfill.Window(journals, since: null).Count);

        // And a stamp older than every file walks all of them rather than guessing.
        Assert.Equal(
            journals.Count,
            LoadoutBackfill.Window(journals, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)).Count);
    }

    /// <summary>One file before the cutoff, not the cutoff itself.</summary>
    [Fact]
    public void TheWalkStartsOneFileBeforeTheStamp()
    {
        var journals = new List<string>();

        for (var day = 1; day < 60; day++)
        {
            journals.Add(Journal($"2026-01-{day:00}T100000"));
        }

        // The stamp lands on day 20, five hours after that day's session opened — so the event that produced
        // it is in day 20's file even though day 21's is the first named at or after it.
        var window = LoadoutBackfill.Window(
            journals, new DateTimeOffset(2026, 1, 20, 15, 0, 0, TimeSpan.Zero));

        Assert.Equal(journals[19], window[0]);
    }

    /// <summary>
    /// A rescan rebuilds rather than catches up, which is the whole difference between it and the walk
 /// at startup.
    /// </summary>
    [Fact]
    public void ARescanDropsAShipNothingInTheJournalsSupports()
    {
        // A file holding a ship no journal will mention — the state a Commander presses the button because
        // of.
        var invented = State(
            store: null,
            Boarding("2026-01-02T01:00:00Z", "type9_military", 99, "int_engine_size7_class5"));

        var store = Store();
        store.Save([invented], Folded);

        Journal("2026-01-03T100000",
            """{"timestamp":"2026-01-03T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            Boarding("2026-01-03T10:05:00Z", "anaconda", 51, "int_engine_size7_class2"));

        var found = LoadoutBackfill.Rescan(_root, NullLogger.Instance);

        Assert.Equal(1, found.Files);
        Assert.Equal(1, found.Ships);

        // The invented ship is gone and the real one is there — where a catch-up, being seeded with the file,
        // would have kept both.
        Assert.Null(found.ByCommander["F1"].For(99));
        Assert.NotNull(found.ByCommander["F1"].For(51));
    }

    /// <summary>
    /// A rescan that read no journals reports nought files, and that is the number the caller has to
    /// act on rather than the ship count.
    /// </summary>
    [Fact]
    public void ARescanOfAFolderWithNoJournalsFindsNothingAndSaysSo()
    {
        Assert.Equal(0, LoadoutBackfill.Rescan(_root, NullLogger.Instance).Files);

        Assert.Equal(
            0,
            LoadoutBackfill.Rescan(Path.Combine(_root, "not-there"), NullLogger.Instance).Files);
    }

    /// <summary>A rescan replaces every known Commander, including one it found nothing for.</summary>
    [Fact]
    public void ReplacingLoadoutsEmptiesACommanderTheRescanFoundNothingFor()
    {
        var gameState = new GameStateStore();

        gameState.Apply(Event(
            """{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        gameState.Apply(Event(Boarding("2026-08-20T01:00:00Z", "anaconda", 51, "a")));

        Assert.NotNull(gameState.Active!.Loadouts.For(51));

        gameState.ReplaceLoadouts(new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal));

        Assert.Null(gameState.Active.Loadouts.For(51));

        // And a Commander the rescan found but this store has never seen is not invented: a bucket exists
        // because a journal established an identity.
        gameState.ReplaceLoadouts(new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal)
        {
            ["F2"] = ShipLoadouts.Empty,
        });

        Assert.Single(gameState.All);
    }

    /// <summary>The bar moves and it finishes at one.</summary>
    [Fact]
    public void ARescanReportsHowFarItHasGot()
    {
        for (var day = 1; day < 6; day++)
        {
            Journal(
                $"2026-01-{day:00}T100000",
                $$"""{"timestamp":"2026-01-{{day:00}}T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        }

        var seen = new List<double>();

        LoadoutBackfill.Rescan(_root, NullLogger.Instance, new Steps(seen));

        Assert.Equal(0, seen[0]);
        Assert.Equal(1, seen[^1]);

        for (var nth = 1; nth < seen.Count; nth++)
        {
            Assert.True(seen[nth] >= seen[nth - 1], $"step {nth} went backwards to {seen[nth]}");
        }
    }

    /// <summary>Collects what it is told, in order.</summary>
    private sealed class Steps(List<double> seen) : IProgress<double>
    {
        public void Report(double value) => seen.Add(value);
    }

    /// <summary>Engineering updates the stored loadout with no <c>Loadout</c> event involved.</summary>
    [Fact]
    public void EngineeringAModuleReachesTheFileWithNoLoadoutBehindIt()
    {
        var state = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "anaconda", 51, "int_engine_size7_class5"),
            """
            {"timestamp":"2026-08-20T04:00:00Z","event":"EngineerCraft","Slot":"MainEngines",
             "BlueprintName":"Engine_Tuned","Level":3,"Quality":0.71,"Engineer":"Felicity Farseer",
             "Modifiers":[{"Label":"EngineOptimalMass","Value":61.0,"OriginalValue":45.0,"LessIsGood":0}]}
            """);

        var store = Store();
        store.Save([state], Folded);

        var reading = Store();
        reading.Load();

        var module = Assert.Single(reading.For("F1")!.For(51)!.Loadout.Modules);

        Assert.Equal("Engine_Tuned", module.Blueprint);
        Assert.Equal(3, module.BlueprintLevel);
        Assert.Equal(61.0, Assert.Single(module.Modifiers).Value);

        // And the event kind is one the host asks about before writing, which is the same list said once
        // rather than restated wherever a save is decided.
        Assert.True(ShipLoadouts.MayChange(Event(
            """{"timestamp":"2026-08-20T04:00:00Z","event":"EngineerCraft","Slot":"MainEngines"}""")));
    }

    /// <summary>A fleet snapshot deletes nothing: <c>StoredShips</c> does not list the flown ship, so treating it as the fleet would drop whatever the Commander is sitting in.</summary>
    [Fact]
    public void AFleetSnapshotNeverDeletesARememberedShip()
    {
        var state = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "type9_military", 53, "int_engine_size7_class5"),
            Boarding("2026-08-20T02:00:00Z", "anaconda", 51, "int_engine_size7_class2"),

            // A snapshot naming neither: the anaconda because it is being flown, the Type 9 because Elite
            // left it out — which the corpus shows it does.
            """
            {"timestamp":"2026-08-20T03:00:00Z","event":"StoredShips","StationName":"Jameson Memorial",
             "StarSystem":"Shinrarta Dezhra","ShipsHere":[],"ShipsRemote":[]}
            """);

        Assert.NotNull(state.Loadouts.For(53));
        Assert.NotNull(state.Loadouts.For(51));
    }

    /// <summary>A second Commander who has not flown this session keeps their ships.</summary>
    [Fact]
    public void ACommanderWhoDidNotFlyTodayIsNotErasedByASave()
    {
        var first = State(store: null, Boarding("2026-08-20T01:00:00Z", "anaconda", 51, "a"));

        var store = Store();
        store.Save([first], Folded);

        var other = new GameStateStore();
        other.Apply(Event("""{"timestamp":"2026-08-21T00:00:00Z","event":"Commander","FID":"F2","Name":"Braben"}"""));
        other.Apply(Event(Boarding("2026-08-21T01:00:00Z", "python", 7, "b")));

        store.Save([other.Active!], Folded);

        var reading = Store();
        reading.Load();

        Assert.NotNull(reading.For("F1")!.For(51));
        Assert.NotNull(reading.For("F2")!.For(7));

        // And neither was handed the other's ship, which is what keying on the Frontier id is for.
        Assert.Null(reading.For("F1")!.For(7));
        Assert.Null(reading.For("F2")!.For(51));
    }

    /// <summary>Deleting the file loses nothing the journals can still supply.</summary>
    [Fact]
    public void AMissingOrUnreadableFileRebuildsRatherThanFailing()
    {
        var reading = Store();
        reading.Load();

        Assert.Null(reading.For("F1"));

        System.IO.File.WriteAllText(File_, "{ this is not json");

        var corrupt = Store();
        corrupt.Load();

        Assert.Null(corrupt.For("F1"));

        // And the journals put it back.
        var journal = Path.Combine(_root, "Journal.2026-08-21T100000.01.log");

        System.IO.File.WriteAllLines(journal,
        [
            """{"timestamp":"2026-08-21T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            Boarding("2026-08-21T10:05:00Z", "anaconda", 51, "int_engine_size7_class2").ReplaceLineEndings(" "),
        ]);

        var rebuilt = LoadoutBackfill.FromHistory([journal], NullLogger.Instance, corrupt.All);

        Assert.NotNull(rebuilt["F1"].For(51));
    }

    /// <summary>
    /// Every field that is set rather than computed survives the round trip, checked by reflection
    /// rather than by anybody remembering to update a mapping.
    /// </summary>
    [Theory]
    [InlineData(typeof(ShipLoadout))]
    [InlineData(typeof(ShipModule))]
    [InlineData(typeof(ShipModifier))]
    public void EveryPersistableFieldIsCarried(Type type)
    {
        var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .ToList();

        Assert.NotEmpty(settable);

        var flown = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "anaconda", 51, "int_engine_size7_class5"));

        var store = Store();
        store.Save([flown], Folded);

        var reading = Store();
        reading.Load();

        var before = flown.Loadouts.For(51)!.Loadout;
        var after = reading.For("F1")!.For(51)!.Loadout;

        foreach (var name in settable)
        {
            var (left, right) = type == typeof(ShipLoadout)
                ? (Read(before, name), Read(after, name))
                : type == typeof(ShipModule)
                    ? (Read(before.Modules[0], name), Read(after.Modules[0], name))
                    : (Read(before.Modules[0].Modifiers[0], name), Read(after.Modules[0].Modifiers[0], name));

            // Lists are compared by count, because what this is guarding against is a field the store never
            // learned about — which arrives as null or empty on the far side.
            if (left is System.Collections.IEnumerable and not string)
            {
                Assert.Equal(Count(left), Count(right));
                continue;
            }

            Assert.Equal(left, right);
        }

        static object? Read(object target, string name) =>
            target.GetType().GetProperty(name)!.GetValue(target);

        static int Count(object? value) =>
            value is System.Collections.IEnumerable items ? items.Cast<object>().Count() : 0;
    }

    /// <summary>
    /// The fixture the guard above rests on: the ship it round-trips actually carries something in
    /// every shape a real one has — a hull, a name, a figure, an engineered module and a modifier.
    /// </summary>
    [Fact]
    public void TheRoundTrippedShipIsAFullOne()
    {
        var flown = State(
            store: null,
            Boarding("2026-08-20T01:00:00Z", "anaconda", 51, "int_engine_size7_class5"));

        var ship = flown.Loadouts.For(51)!.Loadout;

        Assert.Equal("anaconda", ship.Type);
        Assert.Equal("Kestrel", ship.Name);
        Assert.Equal(42.5, ship.MaxJumpRange);

        var module = Assert.Single(ship.Modules);

        Assert.Equal("Engine_Dirty", module.Blueprint);
        Assert.Equal("Felicity Farseer", module.Engineer);
        Assert.Equal(52.3, Assert.Single(module.Modifiers).Value);
    }
}
