using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A link is clear to 250 light years and fades to nothing at 500.</summary>
public class TheCarrierLinkFadesWithDistanceTests
{
    [Fact]
    public void WithinTwoHundredFiftyLightYearsTheLinkIsClear()
    {
        Assert.Equal(1, LinkSignal.Strength(0));
        Assert.Equal(1, LinkSignal.Strength(250));
    }

    [Fact]
    public void ADistanceNobodyKnowsIsAClearLink() => Assert.Equal(1, LinkSignal.Strength(null));

    [Fact]
    public void AtFiveHundredLightYearsAlmostNothingCarries()
    {
        Assert.True(LinkSignal.Strength(500) < 0.05, $"{LinkSignal.Strength(500):F3} carries at 500 Ly");
        Assert.Equal(0, LinkSignal.Strength(501));
    }

    [Fact]
    public void TheSignalNeverRisesWithDistance()
    {
        for (var a = 250.0; a <= 500; a += 0.5)
        {
            for (var b = a + 0.5; b <= 500; b += 0.5)
            {
                Assert.True(
                    LinkSignal.Strength(a) >= LinkSignal.Strength(b),
                    $"{LinkSignal.Strength(b):F4} at {b} Ly is stronger than {LinkSignal.Strength(a):F4} at {a} Ly");
            }
        }
    }

    [Fact]
    public void TheFallIsContinuous()
    {
        for (var away = 250.0; away < 500; away += 0.5)
        {
            Assert.True(
                LinkSignal.Strength(away) - LinkSignal.Strength(away + 0.5) < 0.01,
                $"the signal jumps between {away} and {away + 0.5} Ly");
        }
    }
}
