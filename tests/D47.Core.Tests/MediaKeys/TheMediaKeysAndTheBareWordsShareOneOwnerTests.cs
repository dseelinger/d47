using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Podcasts;
using D47.Core.Tests.Audio;
using D47.Core.Tests.Podcasts;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.MediaKeys;

/// <summary>Which item takes bare "play" and "pause" and the media keys (#2712).</summary>
public sealed class TheMediaKeysAndTheBareWordsShareOneOwnerTests
{
    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private static readonly PodcastEpisode Episode = new(
        "e1",
        "Episode one",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new Uri("https://cdn.example.test/e1.mp3"),
        "audio/mpeg",
        null,
        TimeSpan.FromHours(1));

    private static readonly PodcastShow Show = new("Deep Space Hour", [Episode]);

    private readonly RecordingAudioSink _sink = new();

    private bool _story;
    private bool _book;
    private bool _bookPaused;
    private VideoStanding? _video;

    private PodcastDesk NewPodcastDesk()
    {
        var audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        var player = new PodcastPlayer(
            audio,
            new AmbientMusic(audio, () => NoMusic.Value),
            episode => new MusicTrack(episode.Id, () => new MemoryPcm(canSeek: true)),
            new D47.Core.Storage.MemoryFileSystem(),
            @"C:\d47\data\podcasts\listening.json",
            NullLogger<PodcastPlayer>.Instance,
            work => work(),
            new ManualTime());

        return new PodcastDesk(
            player,
            () => [new CachedShow(Feed, Show)],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([new PodcastRefresh(Feed, Show, null)]),
            _ => Task.FromResult(new PodcastSearchOutcome([], null)),
            feed => Task.FromResult(new PodcastRefresh(feed, Show, null)));
    }

    private BookDesk NewBookDesk() => new(
        () => [],
        _ => true,
        () => _book ? new BookNow(BookSource.FromGutenberg(36), "The War of the Worlds", 0, "The Eve of the War", _bookPaused) : null,
        () => { },
        () => { },
        () => true,
        () => true);

    private WatchDesk NewWatchDesk() => new(
        () => [],
        () => [],
        (_, _) => Task.FromResult(new VideoPage([], 0)),
        _ => true,
        () => _video is { } standing
            ? new VideoNow(new VideoTitle("file", "a", "Orbit", VideoKind.Other, string.Empty, null, null, null), standing)
            : null,
        () => { },
        () => true,
        _ => true,
        _ => { });

    private MediaOwner Owner(PodcastDesk podcast, BookDesk book, WatchDesk video) =>
        MediaOwners.Of(() => _story, () => book, () => podcast, () => video);

    [Fact]
    public void TheOwnerFollowsTheOrderTheBareWordsUse()
    {
        var podcast = NewPodcastDesk();
        var book = NewBookDesk();
        var video = NewWatchDesk();

        Assert.Equal(MediaOwner.Music, Owner(podcast, book, video));

        _video = VideoStanding.Paused;
        Assert.Equal(MediaOwner.Video, Owner(podcast, book, video));

        _video = VideoStanding.Playing;
        Assert.Equal(MediaOwner.Video, Owner(podcast, book, video));

        _book = true;
        _bookPaused = true;
        _video = VideoStanding.Paused;
        Assert.Equal(MediaOwner.Book, Owner(podcast, book, video));

        _video = VideoStanding.Playing;
        Assert.Equal(MediaOwner.Video, Owner(podcast, book, video));

        _video = null;
        _bookPaused = false;
        Assert.Equal(MediaOwner.Book, Owner(podcast, book, video));

        _ = podcast.Player.Play(Feed, Show, Episode);
        Assert.True(podcast.Player.State is { Playing: true });
        Assert.Equal(MediaOwner.Podcast, Owner(podcast, book, video));

        _bookPaused = true;
        Assert.Equal(MediaOwner.Podcast, Owner(podcast, book, video));

        _video = VideoStanding.Playing;
        Assert.Equal(MediaOwner.Podcast, Owner(podcast, book, video));

        _story = true;
        Assert.Equal(MediaOwner.Story, Owner(podcast, book, video));

        _story = false;
        _video = null;
        podcast.Player.Pause();
        Assert.True(podcast.Active);
        Assert.Equal(MediaOwner.Book, Owner(podcast, book, video));

        _book = false;
        Assert.Equal(MediaOwner.Podcast, Owner(podcast, book, video));

        _video = VideoStanding.Paused;
        Assert.Equal(MediaOwner.Podcast, Owner(podcast, book, video));

        _video = VideoStanding.Playing;
        Assert.Equal(MediaOwner.Video, Owner(podcast, book, video));

        podcast.Stop();
    }

    [Fact]
    public void AStoryOpenOverAPlayingBookTakesTheKeys()
    {
        _book = true;
        _story = true;

        Assert.Equal(MediaOwner.Story, Owner(NewPodcastDesk(), NewBookDesk(), NewWatchDesk()));
    }

    [Fact]
    public void AFailedVideoDoesNotTakeTheKeys()
    {
        _video = VideoStanding.Failed;

        Assert.Equal(MediaOwner.Music, Owner(NewPodcastDesk(), NewBookDesk(), NewWatchDesk()));
    }
}
