using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The media keys and <c>manage_music</c>: pause, resume and next on the ambient music (#497).</summary>
public class PauseHoldsTheMusicWhereItIsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-music-transport-tests",
        Guid.NewGuid().ToString("n"));

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;

    public PauseHoldsTheMusicWhereItIsTests()
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
    public void PauseHoldsTheTrackAndResumeContinuesTheSameOne()
    {
        _music.Follow(Docked, musicTrack: null);
        var playing = Assert.Single(_sink.Started);

        _music.Control(MusicAction.Pause);

        Assert.Contains(playing.Id, _sink.Paused);
        Assert.Empty(_sink.Stopped);
        Assert.False(_music.State.Playing);
        Assert.True(_music.State.Paused);
        Assert.Equal(playing.Name, _music.State.Track);

        _music.Control(MusicAction.Resume);

        Assert.DoesNotContain(playing.Id, _sink.Paused);
        Assert.Single(_sink.Started);
        Assert.True(_music.State.Playing);
    }

    [Fact]
    public void NextStartsADifferentTrack()
    {
        _music.Follow(Docked, musicTrack: null);
        var first = Assert.Single(_sink.Started);

        _music.Control(MusicAction.Next);

        Assert.Contains(first.Id, _sink.Stopped);
        Assert.Equal(2, _sink.Started.Count);
        Assert.NotEqual(first.Name, _sink.Started[1].Name);
    }

    [Fact]
    public void NextWhilePausedPlays()
    {
        _music.Follow(Docked, musicTrack: null);
        _music.Control(MusicAction.Pause);

        _music.Control(MusicAction.Next);

        Assert.Equal(2, _sink.Started.Count);
        Assert.False(_music.Paused);
        Assert.True(_music.State.Playing);
    }

    [Fact]
    public void WhilePausedANewSituationStartsNothingAndResumeStartsItsTrack()
    {
        _music.Follow(Docked, musicTrack: null);
        var docked = Assert.Single(_sink.Started);
        _music.Control(MusicAction.Pause);

        _music.Follow(InSupercruise, musicTrack: null);

        Assert.Contains(docked.Id, _sink.Stopped);
        Assert.Single(_sink.Started);
        Assert.Null(_music.State.Track);

        _music.Control(MusicAction.Resume);

        Assert.Equal(2, _sink.Started.Count);
        Assert.Contains("cruise", _sink.Started[1].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void PauseIsNotMute()
    {
        _music.Follow(Docked, musicTrack: null);
        _music.Control(MusicAction.Pause);

        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = true });
        _music.MuteChanged(muted: true);
        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = false });
        _music.MuteChanged(muted: false);

        // Unmuting does not override the Commander's pause.
        Assert.Single(_sink.Started);
        Assert.True(_music.Paused);
    }

    [Fact]
    public void ResumeWhileMutedStartsNothing()
    {
        _audio.Mix = _audio.Mix.With(AudioChannel.Music, _audio.Mix.Music with { Muted = true });

        var said = _music.Control(MusicAction.Resume);

        Assert.Empty(_sink.Started);
        Assert.Contains("muted", said, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pause the music", "pause")]
    [InlineData("resume the music", "resume")]
    [InlineData("next track", "next")]
    [InlineData("skip track", "next")]
    public void ThePhrasesReachManageMusic(string phrase, string action)
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);

        var command = Assert.Single(tool.Commands, c => c.Phrase == phrase);
        Assert.Equal(action, command.Arguments["action"]);
    }

    [Fact]
    public void TheModelCanCallManageMusic()
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.ManageMusicTool);

        Assert.False(tool.Protected);
    }

    [Fact]
    public async Task TheToolRunsTheAction()
    {
        MusicAction? ran = null;
        var tool = AudioCapability.Create(music: action =>
        {
            ran = action;
            return "Next track.";
        }).Tools.Single();

        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string> { ["action"] = "next" }),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(MusicAction.Next, ran);
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
