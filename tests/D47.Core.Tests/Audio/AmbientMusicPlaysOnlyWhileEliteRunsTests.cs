using D47.Core.Storage;
using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ambient music starts nothing on its own while Elite is not running, and stops when it exits (#536).</summary>
[Trait("Category", "Integration")]
public class AmbientMusicPlaysOnlyWhileEliteRunsTests : IDisposable
{
    private readonly MusicBench _bench;

    public AmbientMusicPlaysOnlyWhileEliteRunsTests()
    {
        _bench = new MusicBench(
            "d47-music-running-tests",
            (Situations.Docked, "berth"),
            (Situations.Docked, "hangar"),
            (Situations.General, "lobby"));
    }

    public void Dispose() => _bench.Dispose();

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch };

    private static readonly GameStatus Exited = new() { Flags = StatusFlags.None, ReadAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public void NothingPlaysUntilEliteIsRunning()
    {
        _bench.Music.Follow(Exited, musicTrack: "Exploration");

        Assert.Empty(_bench.Sink.Started);

        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);

        Assert.True(IsDocked(Assert.Single(_bench.Sink.Started).Name));
    }

    [Fact]
    public void EliteExitingStopsTheTrack()
    {
        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);
        var playing = Assert.Single(_bench.Sink.Started);

        _bench.Music.GameRunning(running: false);
        _bench.Music.Follow(Exited, musicTrack: "Starport");

        Assert.Contains(playing.Id, _bench.Sink.Stopped);
        Assert.Single(_bench.Sink.Started);
        Assert.Null(_bench.Music.State.Track);
        Assert.False(_bench.Music.State.Playing);
    }

    [Fact]
    public void ATrackThatFinishesAfterEliteExitsStartsNoOther()
    {
        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.GameRunning(running: false);

        _bench.Music.TrackFinished();

        Assert.Single(_bench.Sink.Started);
    }

    [Theory]
    [InlineData(MusicAction.Resume)]
    [InlineData(MusicAction.Previous)]
    [InlineData(MusicAction.Next)]
    public void ResumeNextAndPreviousStartTheGeneralFolderWhenNoFolderHasPlayed(MusicAction action)
    {
        _bench.Music.Control(action);

        Assert.Equal("lobby", Assert.Single(_bench.Sink.Started).Name);
    }

    [Fact]
    public void PauseStillPausesWithEliteClosed()
    {
        Assert.Equal("Music paused.", _bench.Music.Control(MusicAction.Pause));
        Assert.True(_bench.Music.Paused);
    }

    [Fact]
    public void UnmutingStartsNothingWithEliteClosed()
    {
        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = true });
        _bench.Music.MuteChanged(muted: true);
        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = false });
        _bench.Music.MuteChanged(muted: false);

        Assert.Empty(_bench.Sink.Started);
    }

    [Fact]
    public void EliteStartingAgainPlaysTheSituationItFinds()
    {
        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.GameRunning(running: false);

        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);

        Assert.Equal(2, _bench.Sink.Started.Count);
        Assert.True(IsDocked(_bench.Sink.Started[1].Name));
        Assert.True(_bench.Music.State.Playing);
    }

    [Fact]
    public void EliteStartingAgainWhilePausedStartsNothing()
    {
        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Music.Control(MusicAction.Pause);
        _bench.Music.GameRunning(running: false);

        _bench.Music.GameRunning(running: true);
        _bench.Music.Follow(Docked, musicTrack: null);

        Assert.Single(_bench.Sink.Started);
        Assert.True(_bench.Music.Paused);
    }

    private static bool IsDocked(string name) =>
        name.Contains("berth", StringComparison.Ordinal) || name.Contains("hangar", StringComparison.Ordinal);
}
