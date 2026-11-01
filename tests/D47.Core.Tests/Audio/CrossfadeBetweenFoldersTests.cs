using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A change of music folder fades between the two tracks (#2017).</summary>
public class CrossfadeBetweenFoldersTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public CrossfadeBetweenFoldersTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    [Theory]
    [InlineData("galaxy-map", "docked")]
    [InlineData("docked", "galaxy-map")]
    [InlineData("main-menu", "codex")]
    [InlineData("cqc-menu", "supercruise")]
    [InlineData("scanner", "combat-large")]
    [InlineData("squadrons", "system-map")]
    [InlineData("powerplay", "fleet-carrier")]
    public void EitherFolderBeingAMapOrMenuMakesItThreeQuartersOfASecond(string from, string to)
    {
        Assert.Equal(TimeSpan.FromSeconds(0.75), Crossfade.Between(from, to));
    }

    [Theory]
    [InlineData("docked", "combat-dogfight")]
    [InlineData("supercruise", "combat-srv")]
    [InlineData("normal-space", "interdiction")]
    public void IntoCombatOrAnInterdictionIsHalfASecond(string from, string to)
    {
        Assert.Equal(TimeSpan.FromSeconds(0.5), Crossfade.Between(from, to));
    }

    [Theory]
    [InlineData("docked", "supercruise")]
    [InlineData("combat-dogfight", "docked")]
    [InlineData("interdiction", "normal-space")]
    [InlineData("general", "on-foot")]
    public void EverythingElseIsThreeSeconds(string from, string to)
    {
        Assert.Equal(TimeSpan.FromSeconds(3), Crossfade.Between(from, to));
    }

    [Fact]
    public void AChangeOfFolderFadesTheOldTrackOutWithAHoldAndTheNewOneInOverTheSameTime()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Supercruise, "cruise"));

        music.Follow(Docked, musicTrack: null, Start);
        var berth = Assert.Single(_sink.Started);

        music.Follow(InSupercruise, musicTrack: null, Start);
        var cruise = _sink.Started[1];

        var length = Crossfade.Between(Situations.Docked, Situations.Supercruise);
        Assert.Contains((berth.Id, 0f, length, true), _sink.Fades);
        Assert.Contains((cruise.Id, 1f, length, false), _sink.Fades);
        Assert.Empty(_sink.Paused);
        Assert.Empty(_sink.Stopped);
    }

    [Fact]
    public void ContinuingAShelvedTrackFadesItInOverTheSameTime()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));

        music.Follow(Docked, "GalaxyMap", Start);
        var map = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, Start);
        music.Follow(Docked, "GalaxyMap", Start);

        var length = Crossfade.Between("galaxy-map", Situations.Docked);
        Assert.Equal((map.Id, 1f, length, false), _sink.Fades.Last());
        Assert.DoesNotContain(map.Id, _sink.Held);
    }

    [Fact]
    public void TheFirstTrackStartsWithoutAFade()
    {
        var music = Music((Situations.Docked, "berth"));

        music.Follow(Docked, musicTrack: null, Start);

        Assert.Empty(_sink.Fades);
    }

    [Fact]
    public void ADuckDuringAFadeRelevelsBothTracks()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Supercruise, "cruise"));

        music.Follow(Docked, musicTrack: null, Start);
        var berth = _sink.Started[0];
        music.Follow(InSupercruise, musicTrack: null, Start);
        var cruise = _sink.Started[1];

        _audio.Enqueue(new AudioRequest { Channel = AudioChannel.Speech, Clip = Clip("line") });

        var ducked = _audio.Mix.Music.GainWhile(true);
        Assert.True(ducked < 1f);
        Assert.Equal(ducked, _sink.GainOf(berth.Id)!.Value, 3);
        Assert.Equal(ducked, _sink.GainOf(cruise.Id)!.Value, 3);
    }

    [Fact]
    public void AFadingOutTrackThatEndsOnItsOwnStartsNothing()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Supercruise, "cruise"));
        var finished = 0;
        _audio.MusicFinished += () => finished++;

        music.Follow(Docked, musicTrack: null, Start);
        var berth = _sink.Started[0];
        music.Follow(InSupercruise, musicTrack: null, Start);
        var cruise = _sink.Started[1];

        _sink.CompletePlayback(berth.Id);

        Assert.Equal(0, finished);
        Assert.Equal(2, _sink.Started.Count);
        Assert.Equal(cruise.Name, music.State.Track);

        music.Follow(Docked, musicTrack: null, Start);

        Assert.Equal(3, _sink.Started.Count);
        Assert.NotNull(_sink.Started[2].Track);
    }

    [Fact]
    public void NextIsImmediateAndNotFaded()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Docked, "hangar"));

        music.Follow(Docked, musicTrack: null, Start);
        music.Control(MusicAction.Next);

        Assert.Empty(_sink.Fades);
        Assert.Single(_sink.Stopped);
    }

    private static AudioClip Clip(string name) => new(name, new byte[480], AudioFormat.Standard);

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
