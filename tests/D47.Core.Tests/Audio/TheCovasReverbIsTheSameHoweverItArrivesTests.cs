using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The COVAS reverb as a running filter gives the same bytes however its input is cut.</summary>
public class TheCovasReverbIsTheSameHoweverItArrivesTests
{
    private static readonly AudioClip Speech = PcmConverter.ToStandard(StandInVoice.Clip);

    private static byte[] Whole(IPcmFilter filter, ReadOnlySpan<byte> pcm) => [.. filter.Push(pcm), .. filter.Finish()];

    /// <summary>Chunks of 1 to 4,999 bytes, odd sizes included, so frames are split across pushes.</summary>
    private static IEnumerable<int> RandomCuts(int length, int seed)
    {
        var random = new Random(seed);

        for (var at = 0; at < length;)
        {
            var size = Math.Min(length - at, random.Next(1, 5_000));
            yield return size;
            at += size;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(781)]
    [InlineData(2026)]
    public void AClipFedInRandomChunksIsByteIdenticalToTheClipFedWhole(int seed)
    {
        var pcm = Speech.Pcm.ToArray();
        var chunked = CovasVoice.Filter();
        var output = new List<byte>();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, seed))
        {
            output.AddRange(chunked.Push(pcm.AsSpan(at, size)));
            at += size;
        }

        output.AddRange(chunked.Finish());

        Assert.Equal(Whole(CovasVoice.Filter(), pcm), output.ToArray());
    }

    [Fact]
    public void ApplyIsTheFilterOverTheWholeClip() =>
        Assert.Equal(Whole(CovasVoice.Filter(), Speech.Pcm.Span), CovasVoice.Apply(Speech).Pcm.ToArray());

    [Fact]
    public void ApplyIsTheDryLengthPlusAnEightTenthsOfASecondTail() =>
        Assert.Equal(Speech.Pcm.Length + (48_000 * 8 / 10 * 2), CovasVoice.Apply(Speech).Pcm.Length);

    [Fact]
    public async Task AnArrivingClipThroughTheFilterEndsAsApplyWouldHaveIt()
    {
        var source = new ArrivingClip("line");
        var treated = source.Through(CovasVoice.Filter());
        var pcm = Speech.Pcm.ToArray();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, 47))
        {
            source.Append(pcm.AsSpan(at, size));
            at += size;
        }

        Assert.False(treated.IsComplete);

        source.Complete();

        Assert.Equal(CovasVoice.Apply(Speech).Pcm.ToArray(), (await treated.Whole).Pcm.ToArray());
    }

    [Fact]
    public async Task AnArrivingClipThatFailsFailsWhatIsThroughTheFilter()
    {
        var source = new ArrivingClip("line");
        var treated = source.Through(CovasVoice.Filter());

        source.Append(Speech.Pcm.Span[..4_800]);
        source.Fail(new TtsException("the provider stopped"));

        var error = await Assert.ThrowsAsync<TtsException>(() => treated.Whole);
        Assert.Equal("the provider stopped", error.Message);
    }

    [Fact]
    public async Task AnArrivingClipThatIsCancelledCancelsWhatIsThroughTheFilter()
    {
        var source = new ArrivingClip("line");
        var treated = source.Through(CovasVoice.Filter());

        source.Fail(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => treated.Whole);
    }

    [Fact]
    public void OnlyAStockCoreWithTheReverbOnRunsAFilter()
    {
        Assert.NotNull(GuardianVoice.RunningColourFor(new SpeechSettings(), PersonaCatalog.Covas));
        Assert.Null(GuardianVoice.RunningColourFor(new SpeechSettings { CovasReverb = false }, PersonaCatalog.Covas));
        Assert.Null(GuardianVoice.RunningColourFor(new SpeechSettings(), PersonaCatalog.Warden));
    }
}
