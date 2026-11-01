using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Leaving a music folder shelves its track, and coming back continues it (#2016).</summary>
public class AFolderLeftAndReturnedToContinuesItsTrackTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public AFolderLeftAndReturnedToContinuesItsTrackTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    [Fact]
    public void GoingBackToTheGalaxyMapContinuesItsFirstPlayback()
    {
        var music = Music(("galaxy-map", "chart"), ("galaxy-map", "plot"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = Assert.Single(_sink.Started);

        music.Follow(Docked, musicTrack: null, Start.AddMinutes(1));
        var berth = _sink.Started[1];

        Assert.Contains(map.Id, _sink.Held);
        Assert.DoesNotContain(map.Id, _sink.Stopped);

        music.Follow(Docked, "GalaxyMap", Start.AddMinutes(2));

        Assert.Equal(2, _sink.Started.Count);
        Assert.DoesNotContain(map.Id, _sink.Held);
        Assert.Contains(berth.Id, _sink.Held);
        Assert.Equal(map.Name, music.State.Track);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void TwoSituationsFallingBackToGeneralShareOnePlayback()
    {
        var music = Music((Situations.General, "hum"));

        music.Follow(Docked, musicTrack: null, Start);
        var hum = Assert.Single(_sink.Started);

        music.Follow(InSupercruise, musicTrack: null, Start.AddMinutes(1));

        Assert.Single(_sink.Started);
        Assert.Empty(_sink.Paused);
        Assert.Empty(_sink.Stopped);
        Assert.Equal(hum.Name, music.State.Track);
    }

    [Fact]
    public void AFifthShelvedTrackDropsTheOneShelvedLongestAgo()
    {
        var music = Music(
            ("galaxy-map", "chart"),
            ("system-map", "orrery"),
            ("codex", "entry"),
            ("scanner", "sweep"),
            (Situations.Docked, "berth"),
            (Situations.Supercruise, "cruise"));

        music.Follow(Docked, "GalaxyMap", Start);
        music.Follow(Docked, "SystemMap", Start);
        music.Follow(Docked, "Codex", Start);
        music.Follow(Docked, "SystemAndSurfaceScanner", Start);
        music.Follow(Docked, musicTrack: null, Start);

        Assert.Empty(_sink.Stopped);

        music.Follow(InSupercruise, musicTrack: null, Start);

        var first = _sink.Started[0];
        Assert.Equal([first.Id], _sink.Stopped);

        music.Follow(Docked, "GalaxyMap", Start);

        Assert.Equal(7, _sink.Started.Count);
        Assert.Contains("chart", _sink.Started[6].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ATrackShelvedForOverThirtyMinutesIsNotContinued()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);

        music.Follow(Docked, "GalaxyMap", Start.AddMinutes(31));

        Assert.Contains(map.Id, _sink.Stopped);
        Assert.Equal(3, _sink.Started.Count);
        Assert.NotEqual(map.Id, _sink.Started[2].Id);
    }

    [Fact]
    public void AStaleShelvedTrackIsStoppedWithoutGoingBack()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);

        music.Follow(Docked, musicTrack: null, Start.AddMinutes(31));

        Assert.Equal([map.Id], _sink.Stopped);
    }

    [Fact]
    public void EliteClosingStopsEveryShelvedTrack()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        music.Follow(Docked, musicTrack: null, Start);

        music.GameRunning(running: false);

        Assert.Equal(_sink.Started.Select(request => request.Id).Order(), _sink.Stopped.Order());
    }

    [Fact]
    public void MutingStopsEveryShelvedTrack()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        music.Follow(Docked, musicTrack: null, Start);

        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = true });
        music.MuteChanged(muted: true);

        Assert.Equal(_sink.Started.Select(request => request.Id).Order(), _sink.Stopped.Order());
    }

    [Fact]
    public void OnlyTheLiveTrackEndingStartsTheNext()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);
        var berth = _sink.Started[1];

        _sink.CompletePlayback(map.Id);

        Assert.Equal(2, _sink.Started.Count);
        Assert.Equal(berth.Name, music.State.Track);

        _sink.CompletePlayback(berth.Id);

        Assert.Equal(3, _sink.Started.Count);
        Assert.Null(_sink.Started[2].Track);
    }

    [Fact]
    public void ResumeAfterAChangeWhilePausedContinuesTheNewFoldersShelvedTrack()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);
        var berth = _sink.Started[1];
        music.Control(MusicAction.Pause);

        music.Follow(Docked, "GalaxyMap", Start);

        Assert.Equal(2, _sink.Started.Count);
        Assert.Contains(berth.Id, _sink.Paused);
        Assert.Contains(map.Id, _sink.Held);

        Assert.Equal("Music resumed.", music.Control(MusicAction.Resume));

        Assert.Equal(2, _sink.Started.Count);
        Assert.DoesNotContain(map.Id, _sink.Held);
        Assert.Contains(berth.Id, _sink.Paused);
        Assert.Equal(map.Name, music.State.Track);
    }

    [Fact]
    public void AContinuedTrackComesBackAtTheCurrentLevel()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);

        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Level = 0.2 });
        music.Follow(Docked, "GalaxyMap", Start);

        Assert.Equal(0.2f, _sink.GainOf(map.Id)!.Value, 3);
    }

    private AmbientMusic Music(params (string Folder, string Track)[] tracks)
    {
        const string root = @"C:\d47-music";
        var files = new MemoryFileSystem();

        foreach (var (folder, track) in tracks)
        {
            files.WriteBytes(
                Path.Combine(root, FolderAudioSource.MusicFolder, folder, $"{track}.wav"),
                WavWriter.ToBytes(new byte[480], AudioFormat.Standard));
        }

        var library = CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(root, files, NullLogger<FolderAudioSource>.Instance));

        var music = new AmbientMusic(_audio, () => library, new Random(7));
        _audio.MusicFinished += music.TrackFinished;
        music.GameRunning(running: true);
        return music;
    }

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
