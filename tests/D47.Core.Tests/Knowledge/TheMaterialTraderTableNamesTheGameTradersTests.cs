using Xunit;
using D47.Core.Knowledge;

namespace D47.Core.Tests.Knowledge;

public sealed class TheMaterialTraderTableNamesTheGameTradersTests
{
    [Fact]
    public void BernoulliCityInMachadanTradesRaw()
    {
        Assert.Equal("Raw", MaterialTraderTable.TypeOf(3226424832));
    }

    [Fact]
    public void AnUnlistedMarketIdHasNoType()
    {
        Assert.Null(MaterialTraderTable.TypeOf(1));
    }

    [Fact]
    public void EveryRowIsOneOfTheTraderTypes()
    {
        Assert.NotEmpty(MaterialTraderTable.All);
        Assert.All(MaterialTraderTable.All.Values, type => Assert.Contains(type, StationQuery.TraderTypes));
    }

    [Fact]
    public void AllThreeTypesAreListed()
    {
        Assert.Equal(
            StationQuery.TraderTypes.Order(),
            MaterialTraderTable.All.Values.Distinct().Order());
    }
}
