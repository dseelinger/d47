using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A podcast has its own slot, pauses while D47 listens or speaks, and steps back when it resumes (#2653).</summary>
public class APodcastPausesWhileD47SpeaksTests
{
    private static readonly TimeSpan Position = TimeSpan.FromMinutes(12);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;

    public APodcastPausesWhileD47SpeaksTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate / 5 * 2], AudioFormat.Standard);

    private static MusicTrack Episode(string name = "episode") => new(name, () => throw new NotSupportedException());

    private long Play(TimeSpan? from = null)
    {
        _audio.PlayPodcast(Episode(), from ?? TimeSpan.Zero);
        var id = _sink.Started.Last(request => request.Track is not null).Id;
        _sink.Progresses[id] = new TrackProgress(Position, TimeSpan.FromHours(1));
        return id;
    }

    private static AudioRequest On(AudioChannel channel, string name = "line", string? group = null) =>
        new() { Channel = channel, Clip = Clip(name), Group = group };

    private long PlayingId(string name) => _sink.Started.Last(request => request.Name == name).Id;

    private bool Paused(long podcast) => _sink.Paused.Contains(podcast);

    [Fact]
    public void SpeechPausesItAndItStepsBackTwoSecondsWhenTheSpeechEnds()
    {
        var podcast = Play();

        _audio.Enqueue(On(AudioChannel.Speech, "reply"));
        Assert.True(Paused(podcast));

        _sink.CompletePlayback(PlayingId("reply"));

        Assert.False(Paused(podcast));
        Assert.Equal((podcast, Position - TimeSpan.FromSeconds(2)), _sink.Seeks.Last());
        var log = _sink.Log.ToList();
        Assert.True(log.FindLastIndex(line => line.StartsWith($"seek {podcast} ", StringComparison.Ordinal)) < log.LastIndexOf($"resume {podcast}"));
    }

    [Fact]
    public void AnAlertPausesItTheSameWay()
    {
        var podcast = Play();

        _audio.Enqueue(On(AudioChannel.Alert, "danger"));
        Assert.True(Paused(podcast));

        _sink.CompletePlayback(PlayingId("danger"));

        Assert.False(Paused(podcast));
        Assert.Equal((podcast, Position - TimeSpan.FromSeconds(2)), _sink.Seeks.Last());
    }

    [Fact]
    public void ItHoldsThroughEveryQueuedLineAndResumesOnceAfterTheLast()
    {
        var podcast = Play();

        _audio.Enqueue(On(AudioChannel.Speech, "first", "turn"));
        _audio.Enqueue(On(AudioChannel.Speech, "second", "turn"));

        _sink.CompletePlayback(PlayingId("first"));
        Assert.True(Paused(podcast));
        Assert.Empty(_sink.Seeks);

        _sink.CompletePlayback(PlayingId("second"));
        Assert.False(Paused(podcast));
        Assert.Single(_sink.Seeks);
    }

    [Fact]
    public void ItStepsBackNoFurtherThanTheStart()
    {
        var podcast = Play();
        _sink.Progresses[podcast] = new TrackProgress(TimeSpan.FromSeconds(1), null);

        _audio.Listening = true;
        _audio.Listening = false;

        Assert.Equal((podcast, TimeSpan.Zero), _sink.Seeks.Last());
    }

    [Fact]
    public void TheMicrophoneHoldsIt()
    {
        var podcast = Play();

        _audio.Listening = true;
        Assert.True(Paused(podcast));

        _audio.Listening = false;
        Assert.False(Paused(podcast));
    }

    [Fact]
    public void ATurnInFlightHoldsIt()
    {
        var podcast = Play();

        _audio.TurnInFlight = true;
        Assert.True(Paused(podcast));

        _audio.TurnInFlight = false;
        Assert.False(Paused(podcast));
    }

    [Fact]
    public void AStoryPlaybackHoldsItUntilPausedOrEnded()
    {
        var podcast = Play();

        _audio.HoldSpeechFor("story");
        Assert.True(Paused(podcast));

        _audio.PausePlayback();
        Assert.False(Paused(podcast));

        _audio.HoldSpeechFor("story");
        Assert.True(Paused(podcast));

        _audio.EndPlayback();
        Assert.False(Paused(podcast));
    }

    [Fact]
    public void TheCommandersPauseSurvivesEveryHoldClearing()
    {
        var podcast = Play();
        Assert.True(_audio.PausePodcast());

        _audio.Listening = true;
        _audio.TurnInFlight = true;
        _audio.HoldSpeechFor("story");
        _audio.Enqueue(On(AudioChannel.Alert, "danger"));

        _audio.Listening = false;
        _audio.TurnInFlight = false;
        _audio.EndPlayback();
        _sink.CompletePlayback(PlayingId("danger"));

        Assert.True(Paused(podcast));
        Assert.DoesNotContain($"resume {podcast}", _sink.Log);
        Assert.False(_audio.PodcastPlaying);

        Assert.True(_audio.ResumePodcast());
        Assert.False(Paused(podcast));
    }

    [Fact]
    public void ResumingWhileAHoldIsOnWaitsForTheHold()
    {
        var podcast = Play();
        _audio.PausePodcast();
        _audio.Listening = true;

        _audio.ResumePodcast();
        Assert.True(Paused(podcast));

        _audio.Listening = false;
        Assert.False(Paused(podcast));
    }

    [Fact]
    public void OverheardChatterIsRefusedWhileItPlaysAndAcceptedWhileTheCommanderHasPausedIt()
    {
        Play();

        _audio.Enqueue(On(AudioChannel.Overheard, "chatter"));
        Assert.DoesNotContain(_sink.Started, request => request.Name == "chatter");

        _audio.PausePodcast();
        _audio.Enqueue(On(AudioChannel.Overheard, "chatter"));
        Assert.Contains(_sink.Started, request => request.Name == "chatter");
    }

    [Fact]
    public void StopKeepsThePositionAsTheCommandersPause()
    {
        var podcast = Play();
        _audio.Enqueue(On(AudioChannel.Speech, "reply"));

        _audio.Silence();

        Assert.True(Paused(podcast));
        Assert.DoesNotContain(podcast, _sink.Stopped);
        Assert.Empty(_sink.Seeks);
        Assert.False(_audio.PodcastPlaying);
        Assert.Equal(Position, _audio.PodcastProgress()!.Value.Elapsed);
    }

    [Fact]
    public void AnEpisodeStartedFromAPositionSeeksThere()
    {
        var from = TimeSpan.FromMinutes(5);

        var podcast = Play(from);

        Assert.Equal((podcast, from), Assert.Single(_sink.Seeks));
        Assert.True(_audio.PodcastPlaying);
    }

    [Fact]
    public void AnEpisodeStartedDuringAHoldStartsPaused()
    {
        _audio.Listening = true;

        var podcast = Play();

        Assert.True(Paused(podcast));
    }

    [Fact]
    public void TheEpisodeEndingRaisesPodcastFinishedAndEmptiesTheSlot()
    {
        var podcast = Play();
        var finished = 0;
        _audio.PodcastFinished += () => finished++;

        _sink.CompletePlayback(podcast);

        Assert.Equal(1, finished);
        Assert.False(_audio.PodcastPlaying);
        Assert.Null(_audio.PodcastProgress());
    }

    [Fact]
    public void APodcastIsNotQueued()
    {
        Assert.Throws<ArgumentException>(() => _audio.Enqueue(On(AudioChannel.Podcast)));
    }

    [Fact]
    public void ItPlaysAtThePodcastLevel()
    {
        _audio.Mix = AudioMix.Default with { Podcast = new ChannelMix(0.3, Muted: false, DuckUnderSpeech: 1) };

        var podcast = Play();

        Assert.Equal(0.3f, _sink.GainOf(podcast)!.Value, 3);
    }

    [Fact]
    public void ShelvingTheMusicLeavesThePodcastSlotAlone()
    {
        var music = Music(("galaxy-map", "chart"), (Situations.Docked, "berth"));
        var start = new DateTimeOffset(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);
        var docked = new GameStatus { Flags = StatusFlags.Docked, ReadAt = start };

        music.Follow(docked, "GalaxyMap", start);
        var podcast = Play();

        music.Follow(docked, musicTrack: null, start.AddMinutes(1));
        music.Follow(docked, "GalaxyMap", start.AddMinutes(2));
        _audio.StopMusic();

        Assert.DoesNotContain(podcast, _sink.Stopped);
        Assert.False(Paused(podcast));
        Assert.DoesNotContain(podcast, _sink.Held);
        Assert.True(_audio.PodcastPlaying);
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
        music.GameRunning(running: true);
        return music;
    }
}
