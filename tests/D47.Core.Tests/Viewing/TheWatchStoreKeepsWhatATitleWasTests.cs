using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary><see cref="WatchStore"/> keeps what a title was, so a row can be drawn and played again after a restart (#2673).</summary>
public sealed class TheWatchStoreKeepsWhatATitleWasTests
{
    private const string Path = "data/watch.json";

    private static readonly DateTimeOffset Noon = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly VideoTitle Steel = new(
        VideoSources.Blender,
        "tears",
        "Tears of Steel",
        VideoKind.ShortFilm,
        "CC BY 3.0",
        TimeSpan.FromSeconds(734),
        new Uri("https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mp4"),
        new Uri("https://download.blender.org/demo/movies/ToS/tears_of_steel.srt"),
        "© Blender Foundation");

    private readonly MemoryFileSystem _files = new();

    private WatchStore Open()
    {
        var store = new WatchStore(Path, _files, NullLogger<WatchStore>.Instance);
        store.Load();
        return store;
    }

    [Fact]
    public void ASavedPlaceKeepsTheTitleAcrossARestart()
    {
        Open().SavePlace(Steel.Source, Steel.Id, TimeSpan.FromSeconds(401), false, Noon, Steel);

        var kept = Assert.Single(Open().CarryOnWatching());

        Assert.Equal(Steel, kept.Video);
        Assert.Equal(TimeSpan.FromSeconds(401), kept.Place);
    }

    [Fact]
    public void ALaterSaveWithoutTheTitleKeepsWhatWasThere()
    {
        var store = Open();
        store.Add(Steel.Source, Steel.Id, Noon, Steel);
        store.SavePlace(Steel.Source, Steel.Id, TimeSpan.FromSeconds(60), false, Noon.AddMinutes(1));

        Assert.Equal(Steel, Open().Find(Steel.Source, Steel.Id)!.Video);
    }

    [Fact]
    public void AFileOnThisComputerKeepsItsPathAsItsAddress()
    {
        var path = System.IO.Path.GetFullPath("orbit timelapse.mp4");
        var file = new VideoTitle("file", path, "orbit timelapse.mp4", VideoKind.Other, string.Empty, null, new Uri(path, UriKind.Absolute), null);

        Open().Add(file.Source, file.Id, Noon, file);

        var kept = Open().YourList().Single().Video!;

        Assert.True(kept.Media!.IsFile);
        Assert.Equal(path, kept.Media.LocalPath);
    }

    [Fact]
    public void TheSourcesAreThoseOfEveryTitleKept()
    {
        var store = Open();
        store.Add(Steel.Source, Steel.Id, Noon, Steel);
        store.SavePlace("archive", "moon", TimeSpan.FromSeconds(5), false, Noon);
        store.Add("file", "a.mp4", Noon);

        Assert.Equal(["archive", "blender", "file"], store.Sources().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AFileWrittenBeforeTitlesWereKeptStillLoads()
    {
        _files.WriteText(Path, """{"titles":[{"source":"blender","id":"tears","inList":true,"addedAt":"2026-11-22T12:00:00+00:00","seconds":0,"finished":false}]}""");

        var entry = Assert.Single(Open().YourList());

        Assert.Null(entry.Video);
    }
}
