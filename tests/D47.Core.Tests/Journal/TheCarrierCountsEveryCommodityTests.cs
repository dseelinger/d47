using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The carrier's hold is counted per commodity, and the count is trusted only while it matches the total
/// <c>CarrierStats</c> reports (#799).
/// </summary>
public class TheCarrierCountsEveryCommodityTests
{
    private const string Fid = "F1234567";
    private const long Id = 3715429376;
    private const string Sign = "BNH-T2F";

    private const string LoadGame =
        """{"timestamp":"2026-09-13T15:59:00Z","event":"LoadGame","FID":"F1234567","Commander":"Fixture"}""";

    private static readonly string Buy =
        $$"""{"timestamp":"2026-09-13T16:00:00Z","event":"CarrierBuy","CarrierID":{{Id}},"Callsign":"{{Sign}}","Location":"Meene"}""";

    private static readonly string Dock =
        $$"""{"timestamp":"2026-09-13T16:01:00Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{Id}}}""";

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    private static string Transfer(string at, string type, int count, string direction = "tocarrier") =>
        $$"""{"timestamp":"{{at}}","event":"CargoTransfer","Transfers":[{"Type":"{{type}}","Count":{{count}},"Direction":"{{direction}}"}]}""";

    private static string Stats(string at, int cargo) =>
        $$$"""{"timestamp":"{{{at}}}","event":"CarrierStats","CarrierID":{{{Id}}},"Callsign":"{{{Sign}}}","Name":"Sacred Fire","SpaceUsage":{"TotalCapacity":25000,"Cargo":{{{cargo}}},"FreeSpace":{{{25000 - cargo}}}}}""";

    private static string Order(string at, string commodity, string what) =>
        $$"""{"timestamp":"{{at}}","event":"CarrierTradeOrder","CarrierID":{{Id}},"BlackMarket":false,"Commodity":"{{commodity}}",{{what}}}""";

    private static CarrierState Docked() => CarrierState.None.Apply(Event(Buy)).Apply(Event(Dock));

    private static CarrierState SteelAndTritiumThenStats(int cargo) => Docked()
        .Apply(Event(Transfer("2026-09-13T16:02:00Z", "steel", 500)))
        .Apply(Event(Transfer("2026-09-13T16:03:00Z", "tritium", 200)))
        .Apply(Event(Stats("2026-09-13T16:04:00Z", cargo)));

    [Fact]
    public void ACountThatMatchesTheTotalIsReconciled()
    {
        var hold = SteelAndTritiumThenStats(700).Hold;

        Assert.Equal(500, hold.Holding("steel"));
        Assert.Equal(200, hold.Holding("tritium"));
        Assert.True(hold.Reconciled);
    }

    [Fact]
    public void ACountThatMissesTheTotalIsNotReconciled()
    {
        var carrier = SteelAndTritiumThenStats(900);

        Assert.False(carrier.Hold.Reconciled);
        Assert.True(carrier.TritiumInHoldUncertain);
    }

    [Fact]
    public void AnEmptyHoldStartsTheCountAgain()
    {
        var hold = SteelAndTritiumThenStats(900)
            .Apply(Event(Stats("2026-09-13T16:05:00Z", 0)))
            .Hold;

        Assert.Empty(hold.Tonnes);
        Assert.True(hold.Reconciled);
    }

    [Fact]
    public void ATransferAfterTheCheckKeepsTheFlag()
    {
        var hold = SteelAndTritiumThenStats(700)
            .Apply(Event(Transfer("2026-09-13T16:05:00Z", "gold", 10)))
            .Hold;

        Assert.Equal(10, hold.Holding("gold"));
        Assert.True(hold.Reconciled);
    }

    [Fact]
    public void AnOrderMarksOnlyItsOwnCommodityAndCancellingClearsIt()
    {
        var ordered = SteelAndTritiumThenStats(700)
            .Apply(Event(Order("2026-09-13T16:05:00Z", "steel", "\"PurchaseOrder\":100,\"Price\":5000")));

        Assert.Equal(CarrierOrder.Purchase, ordered.Hold.Order("steel"));
        Assert.False(ordered.Hold.OrderOpen("tritium"));
        Assert.False(ordered.TritiumInHoldUncertain);

        var cancelled = ordered.Apply(Event(Order("2026-09-13T16:06:00Z", "steel", "\"CancelTrade\":true")));

        Assert.False(cancelled.Hold.OrderOpen("steel"));
    }

    [Fact]
    public void ASaleOrderIsRecordedAsOne()
    {
        var hold = Docked()
            .Apply(Event(Order("2026-09-13T16:05:00Z", "$Steel_name;", "\"SaleOrder\":100,\"Price\":5000")))
            .Hold;

        Assert.Equal(CarrierOrder.Sale, hold.Order("steel"));
    }

    [Fact]
    public void BuyingMoreThanTheCountHoldsLeavesZeroAndUnreconciled()
    {
        var hold = SteelAndTritiumThenStats(700)
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:05:00Z","event":"MarketBuy","MarketID":{{Id}},"Type":"steel","Count":800,"BuyPrice":100,"TotalCost":80000}"""))
            .Hold;

        Assert.Equal(0, hold.Holding("steel"));
        Assert.False(hold.Reconciled);
    }

    [Fact]
    public void SellingAnyCommodityAtTheCarrierAddsIt()
    {
        var hold = Docked()
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:05:00Z","event":"MarketSell","MarketID":{{Id}},"Type":"$Gold_name;","Count":12,"SellPrice":100,"TotalSale":1200}"""))
            .Hold;

        Assert.Equal(12, hold.Holding("gold"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TransfersInTheCurrentJournalAreCountedOnce()
    {
        using var install = new TempInstall();
        string[] journal =
        [
            LoadGame, Buy, Stats("2026-09-13T16:00:30Z", 0), Dock,
            Transfer("2026-09-13T16:02:00Z", "steel", 500),
            Transfer("2026-09-13T16:03:00Z", "tritium", 200),
        ];
        File.WriteAllLines(Path.Combine(install.Root, "Journal.2026-09-13T155900.01.log"), journal);

        var backfill = new HistoryBackfill { Directory = install.Root, Loggers = NullLoggerFactory.Instance };
        var store = new GameStateStore { RestoreCarrier = fid => backfill.Carriers?.GetValueOrDefault(fid) };

        foreach (var line in journal)
        {
            store.Apply(Event(line));
        }

        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        var hold = store.Active!.Carrier.Hold;

        Assert.Equal(500, hold.Holding("steel"));
        Assert.Equal(200, hold.Holding("tritium"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnOwnedLiveCarrierTakesTheWalksCountWhenItHasNoFreshStart()
    {
        using var install = new TempInstall();
        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-12T100000.01.log"),
            [LoadGame.Replace("2026-09-13T15:59", "2026-09-12T10:00", StringComparison.Ordinal), Buy, Dock,
             Transfer("2026-09-13T16:02:00Z", "steel", 500)]);

        string[] current =
        [
            LoadGame, Stats("2026-09-13T16:10:00Z", 500), Dock,
            Transfer("2026-09-13T16:11:00Z", "tritium", 200),
        ];
        File.WriteAllLines(Path.Combine(install.Root, "Journal.2026-09-13T155900.01.log"), current);

        var backfill = new HistoryBackfill { Directory = install.Root, Loggers = NullLoggerFactory.Instance };
        var store = new GameStateStore { RestoreCarrier = fid => backfill.Carriers?.GetValueOrDefault(fid) };

        foreach (var line in current)
        {
            store.Apply(Event(line));
        }

        Assert.True(store.Active!.Carrier.Owned);

        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        var hold = store.Active!.Carrier.Hold;

        Assert.Equal(500, hold.Holding("steel"));
        Assert.Equal(200, hold.Holding("tritium"));
        Assert.True(hold.Reconciled);
    }
}
