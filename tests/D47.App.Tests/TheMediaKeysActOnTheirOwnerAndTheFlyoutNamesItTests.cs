using D47.App.Media;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Podcasts;
using D47.Core.Viewing;
using Xunit;

namespace D47.App.Tests;

/// <summary>What each media key does to each owner, and what the flyout shows for it.</summary>
public class TheMediaKeysActOnTheirOwnerAndTheFlyoutNamesItTests
{
    private sealed class Targets : IMediaKeyTargets, IMediaFlyoutSource
    {
        public List<string> Calls { get; } = [];

        public Task<string> NextEpisode { get; set; } = Task.FromResult("next episode");

        public string? StoryTitle { get; set; }

        public bool StoryPaused { get; set; }

        public BookNow? Book { get; set; }

        public PodcastPlayerState? Podcast { get; set; }

        public VideoTitle? Video { get; set; }

        public bool VideoPlaying { get; set; }

        public MusicState Music { get; set; } = new(null, false, false);

        public void StoryResume() => Calls.Add("story resume");

        public void StoryPause() => Calls.Add("story pause");

        public void StorySkip() => Calls.Add("story skip");

        public string BookResume() => Record("book resume");

        public string BookPause() => Record("book pause");

        public string BookNextChapter() => Record("book next chapter");

        public string BookBackAParagraph() => Record("book back a paragraph");

        public string VideoResume() => Record("video resume");

        public string VideoPause() => Record("video pause");

        public string VideoSkip(int seconds) => Record($"video skip {seconds}");

        public string PodcastResume() => Record("podcast resume");

        public string PodcastPause() => Record("podcast pause");

        public string PodcastSkipBack() => Record("podcast skip back");

        public Task<string> PodcastNext()
        {
            Calls.Add("podcast next");
            return NextEpisode;
        }

        public string MusicControl(MusicAction action) => Record($"music {action}");

        private string Record(string call)
        {
            Calls.Add(call);
            return call;
        }
    }

    [Theory]
    [InlineData(MediaOwner.Story, MediaKey.Play, "story resume")]
    [InlineData(MediaOwner.Story, MediaKey.Pause, "story pause")]
    [InlineData(MediaOwner.Story, MediaKey.Next, "story skip")]
    [InlineData(MediaOwner.Book, MediaKey.Play, "book resume")]
    [InlineData(MediaOwner.Book, MediaKey.Pause, "book pause")]
    [InlineData(MediaOwner.Book, MediaKey.Next, "book next chapter")]
    [InlineData(MediaOwner.Book, MediaKey.Previous, "book back a paragraph")]
    [InlineData(MediaOwner.Video, MediaKey.Play, "video resume")]
    [InlineData(MediaOwner.Video, MediaKey.Pause, "video pause")]
    [InlineData(MediaOwner.Video, MediaKey.Next, "video skip 30")]
    [InlineData(MediaOwner.Video, MediaKey.Previous, "video skip -30")]
    [InlineData(MediaOwner.Podcast, MediaKey.Play, "podcast resume")]
    [InlineData(MediaOwner.Podcast, MediaKey.Pause, "podcast pause")]
    [InlineData(MediaOwner.Podcast, MediaKey.Previous, "podcast skip back")]
    [InlineData(MediaOwner.Podcast, MediaKey.Next, "podcast next")]
    [InlineData(MediaOwner.Music, MediaKey.Play, "music Resume")]
    [InlineData(MediaOwner.Music, MediaKey.Pause, "music Pause")]
    [InlineData(MediaOwner.Music, MediaKey.Next, "music Next")]
    [InlineData(MediaOwner.Music, MediaKey.Previous, "music Previous")]
    public void AKeyDoesWhatItsOwnerDoesWithIt(MediaOwner owner, MediaKey key, string expected)
    {
        var targets = new Targets();

        var result = MediaKeys.Press(owner, key, targets);

        Assert.Equal([expected], targets.Calls);
        Assert.NotNull(result);
    }

    [Fact]
    public void PreviousOnAStoryDoesNothing()
    {
        var targets = new Targets();

        var result = MediaKeys.Press(MediaOwner.Story, MediaKey.Previous, targets);

        Assert.Null(result);
        Assert.Empty(targets.Calls);
    }

    [Fact]
    public async Task APodcastsNextKeyReturnsBeforeTheEpisodeHasOpened()
    {
        var opening = new TaskCompletionSource<string>();
        var targets = new Targets { NextEpisode = opening.Task };

        var result = MediaKeys.Press(MediaOwner.Podcast, MediaKey.Next, targets);

        Assert.NotNull(result);
        Assert.False(result.IsCompleted);

        opening.SetResult("Now playing");

        Assert.Equal("Now playing", await result);
    }

    [Fact]
    public async Task APodcastsNextKeyReportsAFailedOpen()
    {
        var targets = new Targets { NextEpisode = Task.FromException<string>(new InvalidOperationException()) };

        var result = MediaKeys.Press(MediaOwner.Podcast, MediaKey.Next, targets);

        Assert.Equal("failed", await result!);
    }

    [Theory]
    [InlineData(false, MediaFlyoutStatus.Playing)]
    [InlineData(true, MediaFlyoutStatus.Paused)]
    public void AStoryShowsItsTitleAndWhetherItIsPaused(bool paused, MediaFlyoutStatus status)
    {
        var flyout = MediaKeys.Flyout(MediaOwner.Story, new Targets { StoryTitle = "The Long Dark", StoryPaused = paused });

        Assert.Equal(new MediaFlyout(status, "The Long Dark", "Story"), flyout);
    }

    [Theory]
    [InlineData(false, MediaFlyoutStatus.Playing)]
    [InlineData(true, MediaFlyoutStatus.Paused)]
    public void ABookShowsItsTitleAndItsChapterTitle(bool paused, MediaFlyoutStatus status)
    {
        var book = new BookNow(BookSource.FromGutenberg(36), "The War of the Worlds", 0, "The Eve of the War", paused);

        var flyout = MediaKeys.Flyout(MediaOwner.Book, new Targets { Book = book });

        Assert.Equal(new MediaFlyout(status, "The War of the Worlds", "The Eve of the War"), flyout);
    }

    [Theory]
    [InlineData(true, MediaFlyoutStatus.Playing)]
    [InlineData(false, MediaFlyoutStatus.Paused)]
    public void APodcastShowsItsEpisodeAndItsShow(bool playing, MediaFlyoutStatus status)
    {
        var episode = new PodcastEpisode("e1", "Episode One", DateTimeOffset.UnixEpoch, new Uri("https://example.test/e1.mp3"), "audio/mpeg", null, null);
        var state = new PodcastPlayerState(new Uri("https://example.test/feed"), "The Show", episode, playing, !playing, TimeSpan.Zero, null, false);

        var flyout = MediaKeys.Flyout(MediaOwner.Podcast, new Targets { Podcast = state });

        Assert.Equal(new MediaFlyout(status, "Episode One", "The Show"), flyout);
    }

    [Theory]
    [InlineData(true, MediaFlyoutStatus.Playing)]
    [InlineData(false, MediaFlyoutStatus.Paused)]
    public void AVideoShowsItsTitle(bool playing, MediaFlyoutStatus status)
    {
        var video = new VideoTitle("file", "a", "Orbit", VideoKind.Other, string.Empty, null, null, null);

        var flyout = MediaKeys.Flyout(MediaOwner.Video, new Targets { Video = video, VideoPlaying = playing });

        Assert.Equal(new MediaFlyout(status, "Orbit", "Video"), flyout);
    }

    [Fact]
    public void MusicShowsItsTrackWithoutTheFolderOrExtension()
    {
        var music = new MusicState(Path.Combine("music", "Cold Orbit.mp3"), true, false);

        var flyout = MediaKeys.Flyout(MediaOwner.Music, new Targets { Music = music });

        Assert.Equal(new MediaFlyout(MediaFlyoutStatus.Playing, "Cold Orbit", "Directive 47"), flyout);
    }

    [Theory]
    [InlineData(false, true, false, MediaFlyoutStatus.Paused)]
    [InlineData(false, false, true, MediaFlyoutStatus.Paused)]
    [InlineData(false, false, false, MediaFlyoutStatus.Stopped)]
    public void MusicWithNoTrackClearsTheFlyoutAndShowsItsState(bool playing, bool paused, bool idle, MediaFlyoutStatus status)
    {
        var music = new MusicState(null, playing, paused, idle);

        var flyout = MediaKeys.Flyout(MediaOwner.Music, new Targets { Music = music });

        Assert.Equal(new MediaFlyout(status, null, null), flyout);
    }

    [Fact]
    public void AnOwnerWithNothingToShowFallsBackToTheMusic()
    {
        var music = new MusicState("loop.ogg", false, true);

        var flyout = MediaKeys.Flyout(MediaOwner.Story, new Targets { Music = music });

        Assert.Equal(new MediaFlyout(MediaFlyoutStatus.Paused, "loop", "Directive 47"), flyout);
    }
}
