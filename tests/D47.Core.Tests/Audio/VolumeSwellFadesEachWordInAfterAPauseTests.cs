using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class VolumeSwellFadesEachWordInAfterAPauseTests
{
    private const int Rate = 48_000;

    private const int Slice = 480;

    private static int At(double seconds) => (int)(seconds * Rate);

    private static double[] Line(params (double StartSeconds, double Seconds)[] tones)
    {
        var samples = new double[At(3)];

        foreach (var (start, seconds) in tones)
        {
            for (var index = 0; index < At(seconds); index++)
            {
                samples[At(start) + index] = 0.3 * Math.Sin(2 * Math.PI * 440 * index / Rate);
            }
        }

        return samples;
    }

    private static double[] Swell(double[] dry, int level)
    {
        var output = new List<double>();
        var stage = GuardianStages.Start("volumeSwell", level, 200, Rate);

        stage.Push(dry, output);
        stage.Finish(output);
        return [.. output];
    }

    private static double Peak(double[] samples, int from) =>
        samples.Skip(from).Take(Slice).Max(Math.Abs);

    [Fact]
    public void AWordAfterSilenceRisesFromZeroToFullOverTheAttackThenHolds()
    {
        var dry = Line((0.5, 1.0));
        var attack = At(0.096);
        var output = Swell(dry, 16);
        var onset = At(0.5);

        Assert.Equal(dry.Length, output.Length);
        var slices = Enumerable.Range(0, attack / Slice).Select(slice => Peak(output, onset + (slice * Slice))).ToArray();

        Assert.True(slices.Zip(slices.Skip(1), (a, b) => b > a).All(rising => rising));
        Assert.True(slices[0] < 0.01);
        Assert.True(Peak(output, onset + attack - Slice) > 0.25);

        for (var at = onset + attack; at < onset + At(1.0) - Slice; at += Slice)
        {
            Assert.Equal(Peak(dry, at), Peak(output, at), 6);
        }
    }

    [Fact]
    public void ASecondWordAfterAPauseSwellsAgain()
    {
        var output = Swell(Line((0.5, 0.5), (1.5, 0.5)), 16);

        Assert.True(Peak(output, At(1.5)) < 0.01);
        Assert.True(Peak(output, At(1.5) + At(0.096)) > 0.25);
    }

    [Fact]
    public void ASecondBurstAfterAShortGapDoesNotSwellAgain()
    {
        var dry = Line((0.5, 0.5), (1.04, 0.5));
        var output = Swell(dry, 16);

        Assert.Equal(Peak(dry, At(1.04)), Peak(output, At(1.04)), 6);
    }

    [Fact]
    public void AHigherLevelTakesLongerToFadeIn()
    {
        var dry = Line((0.5, 1.0));

        Assert.True(Peak(Swell(dry, 20), At(0.5) + At(0.096)) < Peak(Swell(dry, 16), At(0.5) + At(0.096)));
    }

    [Theory]
    [MemberData(nameof(EachLevel))]
    public void FedInRandomChunksTheOutputMatchesOneChunk(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(GuardianVoiceTests.ToneBursts(4, 150, 400, 300));

        var (whole, chunked) = GuardianStages.WholeAndChunked(id, level, dry, 2130 + level, 2_000);

        Assert.Equal(whole, chunked);
    }

    public static TheoryData<string, int> EachLevel => GuardianStages.EachLevel("volumeSwell", "delay");
}
