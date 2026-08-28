using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// A core per ship (Phase 35). The two halves are tested apart because they are two
/// different promises: the file holds what the Commander said, and the watch decides when what
/// they said acts.
/// </summary>
public class ShipCoreStoreTests
{
    [Fact]
    public void ABindingSurvivesBeingWrittenAndReadBack()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Bind(new ShipCoreBinding("F1", 12, "sentinel", "python", "Bad Idea"));

        var reopened = Store(folder);
        reopened.Poll();

        var binding = Assert.Single(reopened.Bindings);
        Assert.Equal("F1", binding.CommanderFid);
        Assert.Equal(12, binding.ShipId);
        Assert.Equal("sentinel", binding.Core);

        // Written for a person reading the file, and read back so a hand edit of them is not
        // silently reverted the next time anything is bound.
        Assert.Equal("python", binding.Hull);
        Assert.Equal("Bad Idea", binding.Name);
    }

    [Fact]
    public void OneCorePerShip()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Bind(new ShipCoreBinding("F1", 12, "sentinel"));
        store.Bind(new ShipCoreBinding("F1", 12, "quartermaster"));

        var binding = Assert.Single(store.Bindings);
        Assert.Equal("quartermaster", binding.Core);
    }

    /// <summary>
    /// The Commander is half the key (the Phase 44 defect item): Elite's ship ids are per
    /// Commander and start small, so two Commanders' ship 12s are two ships — each binds their
    /// own, and each reads back their own.
    /// </summary>
    [Fact]
    public void TwoCommandersMayEachBindTheSameShipId()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Bind(new ShipCoreBinding("F1", 12, "sentinel"));
        store.Bind(new ShipCoreBinding("F2", 12, "quartermaster"));

        var reopened = Store(folder);
        reopened.Poll();

        Assert.Equal(2, reopened.Bindings.Count);
        Assert.Empty(reopened.Problems);
        Assert.Equal("sentinel", reopened.For("F1", 12)!.Core);
        Assert.Equal("quartermaster", reopened.For("F2", 12)!.Core);

        // And forgetting one Commander's ship 12 leaves the other's alone.
        Assert.True(reopened.Forget("F1", 12));
        Assert.Null(reopened.For("F1", 12));
        Assert.Equal("quartermaster", reopened.For("F2", 12)!.Core);
    }

    /// <summary>
    /// A file from before bindings carried a Commander: claimed whole by the first one seen, the
    /// way the checklist adopts unowned notes — a release must not silently unbind every core.
    /// </summary>
    [Fact]
    public void ALegacyFileIsAdoptedByTheFirstCommanderSeen()
    {
        using var folder = new TempFolder();

        File.WriteAllText(folder.File, """
            {
              "ships": [
                { "shipId": 12, "core": "sentinel", "hull": "python", "name": "Bad Idea" }
              ]
            }
            """);

        var store = Store(folder);
        store.Poll();

        Assert.Null(store.For("F1", 12));
        Assert.True(store.Adopt("F1", "Jameson"));
        Assert.Equal("sentinel", store.For("F1", 12)!.Core);

        // Written through, not merely held: the claim survives a restart.
        var reopened = Store(folder);
        reopened.Poll();

        Assert.Equal("F1", Assert.Single(reopened.Bindings).CommanderFid);
        Assert.False(reopened.Adopt("F1"));
    }

    [Fact]
    public void ForgettingLeavesTheOtherShipsAlone()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Bind(new ShipCoreBinding("F1", 12, "sentinel"));
        store.Bind(new ShipCoreBinding("F1", 27, "quartermaster"));

        Assert.True(store.Forget("F1", 12));
        Assert.False(store.Forget("F1", 12));

        Assert.Equal([27], store.Bindings.Select(binding => binding.ShipId));
    }

    /// <summary>
    /// A hand edit is the route this file is meant to be usable by, so a bad line is reported and
    /// the rest still loads — one typo must not cost somebody the other five ships they bound.
    /// </summary>
    [Fact]
    public void ABadLineIsReportedAndTheRestOfTheFileStillLoads()
    {
        using var folder = new TempFolder();

        File.WriteAllText(folder.File, """
            {
              "ships": [
                { "shipId": 12, "core": "sentinel" },
                { "shipId": 13, "core": "not-a-core" },
                { "shipId": 12, "core": "kex" },
                { "core": "warden" },
                { "shipId": 27, "core": "quartermaster" }
              ]
            }
            """);

        var store = Store(folder);
        store.Poll();

        Assert.Equal([12, 27], store.Bindings.Select(binding => binding.ShipId));
        Assert.Equal(3, store.Problems.Count);

        // A core d47 does not ship is refused rather than resolved to the default, which is the
        // one place this differs from the settings row: resolving would put a core the Commander
        // never chose aboard a ship they did, silently, every time they boarded it.
        Assert.Contains(store.Problems, problem => problem.Reason.Contains("not-a-core", StringComparison.Ordinal));
    }

    /// <summary>
    /// Polled by content rather than by a file-system stamp, so an edit made while d47 is running
    /// is live without a restart — and two writes inside one 15.6 ms clock tick are not one.
    /// </summary>
    [Fact]
    public void AnEditWhileRunningIsPickedUp()
    {
        using var folder = new TempFolder();
        var store = Store(folder);
        var changes = 0;
        store.Changed += () => changes++;

        store.Bind(new ShipCoreBinding("F1", 12, "sentinel"));
        Assert.False(store.Poll());

        File.WriteAllText(
            folder.File,
            """{ "ships": [ { "commanderFid": "F1", "shipId": 12, "core": "kex" } ] }""");

        Assert.True(store.Poll());
        Assert.Equal("kex", store.For("F1", 12)!.Core);
        Assert.Equal(2, changes);
    }

    private static ShipCoreStore Store(TempFolder folder) =>
        new(folder.File, NullLogger<ShipCoreStore>.Instance);

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), $"d47-ship-cores-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string File => Path.Combine(Root, "ship-cores.json");

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A temp folder that outlives the test costs nothing and is not what is being asserted.
            }
        }
    }
}

/// <summary>
/// When a binding acts (Phase 35, "Switching ships switches the core"). Every question
/// here is about timing, and every answer is measured in elapsed time handed in — no component
/// tested here owns a thread or reads a clock.
/// </summary>
public class ShipCoreWatchTests
{
    private static readonly TimeSpan Settle = ShipCoreService.DefaultSettle;

    /// <summary>
    /// The ship d47 finds the Commander already in. Applied, because the ship remembers its core;
    /// silent, because launching the app is not a ship change they just made.
    /// </summary>
    [Fact]
    public void TheShipAlreadyBeingFlownIsAdoptedSilently()
    {
        var state = State(12);
        var service = Service(state, ("F1", 12, "sentinel"));

        var due = service.Observe(TimeSpan.Zero);

        Assert.NotNull(due);
        Assert.Equal("sentinel", due!.Core);
        Assert.False(due.Announce);
    }

    [Fact]
    public void AShipWithNoBindingChangesNothing()
    {
        var state = State(12);
        var service = Service(state, ("F1", 27, "sentinel"));

        Assert.Null(service.Observe(TimeSpan.Zero));

        // And boarding it later is still nothing: a ship nobody bound leaves whoever is aboard
        // aboard, which is the whole of "nothing is bound until it is asked for".
        Board(state, 99);
        Assert.Null(Settled(service));
    }

    [Fact]
    public void BoardingABoundShipSwitchesOnceTheChangeHasSettled()
    {
        var state = State(12);
        var service = Service(state, ("F1", 12, "sentinel"), ("F1", 27, "quartermaster"));

        service.Observe(TimeSpan.Zero);
        Board(state, 27);

        // Nothing yet. A Commander who is going to be in three more ships in the next minute has
        // not chosen this one.
        Assert.Null(service.Observe(TimeSpan.FromSeconds(1)));
        Assert.Null(service.Observe(Settle - TimeSpan.FromSeconds(2)));

        var due = service.Observe(TimeSpan.FromSeconds(2));

        Assert.NotNull(due);
        Assert.Equal("quartermaster", due!.Core);
        Assert.True(due.Announce);
    }

    /// <summary>
    /// The shipyard shuffle, which is the case the settle window exists for: five ships in five
    /// minutes is one switch, for the ship they were actually still in.
    /// </summary>
    [Fact]
    public void ShufflingThroughShipsCostsOneSwitchForTheLastOne()
    {
        var state = State(12);
        var service = Service(state, ("F1", 12, "sentinel"), ("F1", 27, "quartermaster"), ("F1", 33, "kex"));

        service.Observe(TimeSpan.Zero);

        var switches = new List<ShipCoreSwitch>();

        foreach (var ship in new[] { 27, 33, 27, 33 })
        {
            Board(state, ship);

            for (var i = 0; i < 4; i++)
            {
                if (service.Observe(TimeSpan.FromSeconds(5)) is { } due)
                {
                    switches.Add(due);
                }
            }
        }

        Assert.Empty(switches);

        // They stop shuffling and stay in the Krait.
        var settled = Settled(service);

        Assert.NotNull(settled);
        Assert.Equal("kex", settled!.Core);
    }

    /// <summary>
    /// Ending up back where they started is not a change. Without this, a Commander who looked
    /// inside another ship and climbed back out would get their own core announced at them.
    /// </summary>
    [Fact]
    public void ComingBackToTheShipAboardCancelsTheSwitch()
    {
        var state = State(12);
        var service = Service(state, ("F1", 12, "sentinel"), ("F1", 27, "quartermaster"));

        service.Observe(TimeSpan.Zero);

        Board(state, 27);
        service.Observe(TimeSpan.FromSeconds(5));

        Board(state, 12);
        Assert.Null(service.Observe(TimeSpan.FromSeconds(5)));
        Assert.Null(Settled(service));
    }

    /// <summary>
    /// Binding is an act and applying is a consequence: binding the ship they are sitting in must
    /// not then read as an arrival and announce the core that is already aboard.
    /// </summary>
    [Fact]
    public void BindingTheShipTheyAreInDoesNotThenSwitchToIt()
    {
        var state = State(12);
        var service = Service(state);

        var said = service.Bind("sentinel");

        Assert.Contains("Sentinel", said, StringComparison.Ordinal);
        Assert.Equal("sentinel", service.For(12)!.Core);
        Assert.Null(Settled(service));
    }

    /// <summary>
    /// The bug the key exists for (the Phase 44 defect item): another Commander's ship 12
    /// is a different ship, so their binding must not put a core aboard this Commander's.
    /// </summary>
    [Fact]
    public void AnotherCommandersBindingForTheSameShipIdDoesNotAnswer()
    {
        var state = State(12);
        var service = Service(state, ("F2", 12, "quartermaster"));

        Assert.Null(service.Observe(TimeSpan.Zero));
        Assert.Null(Settled(service));
        Assert.Null(service.Current);
    }

    /// <summary>
    /// A file from before bindings carried a Commander still works on the release that added the
    /// key: the first Commander seen claims it, and their core arrives as it always did.
    /// </summary>
    [Fact]
    public void ALegacyBindingIsAdoptedAndStillAnswers()
    {
        var state = State(12);
        var service = Service(state, ("", 12, "sentinel"));

        var due = service.Observe(TimeSpan.Zero);

        Assert.NotNull(due);
        Assert.Equal("sentinel", due!.Core);
        Assert.Equal("F1", service.Store.Bindings.Single().CommanderFid);
    }

    [Fact]
    public void NothingHappensBeforeTheJournalHasSaidWhichShip()
    {
        var service = Service(new GameStateStore());

        Assert.Null(Settled(service));
        Assert.Contains("do not know which ship", service.Bind("sentinel"), StringComparison.Ordinal);
        Assert.Empty(service.Store.Bindings);
    }

    [Fact]
    public void ForgettingSaysSoWhetherOrNotThereWasAnything()
    {
        var state = State(12);
        var service = Service(state, ("F1", 12, "sentinel"));

        Assert.Contains("Sentinel", service.Forget(), StringComparison.Ordinal);
        Assert.Contains("was not bound", service.Forget(), StringComparison.Ordinal);
    }

    /// <summary>Runs a whole settle window through, in the ticks the app would.</summary>
    private static ShipCoreSwitch? Settled(ShipCoreService service)
    {
        ShipCoreSwitch? due = null;

        for (var waited = TimeSpan.Zero; waited <= Settle + TimeSpan.FromSeconds(1); waited += TimeSpan.FromSeconds(1))
        {
            due ??= service.Observe(TimeSpan.FromSeconds(1));
        }

        return due;
    }

    /// <summary>
    /// The live half of the Phase 44 defect. The store is keyed per Commander, but the
    /// watch remembers the ship it acted on as a bare id — so a second Commander logging in
    /// aboard their own ship 7 reads as no change, and the first one's core stays aboard. A reset
    /// on the switch makes the next observation the ship d47 found them in, adopted silently.
    /// </summary>
    [Fact]
    public void AResetAdoptsTheNewCommandersShipAndItsOwnCoreSilently()
    {
        var state = State(7);
        var service = Service(state, ("F1", 7, "sentinel"), ("F2", 7, "quartermaster"));

        var first = service.Observe(TimeSpan.FromSeconds(1));
        Assert.NotNull(first);
        Assert.Equal("sentinel", first.Core);
        Assert.False(first.Announce);

        // Bob logs in, also in ship 7.
        state.Apply(Event("""{"timestamp":"2026-08-19T10:00:00Z","event":"Commander","FID":"F2","Name":"Bob"}"""));
        Board(state, 7);

        // Same id, so without the reset this is "no change" — the defect.
        Assert.Null(service.Observe(TimeSpan.FromSeconds(1)));

        service.Reset();

        var adopted = service.Observe(TimeSpan.FromSeconds(1));
        Assert.NotNull(adopted);
        Assert.Equal("quartermaster", adopted.Core);

        // Silent, as the ship d47 found them in: a login is not a ship change they just made, and
        // the greeting is the new core's first words.
        Assert.False(adopted.Announce);

        // And it stays put: the ship they are in is the one acted on, so nothing is due next tick.
        Assert.Null(service.Observe(TimeSpan.FromSeconds(1)));
    }

    private static ShipCoreService Service(
        GameStateStore state,
        params (string Fid, int Ship, string Core)[] bound)
    {
        var store = new ShipCoreStore(
            Path.Combine(Path.GetTempPath(), $"d47-ship-cores-{Guid.NewGuid():N}.json"),
            NullLogger<ShipCoreStore>.Instance);

        foreach (var (fid, ship, core) in bound)
        {
            store.Bind(new ShipCoreBinding(fid, ship, core));
        }

        return new ShipCoreService(store, () => state.Active);
    }

    private static GameStateStore State(int shipId)
    {
        var store = new GameStateStore();

        store.Apply(Event("""{"timestamp":"2026-08-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        Board(store, shipId);

        return store;
    }

    private static void Board(GameStateStore store, int shipId) =>
        store.Apply(Event(
            $$"""{"timestamp":"2026-08-19T09:00:00Z","event":"Loadout","Ship":"python","ShipID":{{shipId}},"ShipName":"Bad Idea","Modules":[]}"""));

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
