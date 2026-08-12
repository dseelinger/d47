using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AudioArbiterTests
{
    private static readonly CueLibrary Cues = CueLibrary.Load();

    private static AudioClip Clip(string name, double seconds = 0.2) =>
        new(name, new byte[(int)(AudioFormat.Standard.SampleRate * seconds) * 2], AudioFormat.Standard);

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        return (arbiter, sink);
    }

    private static AudioRequest Speech(string name, string? group = null) =>
        new() { Channel = AudioChannel.Speech, Clip = Clip(name), Group = group, Caption = name };

    [Fact]
    public void OneThingPlaysAtATimeAndTheRestWait()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Speech("first"));
        arbiter.Enqueue(Speech("second"));

        Assert.Single(sink.Started);
        Assert.Equal("first", sink.Started[0].Clip.Name);

        sink.CompleteCurrent();

        Assert.Equal(2, sink.Started.Count);
        Assert.Equal("second", sink.Started[1].Clip.Name);
    }

    [Fact]
    public void SentencesOfOneReplyKeepTheirOrder()
    {
        var (arbiter, sink) = Build();

        foreach (var sentence in new[] { "one", "two", "three", "four" })
        {
            arbiter.Enqueue(Speech(sentence, group: "turn-1"));
        }

        for (var i = 0; i < 3; i++)
        {
            sink.CompleteCurrent();
        }

        Assert.Equal(
            ["one", "two", "three", "four"],
            sink.Started.Select(request => request.Clip.Name));
    }

    [Fact]
    public void AnAlertSupersedesSpeechMidSentenceAndTheSpeechIsNotResumed()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Speech("a long answer"));
        var speechId = sink.Started[0].Id;

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("interdiction") });

        Assert.Contains(speechId, sink.Stopped);
        Assert.Equal("interdiction", sink.Started[1].Clip.Name);

        // The interrupted sentence is gone, not parked. Finishing the alert plays nothing.
        sink.CompleteCurrent();
        Assert.Equal(2, sink.Started.Count);
    }

    [Fact]
    public void SpeechDoesNotSupersedeAnAlreadyPlayingAlert()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("shields down") });
        arbiter.Enqueue(Speech("as I was saying"));

        Assert.Single(sink.Started);
        Assert.Empty(sink.Stopped);
    }

    [Fact]
    public void AQueuedAlertJumpsAheadOfQueuedSpeech()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Cue, Clip = Clip("holding the head") });
        arbiter.Enqueue(Speech("queued speech"));
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("queued alert") });

        sink.CompleteCurrent();

        Assert.Equal("queued alert", sink.Started[1].Clip.Name);
    }

    /// <summary>
    /// Ranking decides who goes next, not who gets cut off. A loop-state cue plays in the
    /// fifth of a second immediately before the speech it announces, so speech superseding on
    /// rank alone would truncate every cue the app ever plays.
    /// </summary>
    [Fact]
    public void SpeechWaitsForACueRatherThanTruncatingIt()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Cue, Clip = Clip("thinking") });
        var cueId = sink.Started[0].Id;

        arbiter.Enqueue(Speech("here is your answer"));

        Assert.DoesNotContain(cueId, sink.Stopped);
        Assert.Single(sink.Started);

        sink.CompleteCurrent();
        Assert.Equal("here is your answer", sink.Started[1].Clip.Name);
    }

    [Fact]
    public void TheBedDucksUnderSpeechAndComesBackUpAfterwards()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("hum", 3), Loop = true });
        var bedId = sink.Started[0].Id;
        Assert.Equal(1f, sink.GainOf(bedId));

        arbiter.Enqueue(Speech("answer"));
        Assert.True(sink.GainOf(bedId) < 1f, "the bed should duck once speech starts");

        sink.CompleteCurrent();
        Assert.Equal(1f, sink.GainOf(bedId));
    }

    [Fact]
    public void ACueDoesNotDuckTheBed()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("hum", 3), Loop = true });
        var bedId = sink.Started[0].Id;

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Cue, Clip = Clip("tick") });

        Assert.Equal(1f, sink.GainOf(bedId));
    }

    [Fact]
    public void TheBedPlaysWhileThinkingAndStopsOnTheWayOut()
    {
        var (arbiter, sink) = Build();

        arbiter.EnterState(LoopState.Thinking, Cues);
        Assert.True(arbiter.Activity.BedPlaying);
        Assert.Contains(sink.Started, request => request.Loop);

        arbiter.EnterState(LoopState.Answered, Cues);
        Assert.False(arbiter.Activity.BedPlaying);
    }

    [Fact]
    public void TheBedIsDroppedEvenWhenTheTurnEndsByFailing()
    {
        var (arbiter, sink) = Build();

        arbiter.EnterState(LoopState.Thinking, Cues);
        arbiter.EnterState(LoopState.Failed, Cues);

        Assert.False(arbiter.Activity.BedPlaying);
    }

    [Fact]
    public void EnteringAStatePlaysThatStatesOwnCue()
    {
        var (arbiter, sink) = Build();

        arbiter.EnterState(LoopState.Answered, Cues);

        Assert.Equal("answered", sink.Started[0].Clip.Name);
    }

    [Fact]
    public void SilenceStopsEverythingAndClearsWhatWasWaiting()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Bed, Clip = Clip("hum", 3), Loop = true });
        arbiter.Enqueue(Speech("one"));
        arbiter.Enqueue(Speech("two"));
        arbiter.Enqueue(Speech("three"));

        var startedBefore = sink.Started.Count;
        arbiter.Silence();

        Assert.Equal(1, sink.StopAllCount);
        Assert.False(arbiter.IsSpeaking);
        Assert.False(arbiter.Activity.BedPlaying);

        // Nothing that was waiting gets a turn afterwards.
        sink.CompleteCurrent();
        Assert.Equal(startedBefore, sink.Started.Count);
    }

    [Fact]
    public void SilenceAnnouncesItselfSoInFlightSynthesisCanBeAbandoned()
    {
        var (arbiter, _) = Build();
        var announced = 0;
        arbiter.Silenced += () => announced++;

        arbiter.Enqueue(Speech("one"));
        arbiter.Silence();

        Assert.Equal(1, announced);
    }

    [Fact]
    public void SilenceIsSafeWithNothingPlaying()
    {
        var (arbiter, sink) = Build();

        arbiter.Silence();

        Assert.Equal(1, sink.StopAllCount);
        Assert.False(arbiter.IsSpeaking);
    }

    [Fact]
    public void DroppingAGroupClearsThatTurnAndLeavesTheRestAlone()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Speech("old one", group: "turn-1"));
        arbiter.Enqueue(Speech("old two", group: "turn-1"));
        arbiter.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Alert, Clip = Clip("hull damage"), Group = "alerts",
        });

        arbiter.DropGroup("turn-1");

        Assert.Equal("hull damage", sink.Started[^1].Clip.Name);
        Assert.True(arbiter.IsSpeaking);
    }

    [Fact]
    public void ACompletionForSomethingAlreadySupersededIsIgnored()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Speech("interrupted"));
        var stale = sink.Started[0].Id;
        arbiter.Enqueue(new AudioRequest { Channel = AudioChannel.Alert, Clip = Clip("alert") });

        // The sink was already told to stop this one, but a real device can report the
        // completion anyway — the stop and the end-of-buffer race by design.
        sink.CompletePlayback(stale);

        Assert.True(arbiter.IsSpeaking);
        Assert.Equal("alert", arbiter.Activity.Channel is AudioChannel.Alert ? "alert" : "wrong");
    }

    [Fact]
    public void ActivityCarriesTheCaptionOfWhatIsBeingSpoken()
    {
        var (arbiter, sink) = Build();

        arbiter.Enqueue(Speech("Nine jumps to Colonia."));

        Assert.Equal("Nine jumps to Colonia.", arbiter.Activity.Caption);

        sink.CompleteCurrent();

        Assert.Null(arbiter.Activity.Caption);
    }
}
