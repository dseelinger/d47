using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#792: reverse reverb swells into each sound over at most 0.3 s, so it runs on a clip as it arrives.</summary>
public class ReverseReverbRunsAsTheClipArrivesTests
{
    private const int Rate = 48_000;
    private const int Tail = Rate * 3 / 10;
    private const int Block = 2_048;

    private static readonly double[] Dry = GuardianVoiceTests.Samples(GuardianVoiceTests.Voiced(150, seconds: 1.5));

    private static IGuardianStage Start()
    {
        var effect = GuardianVoice.Find("reverseReverb")!;

        return effect.Start(effect.Value(effect.DefaultLevel), 200, Rate);
    }

    private static List<double> Whole()
    {
        var output = new List<double>();
        var stage = Start();

        stage.Push(Dry, output);
        stage.Finish(output);
        return output;
    }

    [Fact]
    public void TheOutputIsTheDryLengthPlusTheTailAndItsFirstSampleIsZero()
    {
        var line = GuardianVoiceTests.Voiced(150, seconds: 1.5);
        var treated = GuardianVoiceTests.Samples(GuardianVoice.Apply(line, GuardianVoiceTests.Ticking("reverseReverb"), 200));

        Assert.Equal(Dry.Length + Tail, Whole().Count);
        Assert.Equal(Dry.Length + Tail, treated.Length);
        Assert.Equal(0, treated[0]);
    }

    [Fact]
    public void FedInRandomChunksTheOutputMatchesOneChunk()
    {
        var chunked = new List<double>();
        var stage = Start();
        var random = new Random(792);

        for (var at = 0; at < Dry.Length;)
        {
            var size = Math.Min(Dry.Length - at, random.Next(0, 3_000));
            stage.Push(Dry.AsSpan(at, size), chunked);
            at += size;
        }

        stage.Finish(chunked);

        Assert.Equal(Whole(), chunked);
    }

    [Fact]
    public void OnceSamplesHaveBeenPushedAllButOneBlockOfThemHasComeOut()
    {
        var output = new List<double>();
        var stage = Start();
        var pushed = 0;

        foreach (var sample in Dry)
        {
            stage.Push([sample], output);
            pushed++;

            Assert.True(output.Count >= pushed - Block, $"after {pushed} samples only {output.Count} had come out");
        }
    }

    [Fact]
    public void AnOutputSampleDoesNotDependOnLaterInput()
    {
        var changed = (double[])Dry.Clone();
        var cut = 20_000;

        for (var index = cut; index < changed.Length; index++)
        {
            changed[index] = 0.9;
        }

        var before = new List<double>();
        var stage = Start();
        stage.Push(changed, before);
        stage.Finish(before);

        // FFT rounding in a block shifts earlier outputs in that block by about 1e-15.
        var whole = Whole();

        for (var index = 0; index <= cut; index++)
        {
            Assert.Equal(whole[index], before[index], 1e-12);
        }
    }

    [Fact]
    public void AnEmptyClipGivesNoOutput()
    {
        var output = new List<double>();

        Start().Finish(output);

        Assert.Empty(output);
    }
}
