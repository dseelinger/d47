using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ABrightnessStepIsAGammaAndBackTests
{
    [Theory]
    [InlineData(-4, 2.0)]
    [InlineData(0, 1.0)]
    [InlineData(4, 0.5)]
    [InlineData(8, 0.25)]
    public void AStepIsTheGammaTwoToTheMinusStepOverFour(int step, double gamma)
    {
        Assert.Equal(gamma, PictureLift.GammaOf(step), 10);
        Assert.Equal(step, PictureLift.StepOf(gamma));
    }

    [Theory]
    [InlineData(4.0, -4)]
    [InlineData(0.01, 8)]
    public void AGammaOutsideTheRangeClampsToAnEnd(double gamma, int step)
    {
        Assert.Equal(step, PictureLift.StepOf(gamma));
    }

    [Fact]
    public void NoGammaIsStepZeroAndTheHeadsetStartIsStepFour()
    {
        Assert.Equal(0, PictureLift.StepOf(null));
        Assert.Equal(4, PictureLift.StepOf(PictureLift.HeadsetGamma));
    }
}
