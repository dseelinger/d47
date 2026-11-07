using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class EveryPowerHasACapitalTests
{
    [Fact]
    public void EveryPowerTheGalaxySearchKnowsHasACapital()
    {
        foreach (var power in GalaxyFilters.Powers)
        {
            Assert.True(PowerCapitals.TryGet(power, out var capital), $"{power} has no capital");
            Assert.False(string.IsNullOrWhiteSpace(capital));
        }
    }

    [Fact]
    public void NoCapitalNamesAPowerTheGalaxySearchDoesNotKnow()
    {
        Assert.Empty(TableContents.PowersWithCapitals.Except(GalaxyFilters.Powers, StringComparer.Ordinal));
    }
}
