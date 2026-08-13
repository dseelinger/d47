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

    /// <summary>
    /// A voice the provider refuses is dropped and the sentence spoken anyway.
    /// <para>
    /// Reported from a running build: a voice chosen while a different provider was selected
    /// stayed in settings across the switch, and every sentence of every turn was sent with it
    /// and refused. The cues need no voice and kept playing, so a provider that could not say
    /// one word read as d47 choosing to be quiet.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// And the id is handed out, because the repair is in settings rather than here. The
    /// pipeline lives for one turn; without this the next turn reads the same stored value and
    /// fails the same way, forever.
    /// </summary>
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

    /// <summary>
    /// Once for the turn, not once per sentence. Sentences render concurrently, so a reply of
    /// six would otherwise raise six complaints about one setting and send the refused voice
    /// five more times after it was known to be bad.
    /// </summary>
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

        // Sent once and never again. The count is the point: four sentences each carrying a
        // voice already known to be refused is the behaviour this replaced.
        Assert.Single(tts.Voices, voice => voice == "en-US-RogerNeural");

        // And nothing was lost on the way. Every sentence spoke, on the fallback voice.
        Assert.Equal(0, pipeline.Failures);
    }

    /// <summary>
    /// A provider that is simply down is not treated as a voice problem: the sentence is lost,
    /// the failure is reported as a failure, and nothing is written out of settings over it.
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
    /// The hazard the pipeline exists to prevent: sentences render concurrently and finish out
    /// of order, but a reply is only a reply in sequence.
    /// </summary>
    [Fact]
    public async Task SentencesReachTheQueueInOrderEvenWhenTheyRenderOutOfOrder()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider { Gated = true };
        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("First. Second. Third. ");

        await tts.WaitForRequest("Third.");

        // All three are in flight at once — that is the concurrency. Now finish them
        // backwards, which is what a variable-latency provider does in practice.
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

        // Nothing has been released, so nothing has been enqueued — yet all three were asked
        // for. Rendering ahead of playback is the whole latency win.
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

    /// <summary>
    /// Shut up has to reach synthesis, not just the queue. A sentence still rendering when the
    /// Commander says stop would otherwise arrive a moment later and speak into the silence.
    /// </summary>
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
