using D47.Core.Journal;
using D47.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Memory;

/// <summary>
/// The only thing in Phase 31 that writes a memory nobody asked for, and the only producer of the
/// observed tier (Phase 31, "d47 observed it in the journal").
/// <para>
/// It exists because without it that tier would be an enum member reachable by nothing — the failure
/// <c>LoreArrival</c>'s own comment refuses to ship. So the assertions here are mostly about it being
/// <b>small</b>: two entries, replaced rather than appended, stamped with the journal's instant
/// rather than with a clock.
/// </para>
/// </summary>
public class MemoryObserverTests : IDisposable
{
    private static readonly DateTimeOffset Tick = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private const string Cmdr = "F1";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-observer-tests", Guid.NewGuid().ToString("N"));

    private MemoryStore Store() =>
        new(Path.Combine(_folder, "memories.json"), NullLogger<MemoryStore>.Instance);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-04-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private const string Docked =
        """
        {"timestamp":"3311-04-01T08:30:00Z","event":"Location","StarSystem":"Deciat","SystemAddress":6681123623626,
        "Docked":true,"StationName":"Farseer Inc"}
        """;

    private const string Elsewhere =
        """
        {"timestamp":"3311-04-01T09:30:00Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106}
        """;

    private const string Loadout =
        """
        {"timestamp":"3311-04-01T08:31:00Z","event":"Loadout","Ship":"krait_mkii","ShipName":"Nightjar","ShipIdent":"NJ-01",
        "ShipID":7,"Modules":[]}
        """;

    private (MemoryStore Store, MemoryBook Book, MemoryObserver Observer) Fresh()
    {
        var store = Store();
        var book = new MemoryBook(store, () => Cmdr, () => MemorySituation.Unknown);
        return (store, book, new MemoryObserver(book));
    }

    [Fact]
    public void WhereAndWhatAreWrittenDownAsObservations()
    {
        var (store, _, observer) = Fresh();

        Assert.Equal(2, observer.Observe(StateFrom(Docked, Loadout), Tick));

        var entries = store.For(Cmdr);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(MemoryTier.Observed, entry.Tier));
        Assert.All(entries, entry => Assert.Equal(MemoryArrival.Journal, entry.Arrival));

        // Read back as observations, never as the Commander's word.
        Assert.All(entries, entry => Assert.StartsWith("I noticed:", entry.Spoken(), StringComparison.Ordinal));
    }

    [Fact]
    public void TheStationIsNamedWhereThereIsOne()
    {
        var (store, _, observer) = Fresh();

        observer.Observe(StateFrom(Docked), Tick);

        var where = Assert.Single(store.For(Cmdr), e => e.Key == MemoryObserver.WhereKey);

        Assert.Contains("Deciat", where.Fact, StringComparison.Ordinal);
        Assert.Contains("Farseer Inc", where.Fact, StringComparison.Ordinal);
    }

    /// <summary>
    /// The property that keeps the file readable. A row per session would add a line every time the
    /// Commander undocked, and this is a file a person is meant to be able to open.
    /// </summary>
    [Fact]
    public void MovingReplacesTheObservationRatherThanAddingOne()
    {
        var (store, _, observer) = Fresh();

        observer.Observe(StateFrom(Docked, Loadout), Tick);
        observer.Observe(StateFrom(Docked, Loadout, Elsewhere), Tick.AddHours(1));

        Assert.Equal(2, store.For(Cmdr).Count);
        Assert.Contains(
            "Shinrarta Dezhra",
            store.For(Cmdr).Single(e => e.Key == MemoryObserver.WhereKey).Fact,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsWrittenWhenNothingChanged()
    {
        var (_, _, observer) = Fresh();
        var state = StateFrom(Docked, Loadout);

        Assert.Equal(2, observer.Observe(state, Tick));

        // This runs ten times a second. A second write on an unchanged state would rewrite the
        // file ten times a second forever.
        Assert.Equal(0, observer.Observe(state, Tick.AddSeconds(1)));
    }

    /// <summary>
    /// The stamp is what "it has been three days" is measured from, so it has to be the instant the
    /// journal reported rather than the instant d47 happened to look.
    /// </summary>
    [Fact]
    public void TheStampComesFromTheJournalRatherThanTheTick()
    {
        var (store, _, observer) = Fresh();
        var state = StateFrom(Docked);

        observer.Observe(state, Tick.AddYears(5));

        var where = Assert.Single(store.For(Cmdr), e => e.Key == MemoryObserver.WhereKey);

        Assert.Equal(state.Session.LastEventAt, where.AddedAt);
        Assert.NotEqual(Tick.AddYears(5), where.AddedAt);
    }

    [Fact]
    public void NothingIsWrittenBeforeTheJournalHasSaidAnything()
    {
        var (store, _, observer) = Fresh();

        Assert.Equal(0, observer.Observe(null, Tick));
        Assert.Empty(store.Everything());
    }

    [Fact]
    public void ObservationsAreDistinguishableFromRememberedFacts()
    {
        // The panel uses this to decide whether to offer a Forget button: an observation is
        // rewritten the moment the Commander moves, so removing one looks broken rather than
        // decisive.
        Assert.True(MemoryObserver.IsObservation(MemoryObserver.WhereKey));
        Assert.True(MemoryObserver.IsObservation(MemoryObserver.ShipKey));
        Assert.False(MemoryObserver.IsObservation("told-1"));
        Assert.False(MemoryObserver.IsObservation("noted-1"));
    }
}
