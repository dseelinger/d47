using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class DelayAddsFadingEchoesAfterEachWordTests
{
    private const int Rate = 48_000;

    private const int EchoSamples = 13_440;

    private static List<double> Run(double[] dry, int level)
    {
        var output = new List<double>();
        var stage = GuardianStages.Start("delay", level, 200, Rate);

        stage.Push(dry, output);
        stage.Finish(output);
        return output;
    }

    private static double Echo(List<double> output, int number) =>
        Enumerable.Range((number * EchoSamples) - 2, 5).Max(index => Math.Abs(output[index]));

    [Fact]
    public void AnImpulseRepeatsEvery280msEachEchoLowerThanTheLast()
    {
        var dry = new double[Rate];
        dry[0] = 1;

        var output = Run(dry, 20);
        var echoes = Enumerable.Range(1, 4).Select(number => Echo(output, number)).ToArray();

        Assert.All(echoes, echo => Assert.True(echo > 0.001));
        Assert.True(echoes[0] < output[0]);
        Assert.True(echoes[0] > echoes[1] && echoes[1] > echoes[2] && echoes[2] > echoes[3]);
        Assert.True(Math.Abs(output[EchoSamples]) > Math.Abs(output[EchoSamples - 100]) * 10);
    }

    [Fact]
    public void TheLineIsFollowedByAFadingTailEndingInSilence()
    {
        var dry = new double[Rate];
        dry[0] = 1;

        var output = Run(dry, 16);

        Assert.Equal(Rate + (int)(1.5 * Rate), output.Count);
        Assert.Equal(0, output[^1]);
    }

    [Fact]
    public void ADrySampleIsKeptAtFullStrength()
    {
        var dry = new double[Rate];
        dry[0] = 0.5;

        Assert.Equal(0.5, Run(dry, 16)[0], 12);
    }
}
