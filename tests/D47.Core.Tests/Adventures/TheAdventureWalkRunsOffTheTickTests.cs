using D47.Core.Adventures;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A walk reads up to the tail's mark on the pool, and live events after the mark are held until it is adopted.</summary>
public sealed class TheAdventureWalkRunsOffTheTickTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-walk", Guid.NewGuid().ToString("N"));

    private readonly string _file;
    private readonly IReadOnlyList<JournalEvent> _route = WholeRoute(Accepted);

    public TheAdventureWalkRunsOffTheTickTests()
    {
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, "Journal.2026-08-22T194000.01.log");
        Append(Commander("F1", Accepted.AddMinutes(-1)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>A book whose story was just begun again, so a walk is owed.</summary>
    private AdventureBook Moved()
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted.AddDays(-1)));
        book.CatchUp([]);

        book.Store.Save("F1", LanternRoute(Accepted));
        book.Reconcile();

        Assert.True(book.NeedsCatchUp);
        return book;
    }

    private void Append(JournalEvent journalEvent) => File.AppendAllLines(_file, [journalEvent.Raw.GetRawText()]);

    private JournalMark Tail() => new(_file, new FileInfo(_file).Length);

    private static AdventureStanding Lantern(AdventureBook book) => book.Standing("F1", "the-lantern-route")!;

    [Fact]
    public void AnEventAfterTheMarkIsFoldedOnceAndItsBeatSaidOnce()
    {
        var book = Moved();
        Append(_route[0]);

        var walk = book.StartWalk(_folder, Tail())!;

        // Written and observed after the mark: the walk must not read it, and the book holds it.
        Append(_route[1]);
        book.Observe(_route[1], "F1");
        Assert.Empty(Lantern(book).Fired);

        book.Adopt(walk.Run());

        Assert.Equal(2, Lantern(book).Fired.Count);
        Assert.Equal([1], book.Drain().Select(moment => moment.Beat));
        Assert.Empty(book.Drain());
        Assert.True(book.IsStirring("F1", "the-lantern-route"));
    }

    [Fact]
    public void AnEventObservedBeforeTheWalkStartsIsCountedOnce()
    {
        var book = Moved();
        Append(_route[0]);
        book.Observe(_route[0], "F1");
        book.Drain();

        book.Adopt(book.StartWalk(_folder, Tail())!.Run());

        Assert.Single(Lantern(book).Fired);

        // And the book folds live events again.
        book.Observe(_route[1], "F1");

        Assert.Equal(2, Lantern(book).Fired.Count);
        Assert.Equal([1], book.Drain().Select(moment => moment.Beat));
    }

    [Fact]
    public void AStampMovedDuringAWalkDiscardsItAndAnotherWalkFollows()
    {
        var book = Moved();
        Append(_route[0]);

        var first = book.StartWalk(_folder, Tail())!;
        Assert.Null(book.StartWalk(_folder, Tail()));

        book.Store.Save("F1", LanternRoute(Accepted.AddSeconds(30)));
        book.Reconcile();

        book.Adopt(first.Run());

        // Discarded: the standings are as they were, and live events are still held.
        Assert.Empty(Lantern(book).Fired);
        Append(_route[1]);
        book.Observe(_route[1], "F1");
        Assert.Empty(Lantern(book).Fired);

        // The next walk's mark is past the held event, so the walk counts it.
        var second = book.StartWalk(_folder, Tail());
        Assert.NotNull(second);

        book.Adopt(second.Run());

        Assert.Equal(2, Lantern(book).Fired.Count);
        Assert.Empty(book.Drain());
    }

    [Fact]
    public void AFileStartedAfterTheMarkDoesNotHideTheMarkedOne()
    {
        var newer = Path.Combine(_folder, "Journal.2026-08-22T200000.01.log");
        File.WriteAllText(newer, string.Empty);

        // Accepted after both sessions started: without the mark, only the newest file is walked.
        var acceptance = new DateTimeOffset(2026, 8, 22, 21, 0, 0, TimeSpan.Zero);

        Assert.Equal([newer], AdventureBook.FilesToWalk(_folder, acceptance));
        Assert.Equal([_file], AdventureBook.FilesToWalk(_folder, acceptance, Tail()));
    }

    [Fact]
    public void AFailedWalkFoldsWhatItHeldAndGoesBackToFoldingLive()
    {
        var book = Moved();
        Append(_route[0]);

        var walk = book.StartWalk(_folder, Tail())!;
        book.Observe(_route[0], "F1");

        AdventureWalkResult result;

        using (new FileStream(_file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = walk.Run();
        }

        Assert.True(result.Failed);

        book.Adopt(result);

        Assert.Single(Lantern(book).Fired);
        Assert.Equal([0], book.Drain().Select(moment => moment.Beat));
        Assert.False(book.NeedsCatchUp);

        book.Observe(_route[1], "F1");

        Assert.Equal(2, Lantern(book).Fired.Count);
        Assert.Equal([1], book.Drain().Select(moment => moment.Beat));
    }
}
