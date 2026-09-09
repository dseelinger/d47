using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class SpeechPipelineTests
{
    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static SpeechPipeline Pipeline(AudioArbiter arbiter, ITtsProvider tts, string group = "turn-1") =>
        new(arbiter, tts, VoiceSelection.Default, group, NullLogger.Instance);

    [Fact]
    public async Task AVoiceTheProviderRefusesIsDroppedAndTheSentenceIsStillSpoken()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider { Refuses = "en-US-RogerNeural" };

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("en-US-RogerNeural"),
            "turn-1",
            NullLogger.Instance);

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        Assert.Equal("You are in Sol.", sink.Started[0].Clip.Name);
        Assert.Equal([("en-US-RogerNeural"), null], tts.Voices);
    }

    /// <summary>And the id is handed out, because the repair is in settings rather than here.</summary>
    [Fact]
    public async Task TheRefusedVoiceIsReportedSoItCanBeWrittenOutOfSettings()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { Refuses = "en-US-RogerNeural" };

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("en-US-RogerNeural"),
            "turn-1",
            NullLogger.Instance);

        var reported = new List<string>();
        pipeline.VoiceRejected += reported.Add;

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        Assert.Equal(["en-US-RogerNeural"], reported);
    }

    /// <summary>Once for the turn, not once per sentence.</summary>
    [Fact]
    public async Task ALongReplyComplainsAboutTheVoiceOnce()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { Refuses = "en-US-RogerNeural" };

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("en-US-RogerNeural"),
            "turn-1",
            NullLogger.Instance);

        var reported = new List<string>();
        pipeline.VoiceRejected += reported.Add;

        pipeline.Push("First. Second. Third. Fourth. ");
        await pipeline.CompleteAsync();

        Assert.Single(reported);

        // Sent once and never again.
        Assert.Single(tts.Voices, voice => voice == "en-US-RogerNeural");

        // And nothing was lost on the way.
        Assert.Equal(0, pipeline.Failures);
    }

    /// <summary>
    /// A provider that is simply down is not treated as a voice problem: the sentence is lost, the
    /// failure is reported as a failure, and nothing is written out of settings over it.
    /// </summary>
    [Fact]
    public async Task AProviderThatIsDownIsNotMistakenForABadVoice()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { FailOn = "Sol" };

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("en-US-RogerNeural"),
            "turn-1",
            NullLogger.Instance);

        var reported = new List<string>();
        pipeline.VoiceRejected += reported.Add;

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        Assert.Empty(reported);
        Assert.Equal(1, pipeline.Failures);
    }

    [Fact]
    public async Task SpeechStartsAtTheFirstSentenceRatherThanAtEndOfTurn()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("You are in Sol. ");

        // Enough to have reached the arbiter, with the rest of the reply not yet written.
        await tts.WaitForRequest("You are in Sol.");
        await pipeline.CompleteAsync();

        Assert.Equal("You are in Sol.", sink.Started[0].Clip.Name);
    }

    /// <summary>
    /// The hazard the pipeline exists to prevent: sentences render concurrently and finish out of
    /// order, but a reply is only a reply in sequence.
    /// </summary>
    [Fact]
    public async Task SentencesReachTheQueueInOrderEvenWhenTheyRenderOutOfOrder()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider { Gated = true };
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("First. Second. Third. ");

        await tts.WaitForRequest("Third.");

        // All three are in flight at once — that is the concurrency.
        tts.Release("Third.");
        tts.Release("Second.");
        tts.Release("First.");

        await pipeline.CompleteAsync();
        sink.PlayOutAll();

        Assert.Equal(
            ["First.", "Second.", "Third."],
            sink.Started.Select(request => request.Clip.Name));
    }

    [Fact]
    public async Task EverySentenceRendersConcurrentlyRatherThanWaitingItsTurn()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { Gated = true };
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("One. Two. Three. ");

        await tts.WaitForRequest("Three.");

        // Nothing has been released, so nothing has been enqueued — yet all three were asked for.
        Assert.Equal(3, tts.Requested.Count);

        tts.ReleaseAll();
        await pipeline.CompleteAsync();
    }

    [Fact]
    public async Task TheLastSentenceIsSpokenEvenWithoutATrailingSpace()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Docking granted.");
        await pipeline.CompleteAsync();

        Assert.Equal(["Docking granted."], sink.Started.Select(request => request.Clip.Name));
    }

    [Fact]
    public async Task SpeechCarriesItsOwnTextAsTheCaption()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Fuel is at 62 percent.");
        await pipeline.CompleteAsync();

        Assert.Equal("Fuel is at 62 percent.", arbiter.Activity.Caption);
        Assert.Single(sink.Started);
    }

    /// <summary>Shut up has to reach synthesis, not just the queue.</summary>
    [Fact]
    public async Task SilencingCancelsSynthesisThatHasNotFinishedYet()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider { Gated = true };
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("A long answer. With more to come. ");
        await tts.WaitForRequest("With more to come.");

        arbiter.Silence();

        // Even releasing the gate afterwards must not produce speech.
        tts.ReleaseAll();
        await pipeline.CompleteAsync();

        Assert.Empty(sink.Started);
        Assert.True(tts.Cancelled > 0, "in-flight synthesis should have been cancelled");
    }

    [Fact]
    public async Task AProviderFailureSkipsThatSentenceAndSaysSoRatherThanKillingTheTurn()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider { FailOn = "second" };
        await using var pipeline = Pipeline(arbiter, tts);

        var reported = new List<string>();
        pipeline.SynthesisFailed += reported.Add;

        pipeline.Push("The first one. The second one. The third one. ");
        await pipeline.CompleteAsync();

        sink.PlayOutAll();

        Assert.Equal(1, pipeline.Failures);
        Assert.Single(reported);
        Assert.Equal(
            ["The first one.", "The third one."],
            sink.Started.Select(request => request.Clip.Name));
    }

    [Fact]
    public async Task ANewTurnCanDropTheTailOfThePreviousOne()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();

        await using (var first = Pipeline(arbiter, tts, group: "turn-1"))
        {
            first.Push("Old one. Old two. Old three. ");
            await first.CompleteAsync();
        }

        var startedBefore = sink.Started.Count;
        arbiter.DropGroup("turn-1");

        sink.CompleteCurrent();
        Assert.Equal(startedBefore, sink.Started.Count);
    }

    [Fact]
    public async Task AnEmptyReplySpeaksNothing()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("   \n ");
        await pipeline.CompleteAsync();

        Assert.Empty(sink.Started);
        Assert.Empty(tts.Requested);
    }
}

/// <summary> What was spoken, and in whose voice, written into the log. </summary>
public class TheVoiceIsWrittenDownTests
{
    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    [Fact]
    public async Task TheVoiceAndTheSpeakerAreRecordedOnce()
    {
        var (arbiter, _) = Build();
        var log = new CapturingLogger();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            new VoiceSelection("en-GB-RyanNeural"),
            "announcement",
            log,
            speaker: "Ilse Bruhn");

        // Three sentences, one voice.
        pipeline.Push("Hold position. This is a routine scan. Do not deploy hardpoints. ");
        await pipeline.CompleteAsync();

        var said = Assert.Single(log.Lines, line => line.Contains("Spoken by", StringComparison.Ordinal));

        Assert.Contains("Ilse Bruhn", said, StringComparison.Ordinal);
        Assert.Contains("en-GB-RyanNeural", said, StringComparison.Ordinal);
    }

    /// <summary>A voice the provider refused did not speak, so what is written down is the one that did.</summary>
    [Fact]
    public async Task ARefusedVoiceIsNotRecordedAsHavingSpoken()
    {
        var (arbiter, _) = Build();
        var log = new CapturingLogger();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider { Refuses = "en-US-RogerNeural" },
            new VoiceSelection("en-US-RogerNeural"),
            "turn-1",
            log,
            speaker: "D47");

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        var said = Assert.Single(log.Lines, line => line.Contains("Spoken by", StringComparison.Ordinal));

        Assert.DoesNotContain("en-US-RogerNeural", said, StringComparison.Ordinal);
        Assert.Contains("provider's own voice", said, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
