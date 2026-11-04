using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class TheWatchFileKeepsWhatItCanTests
{
    private const string WatchesFile = @"C:\d47\data\watches.json";

    private static readonly DateTimeOffset Tick = new(2026, 11, 3, 6, 40, 0, TimeSpan.Zero);

    private static WatchStore Store(MemoryFileSystem files) =>
        new(WatchesFile, files, NullLogger<WatchStore>.Instance);

    private static Watch Relics() => new(
        Guid.NewGuid(), "Relics", WatchKind.Stock,
        MarketId: 128000001, Station: "Sample Port", System: "Sample", Commodity: "Soontill Relics",
        Comparison: StockComparison.Above, Threshold: 10);

    [Fact]
    public void AWrittenFileReadsBackTheSameInAnotherStore()
    {
        var files = new MemoryFileSystem();
        var writer = Store(files);
        var stock = Relics();
        var goal = new Watch(Guid.NewGuid(), "New goals", WatchKind.NewCommunityGoal, Enabled: false);

        Assert.Null(writer.Add(stock));
        Assert.Null(writer.Add(goal));
        Assert.True(writer.MarkFired(stock.Id, Tick));

        var reader = Store(files);
        Assert.True(reader.Poll());

        Assert.Equal(
            [stock with { FiredForTick = BgsTickWatch.Format(Tick) }, goal],
            reader.Watches);
        Assert.Empty(reader.Problems);
    }

    [Fact]
    public void ChangingTheThresholdForgetsTheFiring()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);
        var watch = Relics();
        store.Add(watch);
        store.MarkFired(watch.Id, Tick);

        Assert.Null(store.Update(watch with { Threshold = 20 }));

        Assert.Null(Assert.Single(store.Watches).FiredForTick);
    }

    [Fact]
    public void RenamingOrDisablingKeepsTheFiring()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);
        var watch = Relics();
        store.Add(watch);
        store.MarkFired(watch.Id, Tick);

        Assert.Null(store.Update(watch with { Name = "Soontill", Enabled = false }));

        var kept = Assert.Single(store.Watches);
        Assert.Equal("Soontill", kept.Name);
        Assert.Equal(BgsTickWatch.Format(Tick), kept.FiredForTick);
    }

    [Fact]
    public void AnUpdateCannotSetTheFiringItself()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);
        var watch = Relics();
        store.Add(watch);

        store.Update(watch with { Name = "Soontill", FiredForTick = BgsTickWatch.Format(Tick) });

        Assert.Null(Assert.Single(store.Watches).FiredForTick);
    }

    [Fact]
    public void AStockWatchMissingWhatItNeedsIsRefusedOnRead()
    {
        var files = new MemoryFileSystem();
        files.WriteText(WatchesFile, """
            {
              "watches": [
                { "name": "No market", "kind": "stock", "commodity": "Gold", "comparison": "above", "threshold": 1 },
                { "name": "No commodity", "kind": "stock", "marketId": 1, "commodity": " ", "comparison": "above", "threshold": 1 },
                { "name": "Negative", "kind": "stock", "marketId": 1, "commodity": "Gold", "comparison": "below", "threshold": -1 },
                { "name": "Fine", "kind": "stock", "marketId": 1, "commodity": "Gold", "comparison": "below", "threshold": 5 },
                { "name": "Goals", "kind": "newCommunityGoal" }
              ]
            }
            """);
        var store = Store(files);

        store.Poll();

        Assert.Equal(["Fine", "Goals"], store.Watches.Select(watch => watch.Name));
        Assert.Equal(["No market", "No commodity", "Negative"], store.Problems.Select(problem => problem.Where));
        Assert.All(store.Watches, watch => Assert.True(watch.Enabled));
    }

    [Fact]
    public void AStockWatchMissingWhatItNeedsIsRefusedOnAdd()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);

        Assert.NotNull(store.Add(Relics() with { MarketId = null }));
        Assert.NotNull(store.Add(Relics() with { Commodity = "" }));
        Assert.NotNull(store.Add(Relics() with { Threshold = -1 }));

        Assert.Empty(store.Watches);
        Assert.Null(files.ReadText(WatchesFile));
    }

    [Fact]
    public void TheThirtyThirdWatchIsRefused()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);

        for (var i = 0; i < WatchStore.MaxWatches; i++)
        {
            Assert.Null(store.Add(Relics()));
        }

        Assert.NotNull(store.Add(Relics()));
        Assert.Equal(WatchStore.MaxWatches, store.Watches.Count);
    }

    [Fact]
    public void AnUnreadableFileIsOneProblem()
    {
        var files = new MemoryFileSystem();
        files.WriteText(WatchesFile, "not json");
        var store = Store(files);

        store.Poll();

        Assert.Empty(store.Watches);
        Assert.Equal("watches.json", Assert.Single(store.Problems).Where);
    }

    [Fact]
    public void DeletingRemovesOnlyThatWatch()
    {
        var files = new MemoryFileSystem();
        var store = Store(files);
        var kept = Relics();
        var gone = Relics();
        store.Add(kept);
        store.Add(gone);

        Assert.True(store.Delete(gone.Id));
        Assert.False(store.Delete(gone.Id));

        Assert.Equal(kept.Id, Assert.Single(store.Watches).Id);
    }
}
