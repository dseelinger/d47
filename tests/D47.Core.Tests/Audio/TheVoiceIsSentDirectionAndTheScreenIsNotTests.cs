using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The pipeline half of <see cref="AudioTags"/>: what actually goes on the wire, and what actually
/// reaches the caption (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <see cref="DirectionReachesOnlyAVoiceThatPerformsItTests"/> proves the rule; this proves it is
/// wired. They are different failures — a strip that works and is never called is exactly the shape
/// of bug that ships.
/// </para>
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

    /// <summary>
    /// The whole point. Flash reads "sighs" out loud, so a provider that does not perform
    /// direction must never be handed any.
    /// </summary>
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

    /// <summary>
    /// <b>And the screen never sees it either way</b> — the maintainer's ruling of 2026-09-04.
    /// Direction is an instruction to the voice; a Commander reading <c>[sighs]</c> in a caption is
    /// reading stage notes.
    /// <para>
    /// Asserted on the caption rather than on the clip's name, which is a different thing and
    /// belongs to the provider: a provider that performs direction is <em>sent</em> the brackets, so
    /// it names its clip with them. The pipeline carries the written form separately, and that is
    /// what the caption, the transcript and the said-record are built from.
    /// </para>
    /// </summary>
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
