using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AMicroResourceHasOneRecordTests
{
    private static readonly DateTimeOffset Read = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

    private static SuitInventory Holding(string symbol, string kind, int backpack, int locker) => new()
    {
        Backpack = [new SuitItem(symbol, kind, backpack)],
        ShipLocker = [new SuitItem(symbol, kind, locker)],
        BackpackReadAt = Read,
        ShipLockerReadAt = Read,
    };

    [Fact]
    public void AComponentReadsItsBackpackItsLockerAndTheCategoryCap()
    {
        var detail = MicroResourceGuide.For("aerogel", Holding("aerogel", "Components", 3, 40));

        Assert.NotNull(detail);
        Assert.Equal("Components", detail.Kind);
        Assert.Equal(3, detail.Backpack);
        Assert.Equal(40, detail.Locker);
        Assert.Equal(1000, detail.LockerCap);
        Assert.False(detail.LockerCapPerItem);
    }

    [Fact]
    public void AConsumableReadsThePerItemCap()
    {
        var detail = MicroResourceGuide.For("bypass", Holding("bypass", "Consumables", 2, 5));

        Assert.NotNull(detail);
        Assert.Equal(100, detail.LockerCap);
        Assert.True(detail.LockerCapPerItem);
    }

    [Fact]
    public void ABartenderExchangeNamesWhatToGiveAndWhatYouGet()
    {
        var suit = Holding("chemicalcatalyst", "Components", 0, 20);

        var detail = MicroResourceGuide.For("aerogel", suit, get: 2);

        Assert.NotNull(detail);
        var exchange = Assert.Single(detail.Exchanges, row => row.Offered.Symbol == "chemicalcatalyst");
        Assert.Equal(2, exchange.Get);
        Assert.Equal(20, exchange.Held);
        Assert.InRange(exchange.Give, 1, 20);
    }

    [Fact]
    public void AShipMaterialIsNotAMicroResource()
    {
        Assert.Null(MicroResourceGuide.For("carbon", SuitInventory.Empty));
    }

    [Fact]
    public void TheRecordCarriesNoBackpackCap()
    {
        Assert.DoesNotContain(
            typeof(MicroResourceDetail).GetProperties(),
            property => property.Name.Contains("BackpackCap", StringComparison.Ordinal));
    }
}
