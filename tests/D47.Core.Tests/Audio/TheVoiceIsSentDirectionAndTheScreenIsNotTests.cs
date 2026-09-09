using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The pipeline half of <see cref="AudioTags"/>: what actually goes on the wire, and what actually
/// reaches the caption.
/// </summary>
public class TheVoiceIsSentDirectionAndTheScreenIsNotTests
{
    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static SpeechPipeline Pipeline(AudioArbiter arbiter, ITtsProvider tts) =>
        new(arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance);

    /// <summary>The central case.</summary>
    [Fact]
    public async Task AProviderThatWouldReadItAloudIsNeverSentIt()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { ReadsAudioTags = false };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("[sighs] That is the third interdiction this hour. ");
        await pipeline.CompleteAsync();

        Assert.Equal(["That is the third interdiction this hour."], tts.Requested);
    }

    [Fact]
    public async Task AProviderThatPerformsItIsSentItWhole()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { ReadsAudioTags = true };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("[sighs] That is the third interdiction this hour. ");
        await pipeline.CompleteAsync();

        Assert.Equal(["[sighs] That is the third interdiction this hour."], tts.Requested);
    }

    /// <summary>And the screen never sees it either way — the maintainer's ruling of 2026-09-04.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheCaptionCarriesTheWordsAndNotTheDirection(bool performs)
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { ReadsAudioTags = performs };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("[alarmed] Contact on the scanner. ");
        await pipeline.CompleteAsync();

        Assert.Equal("Contact on the scanner.", arbiter.Activity.Caption);
    }

    /// <summary>
    /// A line that is nothing but direction is dropped rather than sent as an empty request — a
    /// provider handed "" is a failed synthesis and a red banner over nothing at all.
    /// </summary>
    [Fact]
    public async Task ALineOfNothingButDirectionIsNotSpoken()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { ReadsAudioTags = true };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("[sighs] ");
        await pipeline.CompleteAsync();

        Assert.Empty(tts.Requested);
    }
}
