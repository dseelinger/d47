using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary><see cref="WatchStore"/> keeps Your list and each title's place in <c>watch.json</c> (#2672).</summary>
public sealed class TheWatchListAndPlacesAreKeptTests
{
    private const string Path = "data/watch.json";

    private static readonly DateTimeOffset Noon = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private WatchStore Open()
    {
        var store = new WatchStore(Path, _files, NullLogger<WatchStore>.Instance);
        store.Load();
        return store;
    }

    [Fact]
    public void ATitleAddedToYourListIsThereAfterARestart()
    {
        var store = Open();
        store.Add("catalogue", "t1", Noon);
        store.Add("file", "C:/clips/a.mp4", Noon.AddMinutes(1));
        store.Add("catalogue", "t1", Noon.AddMinutes(2));

        var reopened = Open().YourList();

        Assert.Equal(["t1", "C:/clips/a.mp4"], reopened.Select(entry => entry.Id));
        Assert.Equal(Noon, reopened[0].AddedAt);
    }

    [Fact]
    public void ATitleRemovedFromYourListIsGone()
    {
        var store = Open();
        store.Add("catalogue", "t1", Noon);
        store.Remove("catalogue", "t1");

        Assert.Empty(store.YourList());
        Assert.Empty(Open().YourList());
        Assert.Null(Open().Find("catalogue", "t1"));
    }

    [Fact]
    public void ARemovedTitleKeepsItsPlace()
    {
        var store = Open();
        store.Add("catalogue", "t1", Noon);
        store.SavePlace("catalogue", "t1", TimeSpan.FromMinutes(7), false, Noon);
        store.Remove("catalogue", "t1");

        Assert.Empty(store.YourList());
        Assert.Equal(TimeSpan.FromMinutes(7), Open().Find("catalogue", "t1")!.Place);
    }

    [Fact]
    public void APlaceIsReadBackWithWhenItWasWatched()
    {
        Open().SavePlace("catalogue", "t1", TimeSpan.FromSeconds(95.5), false, Noon);

        var entry = Open().Find("catalogue", "t1")!;

        Assert.Equal(TimeSpan.FromSeconds(95.5), entry.Place);
        Assert.Equal(Noon, entry.LastWatched);
        Assert.False(entry.Finished);
    }

    [Fact]
    public void TheSameIdFromTwoSourcesIsTwoTitles()
    {
        var store = Open();
        store.SavePlace("catalogue", "1", TimeSpan.FromSeconds(10), false, Noon);
        store.SavePlace("file", "1", TimeSpan.FromSeconds(20), false, Noon);

        Assert.Equal(TimeSpan.FromSeconds(10), store.Find("catalogue", "1")!.Place);
        Assert.Equal(TimeSpan.FromSeconds(20), store.Find("file", "1")!.Place);
    }

    [Fact]
    public void CarryOnWatchingIsUnfinishedTitlesWithAPlaceNewestFirst()
    {
        var store = Open();
        store.SavePlace("catalogue", "old", TimeSpan.FromMinutes(1), false, Noon);
        store.SavePlace("catalogue", "new", TimeSpan.FromMinutes(2), false, Noon.AddHours(1));
        store.SavePlace("catalogue", "done", TimeSpan.FromMinutes(90), true, Noon.AddHours(2));
        store.SavePlace("catalogue", "unstarted", TimeSpan.Zero, false, Noon.AddHours(3));
        store.Add("catalogue", "listed-only", Noon);

        Assert.Equal(["new", "old"], Open().CarryOnWatching().Select(entry => entry.Id));
    }

    [Fact]
    public void AFinishedTitleLeavesCarryOnWatching()
    {
        var store = Open();
        store.SavePlace("catalogue", "t1", TimeSpan.FromMinutes(30), false, Noon);
        Assert.Single(store.CarryOnWatching());

        store.SavePlace("catalogue", "t1", TimeSpan.FromMinutes(60), true, Noon.AddMinutes(30));

        Assert.Empty(store.CarryOnWatching());
        Assert.True(Open().Find("catalogue", "t1")!.Finished);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2")]
    [InlineData("null")]
    public void ACorruptFileLoadsAsEmptyAndTheBadFileIsSetAside(string text)
    {
        _files.WriteText(Path, text);

        var store = Open();

        Assert.Empty(store.YourList());
        Assert.Empty(store.CarryOnWatching());
        Assert.Null(_files.ReadText(Path));
        Assert.Equal(text, _files.ReadText(Path + ".bad"));
    }

    [Fact]
    public void AStoreSetAsideCanBeWrittenAgain()
    {
        _files.WriteText(Path, "not json");
        var store = Open();

        store.Add("catalogue", "t1", Noon);

        Assert.Single(Open().YourList());
    }
}
