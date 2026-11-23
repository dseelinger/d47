using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>"Pause the book", a bare "pause" and "pause the podcast" said over push-to-talk pause as the Commander, and the pause outlasts the turn (#2689).</summary>
public sealed class SayingPauseOverPushToTalkKeepsTheMediaPausedTests
{
    private static BookDesk Desk(BookRig rig) => new(
        () => rig.Shelf.Books,
        rig.Player.Play,
        () => rig.Player.Now?.ToBookNow(),
        () => rig.Player.Pause(StoryRunPause.Commander),
        rig.Player.Resume,
        () => true,
        () => true,
        () => rig.Player.StoryPlaying);

    /// <summary>Opens a turn, which pauses the book as the app does.</summary>
    private static void HoldPushToTalk(BookRig rig)
    {
        rig.Audio.TurnInFlight = true;
        rig.Player.Pause(StoryRunPause.PushToTalk);
        rig.Settle();
    }

    private static void EndTheTurn(BookRig rig)
    {
        rig.Audio.TurnInFlight = false;
        rig.Player.Settled();
        rig.Settle();
    }

    [Fact]
    public void PauseTheBookOverPushToTalkLeavesItPausedAfterTheTurn()
    {
        var rig = BookRig.InMemory();
        var desk = Desk(rig);
        rig.Play();
        rig.Finish(BookRig.First);

        HoldPushToTalk(rig);

        Assert.Equal("Paused.", desk.Pause());

        EndTheTurn(rig);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal("The book is already paused.", desk.Pause());
    }

    [Fact]
    public void ABarePauseWhileABookReadsPausesItThroughThePushToTalkTurn()
    {
        var rig = BookRig.InMemory();
        var desk = Desk(rig);
        rig.Play();

        HoldPushToTalk(rig);

        Assert.True(desk.Active);
        Assert.Equal("Paused.", desk.Pause());

        EndTheTurn(rig);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
    }

    [Theory]
    [InlineData(StoryRunPause.PushToTalk)]
    [InlineData(StoryRunPause.Interrupted)]
    public void ABookPausedOnlyForSpeechIsNotAPausedBookToResume(StoryRunPause reason)
    {
        var rig = BookRig.InMemory();
        var desk = Desk(rig);
        rig.Play();

        rig.Player.Pause(reason);

        Assert.Equal("The book is already playing.", desk.Resume());
    }

    [Fact]
    public void ACommanderPauseIsAPausedBookToResume()
    {
        var rig = BookRig.InMemory();
        var desk = Desk(rig);
        rig.Play();

        desk.Pause();

        Assert.Equal("Resumed.", desk.Resume());
    }

    [Fact]
    public void PauseThePodcastOverPushToTalkLeavesItPausedAfterTheTurn()
    {
        var rig = BookRig.InMemory();
        var podcasts = new PodcastPlayer(
            rig.Audio,
            new AmbientMusic(rig.Audio, () => CueLibrary.Load()),
            episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")),
            new MemoryFileSystem(),
            @"C:\d47\data\podcasts\listening.json",
            NullLogger<PodcastPlayer>.Instance,
            rig.Background.Enqueue);
        var episode = new PodcastEpisode(
            "e1",
            "Episode one",
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            new Uri("https://cdn.example.test/e1.mp3"),
            "audio/mpeg",
            null,
            TimeSpan.FromHours(1));
        _ = podcasts.Play(new Uri("https://example.test/feed.xml"), new PodcastShow("Deep Space Hour", [episode]), episode);
        rig.Settle();

        rig.Audio.Listening = true;
        rig.Audio.TurnInFlight = true;

        Assert.Equal("Podcast paused.", podcasts.Pause());

        rig.Audio.Listening = false;
        rig.Audio.TurnInFlight = false;
        rig.Settle();

        Assert.True(podcasts.State!.PausedByCommander);
        Assert.Contains(rig.Sink.Started.Last(request => request.Name == "e1").Id, rig.Sink.Paused);
    }
}
