using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The arrival and interdiction folders play one track, then hand back (#2018).</summary>
public class AnArrivalOrInterdictionTrackPlaysOnceTests
{
    private const string Arrival = "arrival-from-hyperspace";
    private const string Hyperspace = "DestinationFromHyperspace";

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static readonly GameStatus Docked = Status(StatusFlags.Docked);

    private static readonly GameStatus InSupercruise = Status(StatusFlags.Supercruise | StatusFlags.InMainShip);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public AnArrivalOrInterdictionTrackPlaysOnceTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    [Fact]
    public void TheOneShotFoldersAreTheTwoArrivalsAndInterdiction()
    {
        Assert.Equal(
            ["arrival-from-hyperspace", "arrival-from-supercruise", "interdiction"],
            Situations.OneShot.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ACueThatEliteLeavesAfterFiveSecondsPlaysToItsEndAndThenTheSupercruiseTrackContinues()
    {
        var music = Music((Situations.Supercruise, "cruise"), (Arrival, "arrive"));

        music.Follow(InSupercruise, null, Start);
        var cruise = _sink.Started[0];
        music.Follow(InSupercruise, Hyperspace, Start);
        var cue = _sink.Started[1];
        music.Follow(InSupercruise, "Supercruise", Start + TimeSpan.FromSeconds(5));

        Assert.Equal("arrive", music.State.Track);
        Assert.Equal(2, _sink.Started.Count);
        Assert.DoesNotContain(cue.Id, _sink.Stopped);

        _sink.CompletePlayback(cue.Id);

        Assert.Equal("cruise", music.State.Track);
        Assert.Equal(2, _sink.Started.Count);
        Assert.Contains($"resume {cruise.Id}", _sink.Log);
    }

    [Fact]
    public void ACueIsNotLooped()
    {
        var music = Music((Arrival, "arrive"), (Situations.General, "calm"));

        music.Follow(InSupercruise, Hyperspace, Start);
        var cue = _sink.Started[0];
        _sink.CompletePlayback(cue.Id);

        Assert.False(cue.Loop);
        Assert.Equal("calm", music.State.Track);
    }

    [Fact]
    public void ACueThatEliteStillNamesWhenItEndsIsNotPlayedAgain()
    {
        var music = Music((Situations.Supercruise, "cruise"), (Arrival, "arrive"));

        music.Follow(InSupercruise, null, Start);
        music.Follow(InSupercruise, Hyperspace, Start);
        _sink.CompletePlayback(_sink.Started[1].Id);
        music.Follow(InSupercruise, Hyperspace, Start + TimeSpan.FromSeconds(10));

        Assert.Equal(2, _sink.Started.Count);
        Assert.Equal("cruise", music.State.Track);
    }

    [Fact]
    public void ASituationThatChangedDuringTheCueIsPlayedWhenItEnds()
    {
        var music = Music(
            (Situations.Supercruise, "cruise"),
            (Situations.Docked, "berth"),
            (Arrival, "arrive"));

        music.Follow(InSupercruise, null, Start);
        music.Follow(InSupercruise, Hyperspace, Start);
        music.Follow(Docked, "Starport", Start + TimeSpan.FromSeconds(4));

        Assert.Equal("arrive", music.State.Track);

        _sink.CompletePlayback(_sink.Started[1].Id);

        Assert.Equal("berth", music.State.Track);
    }

    [Fact]
    public void AnInterdictionDuringAnArrivalCueReplacesItWithTheCrossfadeAndTheArrivalIsNotKept()
    {
        var music = Music(
            (Situations.Supercruise, "cruise"),
            (Arrival, "arrive"),
            ("interdiction", "ambush"));

        music.Follow(InSupercruise, null, Start);
        var cruise = _sink.Started[0];
        music.Follow(InSupercruise, Hyperspace, Start);
        var arrival = _sink.Started[1];
        music.Follow(InSupercruise, "Interdiction", Start + TimeSpan.FromSeconds(2));
        var interdiction = _sink.Started[2];

        Assert.Equal("ambush", music.State.Track);
        Assert.Contains((arrival.Id, 0f, Crossfade.Between(Arrival, "interdiction"), false), _sink.Fades);
        Assert.DoesNotContain(arrival.Id, _sink.Held);
        Assert.DoesNotContain(arrival.Id, _sink.Paused);

        _sink.CompletePlayback(interdiction.Id);

        Assert.Equal("cruise", music.State.Track);
        Assert.Contains($"resume {cruise.Id}", _sink.Log);
        Assert.Equal(3, _sink.Started.Count);
    }

    [Fact]
    public void ANinetySecondCueFadesOutAtSixtySeconds()
    {
        var music = Music((Situations.Supercruise, "cruise"), (Arrival, "arrive"));

        music.Follow(InSupercruise, null, Start);
        var cruise = _sink.Started[0];
        music.Follow(InSupercruise, Hyperspace, Start);
        var cue = _sink.Started[1];

        music.Follow(InSupercruise, Hyperspace, Start + TimeSpan.FromSeconds(59));
        Assert.Equal("arrive", music.State.Track);

        music.Follow(InSupercruise, Hyperspace, Start + TimeSpan.FromSeconds(60));

        Assert.Contains((cue.Id, 0f, Crossfade.Between(Arrival, Situations.Supercruise), false), _sink.Fades);
        Assert.Equal("cruise", music.State.Track);
        Assert.Contains($"resume {cruise.Id}", _sink.Log);

        _sink.CompletePlayback(cue.Id);

        Assert.Equal(2, _sink.Started.Count);
        Assert.Equal("cruise", music.State.Track);
    }

    [Fact]
    public void AnEmptyOneShotFolderChangesNothing()
    {
        var music = Music((Situations.Supercruise, "cruise"));

        music.Follow(InSupercruise, null, Start);
        music.Follow(InSupercruise, Hyperspace, Start + TimeSpan.FromSeconds(1));

        Assert.Single(_sink.Started);
        Assert.Equal("cruise", music.State.Track);
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
