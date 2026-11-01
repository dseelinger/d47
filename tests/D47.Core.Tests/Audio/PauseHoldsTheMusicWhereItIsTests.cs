using D47.Core.Storage;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The media keys and <c>manage_bench.Music</c>: pause, resume and next on the ambient music (#497).</summary>
[Trait("Category", "Integration")]
public class PauseHoldsTheMusicWhereItIsTests : IDisposable
{
    private readonly MusicBench _bench;

    public PauseHoldsTheMusicWhereItIsTests()
    {
        _bench = new MusicBench(
            "d47-music-transport-tests",
            (Situations.Docked, "berth"),
            (Situations.Docked, "hangar"),
            (Situations.Supercruise, "cruise"));
        _bench.Music.GameRunning(running: true);
    }

    public void Dispose() => _bench.Dispose();

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    [Fact]
    public void PauseHoldsTheTrackAndResumeContinuesTheSameOne()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var playing = Assert.Single(_bench.Sink.Started);

        _bench.Music.Control(MusicAction.Pause);

        Assert.Contains(playing.Id, _bench.Sink.Paused);
        Assert.Empty(_bench.Sink.Stopped);
        Assert.False(_bench.Music.State.Playing);
        Assert.True(_bench.Music.State.Paused);
        Assert.Equal(playing.Name, _bench.Music.State.Track);

        _bench.Music.Control(MusicAction.Resume);

        Assert.DoesNotContain(playing.Id, _bench.Sink.Paused);
        Assert.Single(_bench.Sink.Started);
        Assert.True(_bench.Music.State.Playing);
    }

    [Fact]
    public void NextStartsADifferentTrack()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var first = Assert.Single(_bench.Sink.Started);

        _bench.Music.Control(MusicAction.Next);

        Assert.Contains(first.Id, _bench.Sink.Stopped);
        Assert.Equal(2, _bench.Sink.Started.Count);
        Assert.NotEqual(first.Name, _bench.Sink.Started[1].Name);
    }

    [Fact]
    public void NextWhilePausedPlays()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.Control(MusicAction.Pause);

        _bench.Music.Control(MusicAction.Next);

        Assert.Equal(2, _bench.Sink.Started.Count);
        Assert.False(_bench.Music.Paused);
        Assert.True(_bench.Music.State.Playing);
    }

    [Fact]
    public void WhilePausedANewSituationStartsNothingAndResumeStartsItsTrack()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var docked = Assert.Single(_bench.Sink.Started);
        _bench.Music.Control(MusicAction.Pause);

        _bench.Music.Follow(InSupercruise, musicTrack: null);

        Assert.DoesNotContain(docked.Id, _bench.Sink.Stopped);
        Assert.Contains(docked.Id, _bench.Sink.Paused);
        Assert.Single(_bench.Sink.Started);
        Assert.Null(_bench.Music.State.Track);

        _bench.Music.Control(MusicAction.Resume);

        Assert.Equal(2, _bench.Sink.Started.Count);
        Assert.Contains("cruise", _bench.Sink.Started[1].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void PauseIsNotMute()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.Control(MusicAction.Pause);

        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = true });
        _bench.Music.MuteChanged(muted: true);
        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = false });
        _bench.Music.MuteChanged(muted: false);

        // Unmuting does not override the Commander's pause.
        Assert.Single(_bench.Sink.Started);
        Assert.True(_bench.Music.Paused);
    }

    [Fact]
    public void ResumeWhileMutedStartsNothing()
    {
        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = true });

        var said = _bench.Music.Control(MusicAction.Resume);

        Assert.Empty(_bench.Sink.Started);
        Assert.Contains("muted", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pause the music", "pause")]
    [InlineData("resume the music", "resume")]
    [InlineData("next track", "next")]
    [InlineData("skip track", "next")]
    public void ThePhrasesReachManageMusic(string phrase, string action)
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);

        var command = Assert.Single(tool.Commands, c => c.Phrase == phrase);
        Assert.Equal(action, command.Arguments["action"]);
    }

    [Fact]
    public void TheModelCanCallManageMusic()
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);

        Assert.False(tool.Protected);
    }

    [Fact]
    public async Task TheToolRunsTheAction()
    {
        MusicAction? ran = null;
        var tool = AudioCapability.Create(music: action =>
        {
            ran = action;
            return "Next track.";
        }).Tools.Single();

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string> { ["action"] = "next" }),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(MusicAction.Next, ran);
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
