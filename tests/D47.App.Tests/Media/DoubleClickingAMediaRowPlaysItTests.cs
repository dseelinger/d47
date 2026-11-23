using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Tests.Books;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Podcasts;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>A double-click on a Catalogue title, a shelf book or a podcast show plays it; a single click does what it did (#2711).</summary>
[Trait("Category", "Integration")]
public sealed class DoubleClickingAMediaRowPlaysItTests
{
    private static readonly Uri FeedOne = new("https://example.test/one.xml");

    private static readonly Uri FeedTwo = new("https://example.test/two.xml");

    private static Button Row(Control root, string name, string? tag = null) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name && (tag is null || (button.Tag as string) == tag));

    // ----- Movies > Catalogue

    private sealed class Catalogue(Func<VideoPage> answer) : IVideoCatalogue
    {
        public Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken) =>
            Task.FromResult(answer());
    }

    private static VideoTitle Title(int number, bool playable = true) => new(
        VideoSources.InternetArchive,
        $"item{number}",
        $"Item {number}",
        VideoKind.Footage,
        "public domain",
        TimeSpan.FromSeconds(60 + number),
        playable ? new Uri($"https://archive.example.test/item{number}.mp4") : null,
        null,
        Unplayable: playable ? null : "offered as MOV only, which Windows can't play here");

    private static (Window Window, PanelView Panel, WatchHost Host) OpenCatalogue(bool headset = false)
    {
        var (settings, _, _) = TestSurface.Create();
        var files = new MemoryFileSystem();
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var host = new WatchHost(
            new D47.App.Media.VideoPlayback(arbiter, background: work => work()),
            new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance),
            settings,
            files,
            catalogue: true,
            readSubtitles: (_, _) => Task.FromResult<string?>(null),
            background: work => work(),
            videos: new Catalogue(() => new VideoPage([Title(1), Title(2), Title(3, playable: false)], 3, More: false)));

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(watch: host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MoviesRoot));
        Dispatcher.UIThread.RunJobs();
        RowClicks.Click(Row(panel, WatchPage.FindName));

        return (window, panel, host);
    }

    private static void DoubleClick(Window window, Func<Button> row)
    {
        RowClicks.Press(row(), window, 1);
        RowClicks.Click(row());
        RowClicks.Press(row(), window, 2);
    }

    [AvaloniaFact]
    public void ASingleClickOnACatalogueTitleMarksItAndPlaysNothing()
    {
        var (window, panel, host) = OpenCatalogue();

        RowClicks.Press(Row(panel, CataloguePage.RowName, "archive.item2"), window, 1);
        RowClicks.Click(Row(panel, CataloguePage.RowName, "archive.item2"));

        Assert.Equal(["Movies", "Catalogue"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.True(Row(panel, CataloguePage.WatchName).IsEnabled);

        window.Close();
        host.Dispose();
    }

    [AvaloniaFact]
    public void ADoubleClickOnACatalogueTitleOpensThePlayerLikeWatchNow()
    {
        var (window, panel, host) = OpenCatalogue();

        DoubleClick(window, () => Row(panel, CataloguePage.RowName, "archive.item2"));

        Assert.Equal(["Movies", "Item 2"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
        host.Dispose();
    }

    [AvaloniaFact]
    public void ADoubleClickOnATitleThatCannotPlayDoesNothing()
    {
        var (window, panel, host) = OpenCatalogue();

        Assert.False(Row(panel, CataloguePage.RowName, "archive.item3").IsEnabled);

        RowClicks.Press(Row(panel, CataloguePage.RowName, "archive.item3"), window, 2);

        Assert.Equal(["Movies", "Catalogue"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
        host.Dispose();
    }

    [AvaloniaFact]
    public void OnTheBigPanelADoubleClickOnACatalogueTitleDoesNothing()
    {
        var (window, panel, host) = OpenCatalogue(headset: true);

        DoubleClick(window, () => Row(panel, CataloguePage.RowName, "archive.item2"));

        Assert.Equal(["Movies", "Catalogue"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
        host.Dispose();
    }

    // ----- Media > Books

    private static (Window Window, PanelView Panel) OpenBooks(BookRig rig, bool headset = false)
    {
        var host = new BooksHost(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => rig.Installed,
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            rig.BookFiles);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(books: host);

        var window = new Window { Content = panel, Width = headset ? 1024 : 1280, Height = headset ? 640 : 1000 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    [AvaloniaFact]
    public void ASingleClickOnAShelfBookOpensItsPageOnceTheDoubleClickTimeHasPassed()
    {
        var rig = BookRig.InMemory();
        var (window, panel) = OpenBooks(rig);

        RowClicks.Press(Row(panel, BooksPage.RowName), window, 1);
        RowClicks.Click(Row(panel, BooksPage.RowName));

        Assert.Equal("Books", panel.Nav.Trail[^1].Word);

        RowClicks.Until(() => panel.Nav.Trail[^1].Word == "The Night Watch");

        Assert.Equal("The Night Watch", panel.Nav.Trail[^1].Word);
        Assert.Null(rig.Player.Now);

        window.Close();
    }

    [AvaloniaFact]
    public void ADoubleClickOnAShelfBookReadsItOnAndLeavesTheShelfShowing()
    {
        var rig = BookRig.InMemory();
        rig.Shelf.MoveTo(BookRig.Source, new D47.Core.Books.BookPlace(1, 0), finished: false);
        var (window, panel) = OpenBooks(rig);

        DoubleClick(window, () => Row(panel, BooksPage.RowName));
        rig.Settle();
        Dispatcher.UIThread.RunJobs();
        RowClicks.Click(Row(panel, BooksPage.RowName));
        RowClicks.WaitPastDoubleClick();

        Assert.NotNull(rig.Player.Now);
        Assert.Equal(new D47.Core.Books.BookPlace(1, 0), rig.Player.Now!.Place);
        Assert.NotEqual("The Night Watch", panel.Nav.Trail[^1].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void OnTheBigPanelAShelfBookOpensOnTheFirstClickAndADoubleClickReadsNothing()
    {
        var rig = BookRig.InMemory();
        var (window, panel) = OpenBooks(rig, headset: true);

        RowClicks.Press(Row(panel, BooksPage.RowName), window, 1);
        RowClicks.Click(Row(panel, BooksPage.RowName));

        Assert.Equal("The Night Watch", panel.Nav.Trail[^1].Word);
        Assert.Null(rig.Player.Now);

        window.Close();
    }

    // ----- Media > Podcasts

    private static PodcastEpisode Episode(string id, int day) =>
        new(id, $"Episode {id}", new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, TimeSpan.FromMinutes(40));

    private static (Window Window, PanelView Panel, List<Uri> Played, List<string> Said) OpenPodcasts(bool headset = false, Action<PodcastPlayer>? created = null)
    {
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var music = new AmbientMusic(arbiter, () => CueLibrary.Load());
        var player = new PodcastPlayer(arbiter, music, episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")), new MemoryFileSystem(), @"C:\d47\listening.json", NullLogger<PodcastPlayer>.Instance, work => work());
        var played = new List<Uri>();
        var said = new List<string>();
        CachedShow[] shows =
        [
            new(FeedOne, new PodcastShow("Deep Space", [Episode("a", 21)])),
            new(FeedTwo, new PodcastShow("Deep Space Hour", [Episode("x", 3)])),
        ];

        created?.Invoke(player);

        var host = new PodcastsHost(
            player,
            () => shows,
            () => [FeedOne, FeedTwo],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([]),
            _ => Task.FromResult(new PodcastSearchOutcome([], null)),
            feed => Task.FromResult(new PodcastRefresh(feed, null, null)),
            _ => { },
            feed => new PodcastShowPlay { Feed = feed.AbsoluteUri },
            (_, _, _) => { },
            feed =>
            {
                played.Add(feed);
                return Task.FromResult("Every episode of Deep Space Hour is finished.");
            });

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.PodcastsRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel, played, said);
    }

    [AvaloniaFact]
    public void ASingleClickOnAShowSelectsItAndPlaysNothing()
    {
        var (window, panel, played, _) = OpenPodcasts();

        RowClicks.Press(Row(panel, PodcastsPage.ShowName, FeedTwo.AbsoluteUri), window, 1);
        RowClicks.Click(Row(panel, PodcastsPage.ShowName, FeedTwo.AbsoluteUri));

        Assert.Empty(played);

        window.Close();
    }

    [AvaloniaFact]
    public void ADoubleClickOnAShowPlaysThatShowByItsFeedAndSaysWhenEveryEpisodeIsFinished()
    {
        var (window, panel, played, _) = OpenPodcasts();

        DoubleClick(window, () => Row(panel, PodcastsPage.ShowName, FeedTwo.AbsoluteUri));

        Assert.Equal([FeedTwo], played);
        Assert.Contains(
            panel.GetVisualDescendants().OfType<StatusLine>(),
            line => line.IsVisible && line.Text == "Every episode of Deep Space Hour is finished.");

        window.Close();
    }

    [AvaloniaFact]
    public async Task ADoubleClickOnAFinishedShowSaysSoWhileAnotherShowsEpisodeIsInTheSlot()
    {
        PodcastPlayer? slot = null;
        var (window, panel, played, _) = OpenPodcasts(created: player => slot = player);

        await slot!.Play(FeedOne, new PodcastShow("Deep Space", [Episode("a", 21)]), Episode("a", 21));
        Assert.NotNull(slot.State);

        DoubleClick(window, () => Row(panel, PodcastsPage.ShowName, FeedTwo.AbsoluteUri));

        Assert.Equal([FeedTwo], played);
        Assert.Contains(
            panel.GetVisualDescendants().OfType<StatusLine>(),
            line => line.IsVisible && line.Text == "Every episode of Deep Space Hour is finished.");

        window.Close();
    }

    [AvaloniaFact]
    public void ADoubleClickWithControlHeldPlaysNothing()
    {
        var (window, panel, played, _) = OpenPodcasts();

        RowClicks.Press(Row(panel, PodcastsPage.ShowName, FeedOne.AbsoluteUri), window, 1);
        RowClicks.Press(Row(panel, PodcastsPage.ShowName, FeedOne.AbsoluteUri), window, 2, KeyModifiers.Control);

        Assert.Empty(played);

        window.Close();
    }

    [AvaloniaFact]
    public void OnTheBigPanelADoubleClickOnAShowPlaysNothing()
    {
        var (window, panel, played, _) = OpenPodcasts(headset: true);

        DoubleClick(window, () => Row(panel, PodcastsPage.ShowName, FeedTwo.AbsoluteUri));

        Assert.Empty(played);

        window.Close();
    }
}
