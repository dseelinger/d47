using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary><see cref="SpeechPipeline"/> with the COVAS reverb as a running filter.</summary>
public class TheCovasVoicePlaysWhileItArrivesTests
{
    private static readonly byte[] Pcm = PcmConverter.ToStandard(StandInVoice.Clip).Pcm[..48_000].ToArray();

    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static SpeechPipeline Covas(AudioArbiter arbiter, ITtsProvider tts) =>
        new(
            arbiter,
            tts,
            VoiceSelection.Default,
            "turn-1",
            NullLogger.Instance,
            colour: CovasVoice.Apply,
            guardianTreated: true,
            keep: true,
            running: CovasVoice.Filter);

    [Fact]
    public async Task AGroupIsQueuedThroughTheReverbBeforeItsSourceIsWhole()
    {
        var (arbiter, sink) = Build();
        var tts = new StreamingTtsProvider();

        await using var pipeline = Covas(arbiter, tts);

        pipeline.Push("You are in Sol. ");
        var source = await tts.ClipFor("You are in Sol.");
        source.Append(Pcm.AsSpan(0, 9_601));

        await StreamingTtsProvider.Eventually(() => sink.Started.Count == 1, "the group was never queued");

        var queued = sink.Started[0];
        Assert.Null(queued.Clip);
        Assert.NotNull(queued.Arriving);
        Assert.NotSame(source, queued.Arriving);
        Assert.False(source.IsComplete);

        source.Append(Pcm.AsSpan(9_601));
        source.Complete();
        await pipeline.CompleteAsync();

        var whole = new AudioClip("You are in Sol.", Pcm, AudioFormat.Standard);
        Assert.Equal(CovasVoice.Apply(whole).Pcm.ToArray(), Assert.Single(pipeline.Kept!.Parts).Pcm.ToArray());
    }

    [Fact]
    public async Task AClipThatArrivesWholeIsQueuedWholeThroughTheSameReverb()
    {
        var (arbiter, sink) = Build();
        var tts = new FakeTtsProvider();

        await using var pipeline = Covas(arbiter, tts);

        pipeline.Push("You are in Sol. ");
        await pipeline.CompleteAsync();

        var played = Assert.Single(sink.Started);
        Assert.Null(played.Arriving);

        var dry = await tts.SynthesizeAsync("You are in Sol.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        Assert.Equal(CovasVoice.Apply(dry).Pcm.ToArray(), played.Clip!.Pcm.ToArray());
    }
}
