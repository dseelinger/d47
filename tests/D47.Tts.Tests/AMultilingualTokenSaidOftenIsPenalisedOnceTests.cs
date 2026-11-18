using Xunit;

namespace D47.Tts.Tests;

public sealed class AMultilingualTokenSaidOftenIsPenalisedOnceTests
{
    [Fact]
    public void ATokenGeneratedThreeTimesIsDividedByTheFactorOnce()
    {
        // Token 1 at 12 is penalised to 10 and still beats token 2 at 9; penalised three times it would be 6.9.
        float[] logits = [0, 12, 9];

        Assert.Equal(1, ChatterboxMultilingualPipeline.Next(logits, 3, [1, 1, 1]));
    }

    [Fact]
    public void ANegativeScoreIsMultipliedOnce()
    {
        // Token 0 at -1 is penalised to -1.2 and still beats token 1 at -1.5; penalised three times it would be -1.73.
        float[] logits = [-1, -1.5f, -9];

        Assert.Equal(0, ChatterboxMultilingualPipeline.Next(logits, 3, [0, 0, 0]));
    }

    [Fact]
    public void OnlyTheLastRowIsRead()
    {
        float[] logits = [9, 0, 0, 0, 0, 5];

        Assert.Equal(2, ChatterboxMultilingualPipeline.Next(logits, 3, []));
    }
}
