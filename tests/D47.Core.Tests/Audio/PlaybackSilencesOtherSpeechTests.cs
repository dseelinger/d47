using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class PlaybackSilencesOtherSpeechTests
{
    private const string Run = "story-run";

    private static AudioClip Clip(string name) =>
        new(name, new byte[AudioFormat.Standard.SampleRate / 5 * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static AudioRequest On(AudioChannel channel, string name, string? group = null) =>
        new() { Channel = channel, Clip = Clip(name), Group = group, Caption = name };

    private static List<string> Names(RecordingAudioSink sink) =>
        [.. sink.Started.Select(request => request.Name).Where(name => name != "spoken-gap")];

    [Fact]
    public void SpeechInAnotherGroupNeverPlaysThenOrLater()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        arbiter.Enqueue(On(AudioChannel.Speech, "a reply", "turn-1"));
        arbiter.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));
        arbiter.Enqueue(On(AudioChannel.Speech, "a callout"));
        sink.PlayOutAll();

        arbiter.EndPlayback();
        sink.PlayOutAll();

        Assert.Equal(["line one"], Names(sink));
    }

    [Fact]
    public void TurningItOnDropsOtherSpeechAlreadyQueuedOrPlaying()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(On(AudioChannel.Speech, "a long reply", "turn-1"));
        arbiter.Enqueue(On(AudioChannel.Speech, "its second sentence", "turn-1"));
        var reply = sink.Started[0].Id;

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        sink.PlayOutAll();

        Assert.Contains(reply, sink.Stopped);
        Assert.Equal(["a long reply", "line one"], Names(sink));
    }

    [Fact]
    public void CuesStillPlay()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Cue, "listening"));

        Assert.Equal(["listening"], Names(sink));
    }

    [Fact]
    public void AnAlertPausesPlaybackAndPlays()
    {
        var (arbiter, sink) = Build();
        var pauses = 0;
        arbiter.PlaybackPaused += () => pauses++;

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        arbiter.Enqueue(On(AudioChannel.Speech, "line two", Run));
        var line = sink.Started[0].Id;

        arbiter.Enqueue(On(AudioChannel.Alert, "interdiction"));
        sink.PlayOutAll();

        Assert.Equal(1, pauses);
        Assert.Equal(PlaybackHold.Paused, arbiter.Playback);
        Assert.Contains(line, sink.Stopped);
        Assert.Equal(["line one", "interdiction"], Names(sink));
    }

    [Fact]
    public void AnUrgentCalloutsSilenceThenAlertPausesPlaybackRatherThanEndingIt()
    {
        var (arbiter, sink) = Build();
        var pauses = 0;
        arbiter.PlaybackPaused += () => pauses++;

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));

        arbiter.Silence();
        arbiter.Enqueue(On(AudioChannel.Alert, "hull critical"));
        sink.PlayOutAll();

        Assert.Equal(1, pauses);
        Assert.Equal(PlaybackHold.Paused, arbiter.Playback);
        Assert.Equal(["line one", "hull critical"], Names(sink));
    }

    [Fact]
    public void PausedSpeechPlaysAsBefore()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        var line = sink.Started[0].Id;

        arbiter.PausePlayback();
        arbiter.Enqueue(On(AudioChannel.Speech, "a reply", "turn-1"));
        sink.PlayOutAll();

        Assert.Contains(line, sink.Stopped);
        Assert.Equal(["line one", "a reply"], Names(sink));
    }

    [Fact]
    public void ALineOfThePlaybackQueuedWhilePausedNeverPlays()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.Silence();
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        sink.PlayOutAll();

        Assert.Empty(Names(sink));
    }

    [Fact]
    public void WithItOffSpeechPlaysAsBefore()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.EndPlayback();
        arbiter.Enqueue(On(AudioChannel.Speech, "a reply", "turn-1"));
        arbiter.Enqueue(On(AudioChannel.Overheard, "chatter", "comms"));
        sink.PlayOutAll();

        Assert.Equal(PlaybackHold.Off, arbiter.Playback);
        Assert.Equal(["a reply", "chatter"], Names(sink));
    }

    [Fact]
    public void ResumingDropsOtherSpeechAgain()
    {
        var (arbiter, sink) = Build();

        arbiter.HoldSpeechFor(Run);
        arbiter.PausePlayback();
        arbiter.Enqueue(On(AudioChannel.Speech, "a reply", "turn-1"));

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "a callout"));
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        sink.PlayOutAll();

        Assert.Equal(PlaybackHold.Playing, arbiter.Playback);
        Assert.Equal(["a reply", "line one"], Names(sink));
    }

    [Fact]
    public void PlayedIsRaisedOnlyForALineThatRanToItsEnd()
    {
        var (arbiter, sink) = Build();
        var played = new List<string>();
        arbiter.Played += request => played.Add(request.Name);

        arbiter.HoldSpeechFor(Run);
        arbiter.Enqueue(On(AudioChannel.Speech, "line one", Run));
        sink.CompleteCurrent();
        arbiter.Enqueue(On(AudioChannel.Speech, "line two", Run));
        arbiter.PausePlayback();
        sink.PlayOutAll();

        Assert.Equal(["line one"], played);
    }
}
