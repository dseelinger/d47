using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The carrier's hold has no tritium figure of its own — it is counted from the movements that
/// cross it.
/// </summary>
public class TheCarrierCountsItsOwnTritiumTests
{
    private const long Id = 3715429376;
    private const string Sign = "BNH-T2F";

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    /// <summary>The carrier known by id, docked at.</summary>
    private static CarrierState Docked()
    {
        var state = CarrierState.None.Apply(Event(
            $$"""{"timestamp":"2026-09-13T16:00:00Z","event":"CarrierBuy","CarrierID":{{Id}},"Callsign":"{{Sign}}","Location":"Meene"}"""));

        state = state.Apply(Event(
            $$"""{"timestamp":"2026-09-13T16:01:00Z","event":"Docked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{Id}}}"""));

        Assert.Equal(Id, state.CarrierId);
        Assert.True(state.DockedAtOwnCarrier);
        Assert.Null(state.TritiumInHold);

        return state;
    }

    private static JournalEvent Undock() => Event(
        $$"""{"timestamp":"2026-09-13T16:05:00Z","event":"Undocked","StationName":"{{Sign}}","StationType":"FleetCarrier","MarketID":{{Id}}}""");

    private static JournalEvent Transfer(string type, int count, string direction) => Event(
        $$"""{"timestamp":"2026-09-13T16:02:00Z","event":"CargoTransfer","Transfers":[{"Type":"{{type}}","Count":{{count}},"Direction":"{{direction}}"}]}""");

    [Fact]
    public void MovingCargoIntoTheHoldCountsIt()
    {
        var state = Docked().Apply(Transfer("tritium", 540, "tocarrier"));

        Assert.Equal(540, state.TritiumInHold);
        Assert.False(state.TritiumInHoldUncertain);
    }

    [Fact]
    public void MovingCargoOutOfTheHoldSubtractsIt()
    {
        var state = Docked()
            .Apply(Transfer("tritium", 540, "tocarrier"))
            .Apply(Transfer("tritium", 382, "toship"));

        Assert.Equal(158, state.TritiumInHold);
    }

    [Fact]
    public void ATransferOfAnotherCommodityTeachesNothing()
    {
        var state = Docked().Apply(Transfer("steel", 20, "tocarrier"));

        Assert.Null(state.TritiumInHold);
    }

    [Fact]
    public void ATransferWhileNotDockedHereTeachesNothing()
    {
        var state = CarrierState.None
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:00:00Z","event":"CarrierBuy","CarrierID":{{Id}},"Callsign":"{{Sign}}","Location":"Meene"}"""))
            .Apply(Transfer("tritium", 540, "tocarrier"));

        Assert.Null(state.TritiumInHold);
    }

    [Fact]
    public void UndockingStopsCountingTransfers()
    {
        var state = Docked()
            .Apply(Undock())
            .Apply(Transfer("tritium", 540, "tocarrier"));

        Assert.Null(state.TritiumInHold);
    }

    [Fact]
    public void SellingAtTheCarriersOwnMarketAddsToTheHold()
    {
        var state = Docked().Apply(Event(
            $$"""{"timestamp":"2026-09-13T16:03:00Z","event":"MarketSell","MarketID":{{Id}},"Type":"tritium","Count":50,"SellPrice":100,"TotalSale":5000}"""));

        Assert.Equal(50, state.TritiumInHold);
    }

    [Fact]
    public void BuyingAtTheCarriersOwnMarketSubtractsFromTheHold()
    {
        var state = Docked()
            .Apply(Transfer("tritium", 100, "tocarrier"))
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:03:00Z","event":"MarketBuy","MarketID":{{Id}},"Type":"tritium","Count":30,"BuyPrice":100,"TotalCost":3000}"""));

        Assert.Equal(70, state.TritiumInHold);
    }

    [Fact]
    public void ATradeAtAnotherMarketTeachesNothing()
    {
        var state = Docked().Apply(Event(
            """{"timestamp":"2026-09-13T16:03:00Z","event":"MarketSell","MarketID":1,"Type":"tritium","Count":50,"SellPrice":100,"TotalSale":5000}"""));

        Assert.Null(state.TritiumInHold);
    }

    [Fact]
    public void AnOpenTradeOrderMarksTheCountUncertain()
    {
        var state = Docked().Apply(Event(
            $$"""{"timestamp":"2026-09-13T16:03:00Z","event":"CarrierTradeOrder","CarrierID":{{Id}},"BlackMarket":false,"Commodity":"tritium","SaleOrder":100}"""));

        Assert.True(state.TritiumInHoldUncertain);
    }

    [Fact]
    public void CancellingTheOrderDoesNotClearTheUncertainty()
    {
        var state = Docked()
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:03:00Z","event":"CarrierTradeOrder","CarrierID":{{Id}},"BlackMarket":false,"Commodity":"tritium","SaleOrder":100}"""))
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:10:00Z","event":"CarrierTradeOrder","CarrierID":{{Id}},"BlackMarket":false,"Commodity":"tritium","CancelTrade":true}"""));

        Assert.True(state.TritiumInHoldUncertain);
    }

    [Fact]
    public void AnOrderForAnotherCommodityLeavesTheCountCertain()
    {
        var state = Docked().Apply(Event(
            $$"""{"timestamp":"2026-09-13T16:03:00Z","event":"CarrierTradeOrder","CarrierID":{{Id}},"BlackMarket":false,"Commodity":"gold","SaleOrder":100}"""));

        Assert.False(state.TritiumInHoldUncertain);
    }

    [Fact]
    public void ACountThatWouldGoNegativeIsHeldAtZeroAndMarkedUncertain()
    {
        var state = Docked().Apply(Transfer("tritium", 100, "toship"));

        Assert.Equal(0, state.TritiumInHold);
        Assert.True(state.TritiumInHoldUncertain);
    }

    [Fact]
    public void DepositingFuelDoesNotChangeTheHold()
    {
        var state = Docked()
            .Apply(Transfer("tritium", 200, "tocarrier"))
            .Apply(Event(
                $$"""{"timestamp":"2026-09-13T16:04:00Z","event":"CarrierDepositFuel","CarrierID":{{Id}},"Amount":100,"Total":900}"""));

        Assert.Equal(200, state.TritiumInHold);
        Assert.Equal(900, state.FuelLevel);
    }
}
