using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// One fold, two callers (list.md Phase 47). The catch-up over files on disk and the live tick
/// give the same standing for the same events, and the priming replay cannot count a beat twice.
/// </summary>
public class AdventureBookTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-book", Guid.NewGuid().ToString("N"));

    public AdventureBookTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureBook Book()
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        return new AdventureBook(store, NullLogger<AdventureBook>.Instance);
    }

    private string Journal(string name, params D47.Core.Journal.JournalEvent[] events)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllLines(path, events.Select(e => e.Raw.GetRawText()));
        return path;
    }

    [Fact]
    public void BeginStampsAndQueuesTheOpening()
    {
        var book = Book();
        book.Write("F1", LanternRoute());

        Assert.Null(book.Begin("F1", "the-lantern-route", Accepted));

        var stored = Assert.Single(book.Store.For("F1"));
        Assert.Equal(Accepted, stored.AcceptedAt);

        var moment = Assert.Single(book.Drain());
        Assert.True(moment.IsOpening);
        Assert.Equal("Beacons cost money. Somebody is paying.", moment.Line);
        Assert.Empty(book.Drain());
    }

    [Fact]
    public void BeginRefusesAnUnresolvedBeatByName()
    {
        var book = Book();
        book.Write("F1", LanternRoute() with
        {
            Beats = [Beat("Somewhere", null, new AdventureTrigger { Kind = TriggerKind.Arrive, System = "Nowhere Yet" }, "L")],
        });

        var refusal = book.Begin("F1", "the-lantern-route", Accepted);

        Assert.Contains("Nowhere Yet", refusal);
        Assert.Null(Assert.Single(book.Store.For("F1")).AcceptedAt);
    }

    [Fact]
    public void TheLivePathAdvancesAndQueuesEachBeat()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));

        foreach (var journalEvent in WholeRoute(Accepted))
        {
            book.Observe(journalEvent, "F1");
        }

        var moments = book.Drain();

        Assert.Equal([0, 1, 2, 3, 4], moments.Select(m => m.Beat));
        Assert.True(book.Standing("F1", "the-lantern-route")!.IsDone);
    }

    [Fact]
    public void TheCatchUpFindsBeatsThatFiredWhileD47WasClosed()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));

        var route = WholeRoute(Accepted);
        var file = Journal("Journal.2026-08-22T194000.01.log", [Commander("F1", Accepted.AddMinutes(-1)), .. route.Take(3)]);

        book.CatchUp([file]);

        var standing = book.Standing("F1", "the-lantern-route")!;

        Assert.Equal(3, standing.Fired.Count);
        Assert.Equal("Veyl 3 c", standing.Place());

        // In the past: nothing is owed to the callout.
        Assert.Empty(book.Drain());
        Assert.False(book.NeedsCatchUp);
    }

    [Fact]
    public void ThePrimingReplayCannotCountABeatTwice()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));

        var route = WholeRoute(Accepted);
        var file = Journal("Journal.2026-08-22T194000.01.log", [Commander("F1", Accepted.AddMinutes(-1)), .. route.Take(2)]);

        book.CatchUp([file]);

        // The tick loop replays the same file from its start, then sees the rest live.
        foreach (var journalEvent in route)
        {
            book.Observe(journalEvent, "F1");
        }

        var standing = book.Standing("F1", "the-lantern-route")!;

        Assert.Equal(5, standing.Fired.Count);
        Assert.Equal([2, 3, 4], book.Drain().Select(m => m.Beat));
    }

    [Fact]
    public void TheCatchUpKeysOnWhoeverTheJournalSaysIsFlying()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));
        book.Write("F2", LanternRoute(Accepted));

        var file = Journal(
            "Journal.2026-08-22T194000.01.log",
            Commander("F2", Accepted.AddMinutes(-1)),
            Jump(Lantern, Accepted.AddMinutes(1)));

        book.CatchUp([file]);

        Assert.Empty(book.Standing("F1", "the-lantern-route")!.Fired);
        Assert.Single(book.Standing("F2", "the-lantern-route")!.Fired);
    }

    [Fact]
    public void AbandonStopsTheFoldAndDropsAWaitingBeat()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));

        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");

        Assert.Null(book.Abandon("F1", "the-lantern-route", Accepted.AddMinutes(2)));
        Assert.Empty(book.Drain());

        book.Observe(Scan(QuietField, 6, Accepted.AddMinutes(3)), "F1");

        var standing = book.Standing("F1", "the-lantern-route")!;

        Assert.Single(standing.Fired);
        Assert.Equal("abandoned at The Survey", standing.Place());
        Assert.Empty(book.Active("F1"));
    }

    [Fact]
    public void BeginAgainStartsFromTheOpening()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));
        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");
        book.Abandon("F1", "the-lantern-route", Accepted.AddMinutes(2));
        book.Drain();

        var again = Accepted.AddDays(1);

        Assert.Null(book.Begin("F1", "the-lantern-route", again));

        var stored = Assert.Single(book.Store.For("F1"));
        Assert.Equal(again, stored.AcceptedAt);
        Assert.Null(stored.AbandonedAt);
        Assert.Empty(book.Standing("F1", "the-lantern-route")!.Fired);
        Assert.True(Assert.Single(book.Drain()).IsOpening);
    }

    [Fact]
    public void ReconcileKeepsAStandingWhoseStampIsUnchangedAndAsksForAWalkWhenItMoved()
    {
        var book = Book();
        book.Write("F1", LanternRoute(Accepted));
        book.CatchUp([]);
        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");

        // A hand edit to a line: the stamp is the same, the progress survives.
        book.Store.Save("F1", LanternRoute(Accepted) with { Name = "Edited" });
        book.Reconcile();

        Assert.Single(book.Standing("F1", "the-lantern-route")!.Fired);
        Assert.False(book.NeedsCatchUp);

        // The stamp moved: the standing is gone and a walk is owed.
        book.Store.Save("F1", LanternRoute(Accepted.AddDays(1)));
        book.Reconcile();

        Assert.Empty(book.Standing("F1", "the-lantern-route")!.Fired);
        Assert.True(book.NeedsCatchUp);
    }

    [Fact]
    public void FilesToWalkStartsOneSessionBeforeTheAcceptance()
    {
        foreach (var name in new[]
                 {
                     "Journal.2026-08-20T100000.01.log",
                     "Journal.2026-08-21T100000.01.log",
                     "Journal.2026-08-22T100000.01.log",
                     "Journal.2026-08-23T100000.01.log",
                 })
        {
            File.WriteAllText(Path.Combine(_folder, name), string.Empty);
        }

        var files = AdventureBook.FilesToWalk(_folder, new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            ["Journal.2026-08-22T100000.01.log", "Journal.2026-08-23T100000.01.log"],
            files.Select(Path.GetFileName));

        Assert.Empty(AdventureBook.FilesToWalk(_folder, null));
    }
}
