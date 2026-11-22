using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>
/// A book and a podcast take turns, a story wins over a book, a callout pauses a book at its sentence, chatter waits
/// while a book is read, and ambient music yields to it (#2688).
/// </summary>
public sealed class OneMediaItemPlaysAtATimeTests
{
    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly PodcastEpisode Episode = new(
        "e1",
        "Episode one",
        new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
        new Uri("https://cdn.example.test/e1.mp3"),
        "audio/mpeg",
        null,
        TimeSpan.FromHours(1));

    private static readonly PodcastShow Show = new("Deep Space Hour", [Episode]);

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch };

    /// <summary>A podcast player on the rig's arbiter, joined to its book as the app joins them.</summary>
    private static PodcastPlayer Podcasts(BookRig rig)
    {
        var player = new PodcastPlayer(
            rig.Audio,
            rig.Music ?? new AmbientMusic(rig.Audio, () => CueLibrary.Load()),
            episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")),
            new MemoryFileSystem(),
            @"C:\d47\data\podcasts\listening.json",
            NullLogger<PodcastPlayer>.Instance,
            rig.Background.Enqueue);
        rig.Player.TakesTurnsWith(player);
        return player;
    }

    private static void PlayEpisode(BookRig rig, PodcastPlayer podcasts)
    {
        _ = podcasts.Play(Feed, Show, Episode);
        rig.Settle();
    }

    private static long EpisodeId(BookRig rig) => rig.Sink.Started.Last(request => request.Name == Episode.Id).Id;

    /// <summary>Ambient music with one docked track, following the game.</summary>
    private static AmbientMusic Berth(AudioArbiter audio)
    {
        var root = Path.Combine("C:" + Path.DirectorySeparatorChar, "d47-music");
        var files = new MemoryFileSystem();
        files.WriteBytes(
            Path.Combine(root, FolderAudioSource.MusicFolder, Situations.Docked, "berth.wav"),
            WavWriter.ToBytes(new byte[480], AudioFormat.Standard));

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(root, files, NullLogger<FolderAudioSource>.Instance));

        var music = new AmbientMusic(audio, () => library, new Random(7));
        audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);
        return music;
    }

    [Fact]
    public void StartingAPodcastPausesTheBookAndItDoesNotResumeByItself()
    {
        var rig = BookRig.InMemory();
        var podcasts = Podcasts(rig);
        rig.Play();

        PlayEpisode(rig, podcasts);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.True(podcasts.State!.Playing);
        Assert.DoesNotContain(EpisodeId(rig), rig.Sink.Paused);

        rig.Player.Settled();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.DoesNotContain(EpisodeId(rig), rig.Sink.Paused);
    }

    [Fact]
    public void StartingABookPausesThePodcastAndItDoesNotResumeByItself()
    {
        var rig = BookRig.InMemory();
        var podcasts = Podcasts(rig);
        PlayEpisode(rig, podcasts);

        rig.Play();

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.True(podcasts.State!.PausedByCommander);
        Assert.Contains(EpisodeId(rig), rig.Sink.Paused);

        rig.Finish(BookRig.First);
        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();

        Assert.True(podcasts.State!.PausedByCommander);
        Assert.Contains(EpisodeId(rig), rig.Sink.Paused);
    }

    [Fact]
    public void ResumingThePodcastPausesTheBook()
    {
        var rig = BookRig.InMemory();
        var podcasts = Podcasts(rig);
        PlayEpisode(rig, podcasts);
        rig.Play();

        Assert.Equal("Podcast resumed.", podcasts.Resume());
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.True(podcasts.State!.Playing);
        Assert.DoesNotContain(EpisodeId(rig), rig.Sink.Paused);
    }

    [Fact]
    public void ABookDoesNotStartWhileAStoryPlays()
    {
        var rig = BookRig.InMemory();
        rig.PlayStory();

        Assert.True(rig.Player.StoryPlaying);
        Assert.False(rig.Player.Play(BookRig.Source));
        rig.Settle();

        Assert.Null(rig.Player.Now);
        Assert.False(rig.Story.Now!.IsPaused);
        Assert.Equal(["story line"], rig.Sink.Names);
    }

    [Theory]
    [InlineData(StoryRunPause.PushToTalk)]
    [InlineData(StoryRunPause.Interrupted)]
    public void ABookDoesNotStartWhileAStoryIsPausedForSpeech(StoryRunPause reason)
    {
        var rig = BookRig.InMemory();
        rig.PlayStory();
        rig.Story.Pause(reason);

        Assert.False(rig.Player.Play(BookRig.Source));
        rig.Settle();

        Assert.Null(rig.Player.Now);
    }

    [Fact]
    public void ReadingFromAChapterWhileAStoryPlaysLeavesThePlaceWhereItWas()
    {
        var rig = BookRig.InMemory();
        var host = new D47.App.Panel.BooksHost(rig.Shelf, rig.Player, () => rig.Settings, _ => { }, _ => true, (_, _) => Task.FromResult(VoiceCatalogue.Silent));
        rig.PlayStory();

        Assert.False(host.ReadFrom(BookRig.Source, 1));

        Assert.Equal(new D47.Core.Books.BookPlace(0, 0), rig.Shelf.Find(BookRig.Source)!.Place);
    }

    [Fact]
    public void StoppingAPausedStoryLeavesTheBookReading()
    {
        var rig = BookRig.InMemory();
        rig.PlayStory();
        rig.Story.Pause(StoryRunPause.Commander);
        rig.Play();

        rig.Story.Stop();
        rig.Settle();
        rig.Finish(BookRig.First);

        Assert.Equal(BookRig.Second, rig.Player.Now!.Text);
        Assert.Equal(["story line", BookRig.First, BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void AStoryStartingPausesTheBookAndTheBookDoesNotResumeOverIt()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        rig.PlayStory();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);

        rig.Player.Resume();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.False(rig.Story.Now!.IsPaused);
        Assert.Equal([BookRig.First, "story line"], rig.Sink.Names);
    }

    [Fact]
    public void AStoryStartingPausesThePodcast()
    {
        var rig = BookRig.InMemory();
        var podcasts = Podcasts(rig);
        PlayEpisode(rig, podcasts);

        rig.PlayStory();

        Assert.Contains(EpisodeId(rig), rig.Sink.Paused);
    }

    [Fact]
    public void AStoryPausedByTheCommanderLetsABookStartAndIsNotStopped()
    {
        var rig = BookRig.InMemory();
        rig.PlayStory();
        rig.Story.Pause(StoryRunPause.Commander);

        Assert.True(rig.Player.Play(BookRig.Source));
        rig.Settle();

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.NotNull(rig.Story.Now);

        rig.Story.Resume();
        rig.Settle();

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
    }

    [Fact]
    public void ASpeechCalloutPausesTheBookAndItGoesOnFromThatSentence()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Finish(BookRig.First);

        rig.Say(AudioChannel.Speech, "callout", "callout-1");

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);
        Assert.Equal(BookRig.Second, rig.Player.Now.Text);

        rig.Finish("callout");

        Assert.False(rig.Player.Now!.IsPaused);

        rig.Sink.FinishUntil(BookRig.Second);

        Assert.Equal([BookRig.First, BookRig.Second, "callout", BookRig.Second], rig.Sink.Names);
    }

    [Fact]
    public void ACalloutAlreadySpeakingWhenTheBookStartsIsKeptAndTheBookFollowsIt()
    {
        var rig = BookRig.InMemory();
        rig.Say(AudioChannel.Speech, "callout", "callout-1");

        rig.Play();

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);

        rig.Finish("callout");
        rig.Sink.FinishUntil(BookRig.First);

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.Equal(["callout", BookRig.First], rig.Sink.Names);
    }

    [Fact]
    public void ResumingTheBookDuringACalloutWaitsForTheCalloutToEnd()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();
        rig.Say(AudioChannel.Speech, "callout", "callout-1");

        rig.Player.Resume();
        rig.Settle();

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);

        rig.Finish("callout");
        rig.Sink.FinishUntil(BookRig.First);

        Assert.Equal([BookRig.First, "callout", BookRig.First], rig.Sink.Names);
    }

    [Fact]
    public void ChatterIsRefusedWhileTheBookIsReadOrHeldForACalloutAndHeardOnceTheCommanderPausesIt()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        rig.Say(AudioChannel.Overheard, "chatter one", "comms");
        rig.Say(AudioChannel.Speech, "callout", "callout-1");
        rig.Say(AudioChannel.Overheard, "chatter two", "comms");
        rig.Finish("callout");
        rig.Sink.FinishUntil(BookRig.First);

        Assert.False(rig.Player.Now!.IsPaused);

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();
        rig.Say(AudioChannel.Overheard, "chatter three", "comms");
        rig.Sink.FinishUntil("chatter three");

        Assert.Equal([BookRig.First, "callout", BookRig.First, "chatter three"], rig.Sink.Names);
    }

    [Fact]
    public void ChatterIsRefusedWhileAPodcastPlays()
    {
        var rig = BookRig.InMemory();
        var podcasts = Podcasts(rig);
        PlayEpisode(rig, podcasts);

        rig.Say(AudioChannel.Overheard, "chatter", "comms");

        Assert.DoesNotContain("chatter", rig.Sink.Names);
    }

    [Fact]
    public void AmbientMusicYieldsWhileTheBookIsReadThroughACalloutAndComesBackWhenItPauses()
    {
        var rig = BookRig.InMemory(music: Berth);
        var music = rig.Music!;
        music.Follow(Docked, musicTrack: null, DateTimeOffset.UnixEpoch);

        Assert.True(music.State.Playing);

        rig.Play();
        Assert.False(music.State.Playing);

        rig.Say(AudioChannel.Speech, "callout", "callout-1");
        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);
        Assert.False(music.State.Playing);

        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();

        Assert.True(music.State.Playing);
    }

    [Fact]
    public void AmbientMusicComesBackWhenTheBookStops()
    {
        var rig = BookRig.InMemory(music: Berth);
        var music = rig.Music!;
        music.Follow(Docked, musicTrack: null, DateTimeOffset.UnixEpoch);
        rig.Play();

        rig.Player.Stop();
        rig.Settle();

        Assert.True(music.State.Playing);
    }

    [Fact]
    public void ChatterIsHeardUnderAmbientMusic()
    {
        var rig = BookRig.InMemory(music: Berth);
        rig.Music!.Follow(Docked, musicTrack: null, DateTimeOffset.UnixEpoch);

        rig.Say(AudioChannel.Overheard, "chatter", "comms");

        Assert.Contains("chatter", rig.Sink.Names);
    }
}
