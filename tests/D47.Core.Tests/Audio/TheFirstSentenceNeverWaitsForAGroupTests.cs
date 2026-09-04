using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Sentences are gathered for a provider that asked, and never at the cost of the first one
/// (<see cref="ITtsProvider.GroupsSentencesUpTo"/>,
/// <a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>Why the grouping exists at all.</b> ElevenLabs v3 performs delivery direction, but only
/// reliably when it has room — their own guidance encourages over 250 characters, and d47's median
/// sentence is 34, measured over 1,520 real replies. Left alone the app sends exactly the shape the
/// model handles worst.
/// </para>
/// <para>
/// <b>Why it costs nothing.</b> The next group renders while the previous clip plays, and a v3
/// request returns three to four times more audio than it costs in waiting. The only place that
/// could go wrong is the front of a reply, where there is no clip playing yet — which is what the
/// first test here is about, and it is the Phase 5 latency win that is being protected.
/// </para>
/// </summary>
public class TheFirstSentenceNeverWaitsForAGroupTests
{
    private static (AudioArbiter Arbiter, RecordingAudioSink Sink) Build()
    {
        var sink = new RecordingAudioSink();
        return (new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start(), sink);
    }

    private static SpeechPipeline Pipeline(AudioArbiter arbiter, ITtsProvider tts) =>
        new(arbiter, tts, VoiceSelection.Default, "turn-1", NullLogger.Instance);

    /// <summary>
    /// <b>The rule that is not negotiable.</b> The first sentence goes with whatever has arrived,
    /// however little — waiting for a group to fill would put a whole group's worth of streaming in
    /// front of the first sound, which is the delay the sentence splitter exists to remove.
    /// </summary>
    [Fact]
    public async Task TheFirstSentenceLeavesAloneEvenThoughAGroupWasAskedFor()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { GroupsSentencesUpTo = 300 };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Contact on the scanner. ");

        Assert.Equal(["Contact on the scanner."], tts.Requested);

        await pipeline.CompleteAsync();
    }

    /// <summary>
    /// And everything after it is gathered, which is the whole point: a group is long enough for
    /// direction to land where a single sentence is not.
    /// </summary>
    [Fact]
    public async Task EverythingAfterItIsGathered()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { GroupsSentencesUpTo = 300 };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Contact on the scanner. ");
        pipeline.Push("It is holding station off the second planet. ");
        pipeline.Push("We have the angle on it. ");
        await pipeline.CompleteAsync();

        Assert.Equal(
            [
                "Contact on the scanner.",
                "It is holding station off the second planet. We have the angle on it.",
            ],
            tts.Requested);
    }

    /// <summary>
    /// A provider that asked for nothing is unchanged — which is four of the five, so this is the
    /// behaviour almost every Commander still gets.
    /// </summary>
    [Fact]
    public async Task AProviderThatDidNotAskStillGetsOneSentenceAtATime()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider();

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Contact on the scanner. ");
        pipeline.Push("It is holding station off the second planet. ");
        await pipeline.CompleteAsync();

        Assert.Equal(
            ["Contact on the scanner.", "It is holding station off the second planet."],
            tts.Requested);
    }

    /// <summary>
    /// A group closes when the next sentence would overflow it rather than being cut mid-thought,
    /// so the budget is a target and never a truncation.
    /// </summary>
    [Fact]
    public async Task ASentenceThatWouldOverflowClosesTheGroupInFrontOfIt()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { GroupsSentencesUpTo = 60 };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("One. ");
        pipeline.Push("Two two two two two two two two two two two two. ");
        pipeline.Push("Three three three three three three three three. ");
        await pipeline.CompleteAsync();

        Assert.Equal("One.", tts.Requested[0]);
        Assert.All(tts.Requested, sent => Assert.DoesNotContain("Two", sent[1..], StringComparison.Ordinal));
        Assert.Equal(3, tts.Requested.Count);
    }

    /// <summary>
    /// Nothing is left unsaid at the end of a turn. A group still gathering when the stream ends
    /// goes as it is — the flush is what closes it, and a reply that stopped mid-group would drop
    /// its own tail.
    /// </summary>
    [Fact]
    public async Task AGroupStillGatheringWhenTheTurnEndsIsStillSpoken()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { GroupsSentencesUpTo = 300 };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Contact on the scanner. ");
        pipeline.Push("It has not seen us yet.");
        await pipeline.CompleteAsync();

        Assert.Equal(
            ["Contact on the scanner.", "It has not seen us yet."],
            tts.Requested);
    }

    /// <summary>
    /// A caller that hands over its whole text at once — an announcement, the keyword router —
    /// has no first boundary to wait for, so it is gathered from the start rather than being sent
    /// a sentence at a time for no reason.
    /// </summary>
    [Fact]
    public async Task TextPushedAllAtOnceIsGatheredFromTheStart()
    {
        var (arbiter, _) = Build();
        var tts = new FakeTtsProvider { GroupsSentencesUpTo = 300 };

        await using var pipeline = Pipeline(arbiter, tts);

        pipeline.Push("Contact on the scanner. It has not seen us yet. ");
        await pipeline.CompleteAsync();

        Assert.Equal(["Contact on the scanner. It has not seen us yet."], tts.Requested);
    }
}
