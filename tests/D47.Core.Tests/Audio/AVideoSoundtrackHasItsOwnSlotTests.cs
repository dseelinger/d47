using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A video's soundtrack streams in its own slot on the Video channel, ducks under speech, and keeps chatter off while it plays (#2668).</summary>
public sealed class AVideoSoundtrackHasItsOwnSlotTests
{
    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public AVideoSoundtrackHasItsOwnSlotTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    private static MusicTrack Soundtrack(string name = "film") => new(name, () => throw new NotSupportedException());

    private static AudioRequest On(AudioChannel channel, string name) =>
        new() { Channel = channel, Clip = new AudioClip(name, new byte[960], AudioFormat.Standard) };

    [Fact]
    public void ItStreamsAtTheVideoLevelAndSeeksToWhereItStarts()
    {
        var id = _audio.PlayVideo(Soundtrack(), TimeSpan.FromMinutes(3));

        var started = Assert.Single(_sink.Started);
        Assert.Equal(id, started.Id);
        Assert.NotNull(started.Track);
        Assert.Equal(AudioMix.Default.Video.GainWhile(false), started.Gain, 3);
        Assert.Equal((id, TimeSpan.FromMinutes(3)), Assert.Single(_sink.Seeks));
    }

    [Fact]
    public void SpeechDucksItAndItComesBackAfter()
    {
        var id = _audio.PlayVideo(Soundtrack(), TimeSpan.Zero);

        _audio.Enqueue(On(AudioChannel.Speech, "reply"));
        Assert.Equal(AudioMix.Default.Video.GainWhile(true), _sink.GainChanges.Last(change => change.Id == id).Gain, 3);

        _sink.CompleteCurrent();
        Assert.Equal(AudioMix.Default.Video.GainWhile(false), _sink.GainChanges.Last(change => change.Id == id).Gain, 3);
    }

    [Fact]
    public void PauseResumeSeekAndStopReachTheSoundtrack()
    {
        var id = _audio.PlayVideo(Soundtrack(), TimeSpan.Zero);
        _sink.Progresses[id] = new TrackProgress(TimeSpan.FromSeconds(42), TimeSpan.FromHours(1));

        Assert.True(_audio.PauseVideo());
        Assert.Contains(id, _sink.Paused);
        Assert.True(_audio.ResumeVideo());
        Assert.DoesNotContain(id, _sink.Paused);
        Assert.True(_audio.SeekVideo(TimeSpan.FromMinutes(10)));
        Assert.Equal(TimeSpan.FromSeconds(42), _audio.VideoProgress()!.Value.Elapsed);

        _audio.StopVideo();

        Assert.Contains(id, _sink.Stopped);
        Assert.Null(_audio.VideoProgress());
        Assert.False(_audio.PauseVideo());
    }

    [Fact]
    public void ItsEndIsReportedWithItsId()
    {
        var id = _audio.PlayVideo(Soundtrack(), TimeSpan.Zero);
        long? ended = null;
        _audio.VideoFinished += finished => ended = finished;

        _sink.CompletePlayback(id);

        Assert.Equal(id, ended);
        Assert.False(_audio.ResumeVideo());
    }

    [Fact]
    public void ChatterIsRefusedWhileAVideoIsWatched()
    {
        _audio.VideoPlays(true);
        _audio.Enqueue(On(AudioChannel.Overheard, "chatter"));
        Assert.Empty(_sink.Started);

        _audio.VideoPlays(false);
        _audio.Enqueue(On(AudioChannel.Overheard, "chatter"));
        Assert.Single(_sink.Started);
    }

    [Fact]
    public void ASoundtrackIsNotQueued()
    {
        var thrown = Assert.Throws<ArgumentException>(() => _audio.Enqueue(On(AudioChannel.Video, "film")));
        Assert.Contains("PlayVideo", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AmbientMusicSaysItWaitsForAVideo()
    {
        var music = new AmbientMusic(_audio, () => CueLibrary.Load());

        music.Yield(MusicYield.Video, yielding: true);

        Assert.Equal("Ambient music waits while a video plays.", music.Control(MusicAction.Next));
    }
}
