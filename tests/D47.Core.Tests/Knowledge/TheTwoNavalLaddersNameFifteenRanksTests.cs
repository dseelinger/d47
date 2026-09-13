using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Both naval rank ladders, 0 to 14 (#178).</summary>
public class TheTwoNavalLaddersNameFifteenRanksTests
{
    [Fact]
    public void TheTopAndBottomRanksAreNamedOnBothLadders()
    {
        Assert.Equal("None", NavalRanks.EmpireName(0));
        Assert.Equal("King", NavalRanks.EmpireName(14));
        Assert.Equal("Duke", NavalRanks.EmpireName(12));
        Assert.Equal("Baron", NavalRanks.EmpireName(7));

        Assert.Equal("None", NavalRanks.FederationName(0));
        Assert.Equal("Admiral", NavalRanks.FederationName(14));
        Assert.Equal("Rear Admiral", NavalRanks.FederationName(12));
        Assert.Equal("Ensign", NavalRanks.FederationName(7));
    }

    [Fact]
    public void ARankOutsideZeroToFourteenNamesNothing()
    {
        Assert.Null(NavalRanks.EmpireName(-1));
        Assert.Null(NavalRanks.EmpireName(15));
        Assert.Null(NavalRanks.FederationName(-1));
        Assert.Null(NavalRanks.FederationName(15));
    }
}
