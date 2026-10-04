using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ATraderGivesAMaterialThreeWaysTests
{
    private static MaterialEntry Entry(string symbol) => MaterialCatalogue.Find(symbol)!;

    [Fact]
    public void AMidGradeMaterialComesUpFromBelowDownFromAboveAndAcross()
    {
        var trades = MaterialGuide.TradesInto(Entry("mercury"));

        var up = Assert.Single(trades, trade => trade.Direction == TradeDirection.Up);
        Assert.Equal("arsenic", up.From?.Symbol);
        Assert.Equal((2, 6, 1), (up.FromGrade, up.Give, up.Get));

        var down = Assert.Single(trades, trade => trade.Direction == TradeDirection.Down);
        Assert.Equal("polonium", down.From?.Symbol);
        Assert.Equal((4, 1, 3), (down.FromGrade, down.Give, down.Get));

        var across = Assert.Single(trades, trade => trade.Direction == TradeDirection.Across);
        Assert.Null(across.From);
        Assert.Equal((3, 6, 1), (across.FromGrade, across.Give, across.Get));
    }

    [Fact]
    public void TheTopGradeOfALineHasNoTradeDown()
    {
        var trades = MaterialGuide.TradesInto(Entry("polonium"));

        Assert.DoesNotContain(trades, trade => trade.Direction == TradeDirection.Down);
        Assert.Contains(trades, trade => trade.Direction == TradeDirection.Up);
    }

    [Fact]
    public void AGradeOneMaterialHasNoTradeUp()
    {
        var trades = MaterialGuide.TradesInto(Entry("carbon"));

        Assert.DoesNotContain(trades, trade => trade.Direction == TradeDirection.Up);
        Assert.Contains(trades, trade => trade.Direction == TradeDirection.Down);
    }

    [Fact]
    public void NoTraderGivesAGuardianMaterial()
    {
        Assert.Empty(MaterialGuide.TradesInto(Entry("guardian_powercell")));
    }

    [Fact]
    public void RawMaterialsAreSearchableAndGuardianOnesAreNot()
    {
        Assert.True(MaterialGuide.Searchable(Entry("polonium")));
        Assert.False(MaterialGuide.Searchable(Entry("guardian_powercell")));
    }
}
