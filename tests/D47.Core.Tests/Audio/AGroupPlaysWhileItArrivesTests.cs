using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary><see cref="SpeechPipeline"/> over a provider whose clips arrive in parts.</summary>
public class AGroupPlaysWhileItArrivesTests
{
    private static readonly byte[] Pcm = [.. Enumerable.Range(0, 4800).Select(i => (byte)i)];

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    [Fact]
    public async Task AnUncolouredGroupIsQueuedBeforeItsLastByteArrives()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider();

        await using var pipeline = new SpeechPipeline(arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance);

        pipeline.Push("You are in Sol. ");
        var clip = await tts.ClipFor("You are in Sol.");
        clip.Append(Pcm.AsSpan(0, 100));

        await StreamingTtsProvider.Eventually(() => sink.Started.Count == 1, "the group was never queued");

        Assert.Same(clip, sink.Started[0].Arriving);
        Assert.Null(sink.Started[0].Clip);
        Assert.False(clip.IsComplete);

        clip.Append(Pcm.AsSpan(100));
        clip.Complete();
        await pipeline.CompleteAsync();
    }

    [Fact]
    public async Task AColouredGroupIsQueuedAsAClipOnlyOnceItIsWhole()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            colour: clip => clip with { Name = "coloured" });

        pipeline.Push("You are in Sol. ");
        var clip = await tts.ClipFor("You are in Sol.");
        clip.Append(Pcm);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(sink.Started);

        clip.Complete();
        await pipeline.CompleteAsync();

        var played = Assert.Single(sink.Started);
        Assert.Null(played.Arriving);
        Assert.Equal("coloured", played.Clip!.Name);
    }

    [Fact]
    public async Task GroupsPlayInOrderWhenTheSecondArrivesFirst()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider { AcceptGated = true };

        await using var pipeline = new SpeechPipeline(arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance);

        pipeline.Push("First. Second. ");
        await tts.WaitForRequest("Second.");

        tts.Accept("Second.");
        var second = await tts.ClipFor("Second.");
        second.Append(Pcm);
        second.Complete();

        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(sink.Started);

        tts.Accept("First.");
        var first = await tts.ClipFor("First.");
        first.Append(Pcm);
        first.Complete();

        await pipeline.CompleteAsync();
        sink.PlayOutAll();

        Assert.Equal(["First.", "Second."], sink.Started.Select(request => request.Name));
    }

    [Fact]
    public async Task AbandoningCancelsAClipMidArrivalAndQueuesNothingMore()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider { AcceptGated = true };

        await using var pipeline = new SpeechPipeline(arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance);

        pipeline.Push("First. Second. ");
        await tts.WaitForRequest("First.");
        tts.Accept("First.");

        var first = await tts.ClipFor("First.");
        first.Append(Pcm.AsSpan(0, 100));
        await StreamingTtsProvider.Eventually(() => sink.Started.Count == 1, "the first group was never queued");

        pipeline.Abandon();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Whole);

        await tts.WaitForRequest("Second.");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Single(sink.Started);
    }

    [Fact]
    public async Task AVoiceRefusedBeforeTheStreamStartsIsDroppedAndTheGroupIsStillSpoken()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider { Refuses = "refused-voice" };

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("refused-voice"),
            "turn-1",
            NullLogger.Instance);

        var reported = new List<string>();
        pipeline.VoiceRejected += reported.Add;

        pipeline.Push("You are in Sol. ");
        var clip = await tts.ClipFor("You are in Sol.");
        clip.Append(Pcm);
        clip.Complete();
        await pipeline.CompleteAsync();

        Assert.Equal(["refused-voice"], reported);
        Assert.Equal(["refused-voice", null], tts.Voices);
        Assert.Same(clip, Assert.Single(sink.Started).Arriving);
    }

    [Fact]
    public async Task CompletingWaitsForTheClipAndKeepsExactlyWhatArrived()
    {
        var (arbiter, _) = Build();
        var tts = new StreamingTtsProvider();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            keep: true);

        pipeline.Push("You are in Sol. ");
        var clip = await tts.ClipFor("You are in Sol.");
        clip.Append(Pcm.AsSpan(0, 333));

        var completing = pipeline.CompleteAsync();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(completing.IsCompleted);

        clip.Append(Pcm.AsSpan(333, 1001));
        clip.Append(Pcm.AsSpan(1334));
        clip.Complete();
        await completing;

        Assert.Equal(Pcm, Assert.Single(pipeline.Kept!.Parts).Pcm.ToArray());
    }

    [Fact]
    public async Task TheRecorderIsToldWhenTheFirstAudioArrived()
    {
        var (arbiter, _) = Build();
        var tts = new StreamingTtsProvider();
        var notes = new List<SynthesisNote>();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            noted: notes.Add);

        pipeline.Push("You are in Sol. ");
        var clip = await tts.ClipFor("You are in Sol.");
        clip.Append(Pcm.AsSpan(0, 100));
        await Task.Delay(30, TestContext.Current.CancellationToken);
        clip.Append(Pcm.AsSpan(100));
        clip.Complete();
        await pipeline.CompleteAsync();

        await StreamingTtsProvider.Eventually(() => notes.Count == 1, "nothing was noted");

        var note = notes[0];
        Assert.NotNull(note.FirstAudio);
        Assert.True(note.FirstAudio < note.Elapsed, $"first audio {note.FirstAudio} is not before the whole clip {note.Elapsed}");
    }

    [Fact]
    public async Task AWholeClipNotesNoFirstAudio()
    {
        var (arbiter, _) = Build();
        var notes = new List<SynthesisNote>();

        await using var pipeline = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            noted: notes.Add);

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        Assert.Null(Assert.Single(notes).FirstAudio);
    }
}
