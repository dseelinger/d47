using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ten minutes with nothing happening fades the music out and holds it; activity fades it back in (#2019).</summary>
public class TheMusicFadesOutWhenNothingHappensTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TenMinutesOn = Start + IdleWatch.Threshold;

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public TheMusicFadesOutWhenNothingHappensTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    [Fact]
    public void TenMinutesOfStationChatterFadesTheTrackOutOverTenSecondsAndHoldsIt()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = Assert.Single(_sink.Started);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddSeconds(-1), [Event("ReceiveText"), Event("NpcCrewPaidWage")]);
        Assert.Empty(_sink.Fades);

        music.Follow(Docked, musicTrack: null, TenMinutesOn, [Event("Music")]);

        Assert.Equal((berth.Id, 0f, TimeSpan.FromSeconds(10), true), Assert.Single(_sink.Fades));
        Assert.Contains(berth.Id, _sink.Held);
        Assert.Empty(_sink.Stopped);
        Assert.Equal(new MusicState(berth.Name, Playing: false, Paused: false, Idle: true), music.State);
    }

    [Fact]
    public void AnEventFadesItBackInOverThreeSecondsFromWhereItHeld()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(5), [Event("MarketBuy")]);

        Assert.Single(_sink.Started);
        Assert.DoesNotContain(berth.Id, _sink.Held);
        Assert.Equal((berth.Id, 1f, TimeSpan.FromSeconds(3), false), _sink.Fades[^1]);
        Assert.True(music.State.Playing);
        Assert.False(music.State.Idle);
    }

    [Fact]
    public void ATrackHeldWhileIdleIsNotDroppedAtThirtyMinutes()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(45), []);
        Assert.Empty(_sink.Stopped);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(46), [Event("Undocked")]);

        Assert.Single(_sink.Started);
        Assert.Equal(berth.Name, music.State.Track);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void AVoiceTurnResetsTheClock()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);

        music.TurnStarted();
        music.Follow(Docked, musicTrack: null, Start.AddMinutes(9), []);
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        Assert.Empty(_sink.Fades);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void AVoiceTurnWhileIdleFadesTheTrackBackIn()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        music.TurnStarted();
        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(1), []);

        Assert.Equal((berth.Id, 1f, TimeSpan.FromSeconds(3), false), _sink.Fades[^1]);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void AChangeOfFolderResetsTheClock()
    {
        var music = Music((Situations.Docked, "berth"), (Situations.Supercruise, "cruise"));
        music.Follow(Docked, musicTrack: null, Start, []);

        music.Follow(InSupercruise, musicTrack: null, Start.AddMinutes(9), []);
        var fades = _sink.Fades.Count;
        music.Follow(InSupercruise, musicTrack: null, TenMinutesOn, []);

        Assert.Equal(fades, _sink.Fades.Count);
        Assert.True(music.State.Playing);
    }

    [Fact]
    public void PlayOnTheKeyboardWhileIdleBringsTheTrackBack()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        Assert.Equal("Music resumed.", music.Control(MusicAction.Resume));

        Assert.Equal((berth.Id, 1f, TimeSpan.FromSeconds(3), false), _sink.Fades[^1]);
        Assert.True(music.State.Playing);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddSeconds(1), []);

        Assert.True(music.State.Playing);
        Assert.Equal(2, _sink.Fades.Count);
    }

    [Fact]
    public void ATrackThatEndsWhileIdleStartsNothingUntilActivity()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Follow(Docked, musicTrack: null, TenMinutesOn, []);

        _sink.CompletePlayback(berth.Id);
        Assert.Single(_sink.Started);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(1), [Event("Undocked")]);

        var next = _sink.Started[1];
        Assert.Equal((next.Id, 1f, TimeSpan.FromSeconds(3), false), _sink.Fades[^1]);
    }

    [Fact]
    public void ACommandersPauseIsNotLiftedByActivity()
    {
        var music = Music((Situations.Docked, "berth"));
        music.Follow(Docked, musicTrack: null, Start, []);
        var berth = _sink.Started[0];
        music.Control(MusicAction.Pause);
        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(1), []);

        music.Follow(Docked, musicTrack: null, TenMinutesOn.AddMinutes(2), [Event("Undocked")]);

        Assert.Contains(berth.Id, _sink.Paused);
        Assert.Empty(_sink.Fades);
        Assert.True(music.State.Paused);
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

    private static JournalEvent Event(string kind) =>
        new(Start, kind, JsonDocument.Parse($$"""{"event":"{{kind}}"}""").RootElement.Clone());

    private static GameStatus Status(StatusFlags flags) =>
        new() { Flags = flags, ReadAt = DateTimeOffset.UnixEpoch };
}
