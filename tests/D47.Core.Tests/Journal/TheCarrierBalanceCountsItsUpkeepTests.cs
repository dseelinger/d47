using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCarrierBalanceCountsItsUpkeepTests
{
    private const long Id = 3715429376;

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T13:00:00Z");

    private static string Stats(string at, long balance, string type = "FleetCarrier", long id = Id) =>
        $$$"""{"timestamp":"{{{at}}}","event":"CarrierStats","CarrierID":{{{id}}},"CarrierType":"{{{type}}}","Callsign":"BNH-T2F","Name":"Sacred Fire","Finance":{"CarrierBalance":{{{balance}}}}}""";

    private static string Deposit(string at, long deposit, long after) =>
        $$$"""{"timestamp":"{{{at}}}","event":"CarrierBankTransfer","CarrierID":{{{Id}}},"Deposit":{{{deposit}}},"PlayerBalance":1000,"CarrierBalance":{{{after}}}}""";

    private static CarrierState Fold(CarrierState start, params string[] lines)
    {
        var state = start;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state = state.Apply(parsed!);
        }

        return state;
    }

    private static CarrierState Fold(params string[] lines) => Fold(CarrierState.None, lines);

    /// <summary>The one-tick intervals from the maintainer's own history, oldest first.</summary>
    private static readonly string[] History =
    [
        Stats("2026-08-17T20:00:00Z", 1_100_000_000),
        Stats("2026-08-21T20:00:00Z", 1_090_399_992),
        Stats("2026-08-29T20:00:00Z", 1_080_799_992),
        Stats("2026-09-09T20:00:00Z", 1_071_099_984),
        Stats("2026-09-13T20:00:00Z", 1_061_499_984),
        Stats("2026-09-18T20:00:00Z", 1_051_799_984),
    ];

    [Fact]
    public void TheWeeklyFigureIsTheLatestOneTickDrop()
    {
        Assert.Equal(9_700_000, Fold(History).WeeklyUpkeep);
    }

    [Fact]
    public void ATwoTickGapIsSplitAcrossBothWeeks()
    {
        var state = Fold(Stats("2026-08-03T20:00:00Z", 1_000_000_000), Stats("2026-08-17T20:00:00Z", 980_899_992));

        Assert.Equal(9_550_004, state.WeeklyUpkeep);
    }

    [Fact]
    public void TheBalanceNowIsTheRecordedOneLessATickOfUpkeep()
    {
        var state = Fold([.. History, Stats("2026-09-22T23:24:52Z", 990_302_661)]);

        var balance = CarrierUpkeep.Now(state, Now);

        Assert.NotNull(balance);
        Assert.True(balance.Value.Adjusted);
        Assert.Equal(980_602_661, balance.Value.Balance);
        Assert.Equal(101, balance.Value.WeeksCovered);
    }

    [Fact]
    public void ADepositIsNotReadAsNegativeUpkeep()
    {
        var state = Fold([.. History, Deposit("2026-09-20T10:00:00Z", 268_950_000, 1_320_749_984)]);

        Assert.Equal(9_700_000, state.WeeklyUpkeep);
        Assert.Equal(1_320_749_984, state.Balance);
    }

    [Fact]
    public void AWeekMeasuredAcrossADepositCountsOnlyTheUpkeep()
    {
        var state = Fold([.. History, Deposit("2026-09-25T10:00:00Z", 268_950_000, 1_311_049_984)]);

        Assert.Equal(9_700_000, state.WeeklyUpkeep);
    }

    [Theory]
    [InlineData("CarrierCrewServices", """ "Operation":"Activate","CrewRole":"Shipyard" """)]
    [InlineData("CarrierModulePack", """ "Operation":"BuyPack","PackTheme":"Weapons","Cost":1000000 """)]
    [InlineData("CarrierShipPack", """ "Operation":"BuyPack","PackTheme":"Zorgon","Cost":1000000 """)]
    [InlineData("CarrierTradeOrder", """ "BlackMarket":false,"Commodity":"gold","PurchaseOrder":10,"Price":50000 """)]
    public void AnIntervalWithSpendingLeavesThePreviousFigure(string kind, string fields)
    {
        var spend = $$$"""{"timestamp":"2026-09-20T10:00:00Z","event":"{{{kind}}}","CarrierID":{{{Id}}},{{{fields}}}}""";

        var state = Fold([.. History, spend, Stats("2026-09-25T10:00:00Z", 1_000_000_000)]);

        Assert.Equal(9_700_000, state.WeeklyUpkeep);
    }

    [Fact]
    public void WithNoTickSinceTheBalanceNothingIsAdjusted()
    {
        string[] lines = [.. History, Stats("2026-09-24T07:30:00Z", 990_302_661)];

        var balance = CarrierUpkeep.Now(Fold(lines), Now);

        Assert.Equal(990_302_661, balance!.Value.Balance);
        Assert.False(balance.Value.Adjusted);
        Assert.DoesNotContain("about", CarrierCapability.Describe(Game(lines), Now), StringComparison.Ordinal);
    }

    [Fact]
    public void BeforeAnyUsableIntervalOnlyTheRecordedBalanceIsReported()
    {
        var report = CarrierCapability.Describe(Game(Stats("2026-09-18T20:00:00Z", 990_302_661)), Now);

        Assert.Contains("Balance 990,302,661 cr.", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Upkeep", report, StringComparison.Ordinal);
        Assert.DoesNotContain("covers", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReportSaysTheBalanceWasAdjustedAndFromWhat()
    {
        var report = CarrierCapability.Describe(
            Game([.. History, Stats("2026-09-22T23:24:52Z", 990_302_661)]),
            Now);

        Assert.Contains(
            "Balance about 980,602,661 cr (990,302,661 on 22 Sep, less one week's upkeep of 9,700,000).",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "Upkeep 9,700,000 cr a week, which the balance covers for 101 weeks.",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSquadronCarrierGetsNoUpkeep()
    {
        var squadron = Fold(
            CarrierState.NoSquadron,
            [.. History.Select(line => line.Replace("FleetCarrier", "SquadronCarrier", StringComparison.Ordinal))]);

        Assert.Null(squadron.WeeklyUpkeep);
        Assert.Null(CarrierUpkeep.Now(squadron, Now));
    }

    [Theory]
    [InlineData("2026-09-17T06:59:59Z", "2026-09-17T07:00:00Z", 1)]
    [InlineData("2026-09-17T07:00:00Z", "2026-09-24T06:59:59Z", 0)]
    [InlineData("2026-09-17T07:30:00Z", "2026-09-24T07:00:00Z", 1)]
    [InlineData("2026-09-10T08:00:00Z", "2026-09-24T13:00:00Z", 2)]
    [InlineData("1969-12-20T00:00:00Z", "1970-01-01T07:00:00Z", 2)]
    public void TicksAreCountedInTheHalfOpenInterval(string from, string to, int ticks)
    {
        Assert.Equal(ticks, CarrierUpkeep.TicksBetween(DateTimeOffset.Parse(from), DateTimeOffset.Parse(to)));
    }

    private static CommanderGameState? Game(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in (string[])[
            """{"timestamp":"2026-08-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            .. lines])
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active;
    }
}
