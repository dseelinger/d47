using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The arbiter reports and seeks only the live track: not during the gap before it, not while its length is unknown,
/// and never a shelved one. A seek leaves pause, hold and gain alone, and counts as activity for the idle fade.
/// </summary>
public class TheLiveTrackReportsItsPositionAndSeeksTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch };

    private static readonly TrackProgress Known = new(TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(3));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public TheLiveTrackReportsItsPositionAndSeeksTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    [Fact]
    public void NoLiveTrackHasNoProgressAndCannotSeek()
    {
        Assert.Null(_audio.MusicProgress());
        Assert.False(_audio.SeekMusic(TimeSpan.FromSeconds(10)));
        Assert.Empty(_sink.Seeks);
    }

    [Fact]
    public void TheLiveTrackReportsWhatTheSinkCounts()
    {
        var id = Play();

        Assert.Equal(Known, _audio.MusicProgress());

        Assert.True(_audio.SeekMusic(TimeSpan.FromSeconds(90)));
        Assert.Equal((id, TimeSpan.FromSeconds(90)), Assert.Single(_sink.Seeks));
    }

    [Fact]
    public void TheGapBeforeATrackIsNothingPlayedAndCannotSeek()
    {
        _audio.PlayMusic(Track("next"), afterGap: true);
        _sink.Progresses[_sink.Started[0].Id] = Known;

        Assert.Equal(new TrackProgress(TimeSpan.Zero, null), _audio.MusicProgress());
        Assert.False(_audio.SeekMusic(TimeSpan.FromSeconds(10)));
        Assert.Empty(_sink.Seeks);
    }

    [Fact]
    public void ATrackWhoseLengthIsNotKnownCannotSeek()
    {
        _audio.PlayMusic(Track("unknown"));
        var id = _sink.Started[0].Id;
        _sink.Progresses[id] = new TrackProgress(TimeSpan.FromSeconds(5), null);

        Assert.Equal(new TrackProgress(TimeSpan.FromSeconds(5), null), _audio.MusicProgress());
        Assert.False(_audio.SeekMusic(TimeSpan.FromSeconds(10)));
        Assert.Empty(_sink.Seeks);
    }

    [Fact]
    public void AShelvedTrackIsNeverReportedOrSeeked()
    {
        Play();

        _audio.ShelveMusic(Start);

        Assert.Null(_audio.MusicProgress());
        Assert.False(_audio.SeekMusic(TimeSpan.FromSeconds(10)));
        Assert.Empty(_sink.Seeks);
    }

    [Fact]
    public void ASeekOnAPausedTrackLeavesItPausedAtItsGain()
    {
        var id = Play();
        _audio.PauseMusic();
        var before = _sink.Log.Count;

        Assert.True(_audio.SeekMusic(TimeSpan.FromSeconds(90)));

        Assert.Equal([$"seek {id} 90s"], _sink.Log.Skip(before));
        Assert.True(_audio.MusicHeld);
        Assert.Contains(id, _sink.Paused);
    }

    [Fact]
    public void ASeekOnATrackHeldAtTheEndOfAFadeLeavesItHeld()
    {
        var id = Play();
        _audio.FadeOutMusic(TimeSpan.FromSeconds(10));
        var before = _sink.Log.Count;

        Assert.True(_audio.SeekMusic(TimeSpan.FromSeconds(90)));

        Assert.Equal([$"seek {id} 90s"], _sink.Log.Skip(before));
        Assert.True(_audio.MusicHeld);
        Assert.Contains(id, _sink.Held);
    }

    [Fact]
    public void ASeekResetsTheIdleClock()
    {
        var music = Music();
        music.Follow(Docked, musicTrack: null, Start, []);
        _sink.Progresses[_sink.Started[0].Id] = Known;

        Assert.True(music.Seek(TimeSpan.FromSeconds(90)));
        music.Follow(Docked, musicTrack: null, Start.AddMinutes(9), []);
        music.Follow(Docked, musicTrack: null, Start + IdleWatch.Threshold, []);

        Assert.Empty(_sink.Fades);
        Assert.Equal(Known, music.Progress());
    }

    [Fact]
    public void ASeekWhileIdleFadesTheTrackBackIn()
    {
        var music = Music();
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        _sink.Progresses[berth.Id] = Known;
        music.Follow(Docked, musicTrack: null, Start + IdleWatch.Threshold, []);
        Assert.Contains(berth.Id, _sink.Held);

        Assert.True(music.Seek(TimeSpan.FromSeconds(90)));
        Assert.Contains(berth.Id, _sink.Held);

        music.Follow(Docked, musicTrack: null, Start + IdleWatch.Threshold + TimeSpan.FromMinutes(1), []);

        Assert.Equal((berth.Id, 1f, AmbientMusic.WakeFadeIn, false), _sink.Fades[^1]);
    }

    private long Play()
    {
        _audio.PlayMusic(Track("berth"));
        var id = _sink.Started[0].Id;
        _sink.Progresses[id] = Known;
        return id;
    }

    private static MusicTrack Track(string name) => new(name, () => throw new InvalidOperationException("not opened"));

    private AmbientMusic Music()
    {
        const string root = @"C:\d47-music";
        var files = new MemoryFileSystem();
        files.WriteBytes(
            Path.Combine(root, FolderAudioSource.MusicFolder, Situations.Docked, "berth.wav"),
            WavWriter.ToBytes(new byte[480], AudioFormat.Standard));

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(root, files, NullLogger<FolderAudioSource>.Instance));

        var music = new AmbientMusic(_audio, () => library, new Random(7));
        _audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);
        return music;
    }
}
