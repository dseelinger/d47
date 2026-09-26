using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A track that ends on its own is followed by two seconds of silence before the next (#522).</summary>
public class SongsAreTwoSecondsApartTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-music-gap-tests",
        Guid.NewGuid().ToString("n"));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;

    public SongsAreTwoSecondsApartTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();

        var library = Library(
            (Situations.Docked, "berth"),
            (Situations.Docked, "hangar"),
            (Situations.Supercruise, "cruise"));

        _music = new AmbientMusic(_audio, () => library, new Random(7));
        _audio.MusicFinished += _music.TrackFinished;
    }

    public void Dispose()
    {
        _audio.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    [Fact]
    public void AFinishedTrackIsFollowedByTwoSecondsOfSilenceThenTheNext()
    {
        _music.Follow(Docked, musicTrack: null);
        var first = Assert.Single(_sink.Started);

        _sink.CompletePlayback(first.Id);

        var gap = _sink.Started[1];
        Assert.Null(gap.Track);
        var clip = Assert.IsType<AudioClip>(gap.Clip);
        Assert.Equal(AudioArbiter.MusicGap, clip.Duration);
        Assert.Equal(-1, clip.Pcm.Span.IndexOfAnyExcept((byte)0));
        Assert.True(_audio.Activity.MusicPlaying);

        _sink.CompletePlayback(gap.Id);

        var second = _sink.Started[2];
        Assert.NotNull(second.Track);
        Assert.NotEqual(first.Name, second.Name);
        Assert.Equal(second.Name, _music.State.Track);
    }

    [Fact]
    public void TheFirstTrackInASituationHasNoGap()
    {
        _music.Follow(Docked, musicTrack: null);

        Assert.NotNull(Assert.Single(_sink.Started).Track);
    }

    [Fact]
    public void ASituationChangeDuringTheGapStartsTheNewTrackAtOnce()
    {
        _music.Follow(Docked, musicTrack: null);
        _sink.CompletePlayback(_sink.Started[0].Id);
        var gap = _sink.Started[1];

        _music.Follow(InSupercruise, musicTrack: null);

        Assert.Contains(gap.Id, _sink.Stopped);
        Assert.Equal(3, _sink.Started.Count);
        Assert.Contains("cruise", _sink.Started[2].Name, StringComparison.Ordinal);
        Assert.NotNull(_sink.Started[2].Track);
    }

    [Fact]
    public void SilenceDuringTheGapStartsNothingAfterIt()
    {
        _music.Follow(Docked, musicTrack: null);
        _sink.CompletePlayback(_sink.Started[0].Id);
        var gap = _sink.Started[1];

        _audio.Silence();
        _sink.CompletePlayback(gap.Id);

        Assert.Equal(2, _sink.Started.Count);
        Assert.False(_audio.Activity.MusicPlaying);
    }

    [Fact]
    public void MutingDuringTheGapStartsNothingAfterIt()
    {
        _music.Follow(Docked, musicTrack: null);
        _sink.CompletePlayback(_sink.Started[0].Id);
        var gap = _sink.Started[1];

        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = true });
        _music.MuteChanged(muted: true);
        _sink.CompletePlayback(gap.Id);

        Assert.Contains(gap.Id, _sink.Stopped);
        Assert.Equal(2, _sink.Started.Count);
    }

    [Fact]
    public void NextDuringTheGapSkipsTheWait()
    {
        _music.Follow(Docked, musicTrack: null);
        _sink.CompletePlayback(_sink.Started[0].Id);
        var gap = _sink.Started[1];

        _music.Control(MusicAction.Next);

        Assert.Contains(gap.Id, _sink.Stopped);
        Assert.NotNull(_sink.Started[2].Track);
    }

    [Fact]
    public void AStoppedTrackIsNotFollowedByAGap()
    {
        _music.Follow(Docked, musicTrack: null);

        _music.Control(MusicAction.Next);

        Assert.All(_sink.Started, request => Assert.NotNull(request.Track));
    }

    [Fact]
    public void PausingDuringTheGapHoldsIt()
    {
        _music.Follow(Docked, musicTrack: null);
        _sink.CompletePlayback(_sink.Started[0].Id);
        var gap = _sink.Started[1];

        _music.Control(MusicAction.Pause);

        Assert.Contains(gap.Id, _sink.Paused);

        _music.Control(MusicAction.Resume);
        _sink.CompletePlayback(gap.Id);

        Assert.Equal(3, _sink.Started.Count);
        Assert.NotNull(_sink.Started[2].Track);
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };

    private CueLibrary Library(params (string Situation, string Track)[] tracks)
    {
        foreach (var (situation, track) in tracks)
        {
            var path = Path.Combine(_root, FolderAudioSource.MusicFolder, situation, $"{track}.wav");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, WavWriter.ToBytes(new byte[480], AudioFormat.Standard));
        }

        return CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(_root, NullLogger<FolderAudioSource>.Instance));
    }
}
