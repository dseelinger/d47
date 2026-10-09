using D47.Core.Storage;
using D47.Core.Audio;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ambient music starts nothing while Elite is not running, and stops when it exits (#536).</summary>
[Trait("Category", "Integration")]
public class AmbientMusicPlaysOnlyWhileEliteRunsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-music-running-tests",
        Guid.NewGuid().ToString("n"));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;

    public AmbientMusicPlaysOnlyWhileEliteRunsTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();

        var library = Library(
            (Situations.Docked, "berth"),
            (Situations.Docked, "hangar"),
            (Situations.General, "lobby"));

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

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch };

    private static readonly GameStatus Exited = new() { Flags = StatusFlags.None, ReadAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public void NothingPlaysUntilEliteIsRunning()
    {
        _music.Follow(Exited, musicTrack: "Exploration");

        Assert.Empty(_sink.Started);

        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);

        Assert.True(IsDocked(Assert.Single(_sink.Started).Name));
    }

    [Fact]
    public void EliteExitingStopsTheTrack()
    {
        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        _music.GameRunning(running: false);
        _music.Follow(Exited, musicTrack: "Starport");

        Assert.Contains(playing.Id, _sink.Stopped);
        Assert.Single(_sink.Started);
        Assert.Null(_music.State.Track);
        Assert.False(_music.State.Playing);
    }

    [Fact]
    public void ATrackThatFinishesAfterEliteExitsStartsNoOther()
    {
        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);
        _music.GameRunning(running: false);

        _music.TrackFinished();

        Assert.Single(_sink.Started);
    }

    [Theory]
    [InlineData(MusicAction.Resume)]
    [InlineData(MusicAction.Next)]
    public void ResumeAndNextSayEliteIsNotRunning(MusicAction action)
    {
        var said = _music.Control(action);

        Assert.Equal("Elite is not running.", said);
        Assert.Empty(_sink.Started);
    }

    [Fact]
    public void PauseStillPausesWithEliteClosed()
    {
        Assert.Equal("Music paused.", _music.Control(MusicAction.Pause));
        Assert.True(_music.Paused);
    }

    [Fact]
    public void UnmutingStartsNothingWithEliteClosed()
    {
        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = true });
        _music.MuteChanged(muted: true);
        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = false });
        _music.MuteChanged(muted: false);

        Assert.Empty(_sink.Started);
    }

    [Fact]
    public void EliteStartingAgainPlaysTheSituationItFinds()
    {
        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);
        _music.GameRunning(running: false);

        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);

        Assert.Equal(2, _sink.Started.Count);
        Assert.True(IsDocked(_sink.Started[1].Name));
        Assert.True(_music.State.Playing);
    }

    [Fact]
    public void EliteStartingAgainWhilePausedStartsNothing()
    {
        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);
        _music.Control(MusicAction.Pause);
        _music.GameRunning(running: false);

        _music.GameRunning(running: true);
        _music.Follow(Docked, musicTrack: null);

        Assert.Single(_sink.Started);
        Assert.True(_music.Paused);
    }

    private static bool IsDocked(string name) =>
        name.Contains("berth", StringComparison.Ordinal) || name.Contains("hangar", StringComparison.Ordinal);

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
            new FolderAudioSource(_root, new DiskFileSystem(), NullLogger<FolderAudioSource>.Instance));
    }
}
