using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>#908: a rewrite's file write and change handlers do not hold the lock the tick takes.</summary>
public sealed class ARewriteSavesOutsideTheBooksLockTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-rewrite", Guid.NewGuid().ToString("N"));

    public ARewriteSavesOutsideTheBooksLockTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureBook Book()
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));
        return book;
    }

    private static IReadOnlyList<AdventureBeat> Rewrite(string line) =>
        [.. LanternRoute().Beats.Select(beat => beat with { Line = line })];

    [Fact]
    public void AnotherThreadCanTakeTheBooksLockWhileARewriteSaves()
    {
        var book = Book();
        bool? reachable = null;

        book.Store.Changed += () =>
        {
            // Every read of the book takes its lock; from another thread it blocks while the saver holds it.
            reachable = Task.Run(() => book.IsStirring("F1", "the-lantern-route"))
                .Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        };

        Assert.Null(book.ReplaceBeats("F1", "the-lantern-route", 0, Rewrite("Rewritten."), Accepted.AddHours(1)));

        Assert.True(reachable);
    }

    [Fact]
    public void TwoRewritesTogetherLeaveTheLastOneOnFile()
    {
        var book = Book();

        var results = Enumerable.Range(0, 8)
            .AsParallel()
            .Select(index => (Index: index, Refusal: book.ReplaceBeats("F1", "the-lantern-route", 0, Rewrite($"Rewrite {index}."), Accepted.AddHours(1).AddMinutes(index))))
            .ToList();

        Assert.All(results, result => Assert.Null(result.Refusal));

        var stored = Assert.Single(book.Store.For("F1"));
        var standing = Assert.Single(book.Standings("F1"));

        Assert.Equal(stored.Beats[0].Line, standing.Adventure.Beats[0].Line);
        Assert.Equal(stored.RewrittenAt, standing.Adventure.RewrittenAt);
    }
}
