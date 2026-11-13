using D47.Core.Storage;
using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A track that ends on its own is followed by two seconds of silence before the next (#522).</summary>
[Trait("Category", "Integration")]
public class SongsAreTwoSecondsApartTests : IDisposable
{
    private readonly MusicBench _bench;

    public SongsAreTwoSecondsApartTests()
    {
        _bench = new MusicBench(
            "d47-music-gap-tests",
            (Situations.Docked, "berth"),
            (Situations.Docked, "hangar"),
            (Situations.Supercruise, "cruise"));
        _bench.Music.GameRunning(running: true);
    }

    public void Dispose() => _bench.Dispose();

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    [Fact]
    public void AFinishedTrackIsFollowedByTwoSecondsOfSilenceThenTheNext()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        var first = Assert.Single(_bench.Sink.Started);

        _bench.Sink.CompletePlayback(first.Id);

        var gap = _bench.Sink.Started[1];
        Assert.Null(gap.Track);
        var clip = Assert.IsType<AudioClip>(gap.Clip);
        Assert.Equal(AudioArbiter.MusicGap, clip.Duration);
        Assert.Equal(-1, clip.Pcm.Span.IndexOfAnyExcept((byte)0));
        Assert.True(_bench.Audio.Activity.MusicPlaying);

        _bench.Sink.CompletePlayback(gap.Id);

        var second = _bench.Sink.Started[2];
        Assert.NotNull(second.Track);
        Assert.NotEqual(first.Name, second.Name);
        Assert.Equal(second.Name, _bench.Music.State.Track);
    }

    [Fact]
    public void TheFirstTrackInASituationHasNoGap()
    {
        _bench.Music.Follow(Docked, musicTrack: null);

        Assert.NotNull(Assert.Single(_bench.Sink.Started).Track);
    }

    [Fact]
    public void ASituationChangeDuringTheGapStartsTheNewTrackAtOnce()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Sink.CompletePlayback(_bench.Sink.Started[0].Id);
        var gap = _bench.Sink.Started[1];

        _bench.Music.Follow(InSupercruise, musicTrack: null);

        Assert.Contains(gap.Id, _bench.Sink.Stopped);
        Assert.Equal(3, _bench.Sink.Started.Count);
        Assert.Contains("cruise", _bench.Sink.Started[2].Name, StringComparison.Ordinal);
        Assert.NotNull(_bench.Sink.Started[2].Track);
    }

    [Fact]
    public void SilenceDuringTheGapStillPlaysTheNextTrackAfterIt()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Sink.CompletePlayback(_bench.Sink.Started[0].Id);
        var gap = _bench.Sink.Started[1];

        _bench.Audio.Silence();

        Assert.DoesNotContain(gap.Id, _bench.Sink.Stopped);

        _bench.Sink.CompletePlayback(gap.Id);

        Assert.Equal(3, _bench.Sink.Started.Count);
        Assert.NotNull(_bench.Sink.Started[2].Track);
        Assert.True(_bench.Audio.Activity.MusicPlaying);
    }

    [Fact]
    public void MutingDuringTheGapStartsNothingAfterIt()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Sink.CompletePlayback(_bench.Sink.Started[0].Id);
        var gap = _bench.Sink.Started[1];

        _bench.Audio.Mix = _bench.Audio.Mix.With(AudioChannel.Music, _bench.Audio.Mix.Music with { Muted = true });
        _bench.Music.MuteChanged(muted: true);
        _bench.Sink.CompletePlayback(gap.Id);

        Assert.Contains(gap.Id, _bench.Sink.Stopped);
        Assert.Equal(2, _bench.Sink.Started.Count);
    }

    [Fact]
    public void NextDuringTheGapSkipsTheWait()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Sink.CompletePlayback(_bench.Sink.Started[0].Id);
        var gap = _bench.Sink.Started[1];

        _bench.Music.Control(MusicAction.Next);

        Assert.Contains(gap.Id, _bench.Sink.Stopped);
        Assert.NotNull(_bench.Sink.Started[2].Track);
    }

    [Fact]
    public void AStoppedTrackIsNotFollowedByAGap()
    {
        _bench.Music.Follow(Docked, musicTrack: null);

        _bench.Music.Control(MusicAction.Next);

        Assert.All(_bench.Sink.Started, request => Assert.NotNull(request.Track));
    }

    [Fact]
    public void PausingDuringTheGapHoldsIt()
    {
        _bench.Music.Follow(Docked, musicTrack: null);
        _bench.Sink.CompletePlayback(_bench.Sink.Started[0].Id);
        var gap = _bench.Sink.Started[1];

        _bench.Music.Control(MusicAction.Pause);

        Assert.Contains(gap.Id, _bench.Sink.Paused);

        _bench.Music.Control(MusicAction.Resume);
        _bench.Sink.CompletePlayback(gap.Id);

        Assert.Equal(3, _bench.Sink.Started.Count);
        Assert.NotNull(_bench.Sink.Started[2].Track);
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
