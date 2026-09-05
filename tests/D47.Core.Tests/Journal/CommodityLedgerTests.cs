using System.Text.Json;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What the Community Goal commodity made or lost, net of cost, across sessions (#296).</summary>
public class CommodityLedgerTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null ? parsed : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame(DateTimeOffset at, string fid = Fid) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{fid}}", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Buy(DateTimeOffset at, int count, int price, string type = "palladium") =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketBuy", "MarketID":1, "Type":"{{type}}", "Count":{{count}}, "BuyPrice":{{price}}, "TotalCost":{{count * price}} }""");

    private static JournalEvent Sell(DateTimeOffset at, int count, int price, int paid, string type = "palladium", long market = 2) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":{{market}}, "Type":"{{type}}", "Count":{{count}}, "SellPrice":{{price}}, "TotalSale":{{count * price}}, "AvgPricePaid":{{paid}} }""");

    private static JournalEvent Goal(DateTimeOffset at, int id, string title, DateTimeOffset expiry) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"CommunityGoal", "CurrentGoals":[ { "CGID":{{id}}, "Title":"{{title}}", "SystemName":"Ega", "MarketName":"Port", "Expiry":"{{expiry:yyyy-MM-ddTHH:mm:ssZ}}", "IsComplete":false, "CurrentTotal":1, "PlayerContribution":0, "NumContributors":1, "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TierReached":"Tier 1" } ] }""");

    [Fact]
    public void ASaleIsNetOfWhatEliteSaysTheCargoCost()
    {
        var ledger = new CommodityLedger();

        ledger.Apply([LoadGame(Noon), Sell(Noon.AddMinutes(5), 100, 51_000, 48_200)]);

        var session = ledger.Session(Fid, "Palladium");

        Assert.Equal(280_000, session.Net);
        Assert.Equal(1, session.Sales);
        Assert.Equal(100, session.Tonnes);
        Assert.Equal(5_100_000, session.Revenue);
        Assert.Equal(4_820_000, session.Cost);
    }

    [Fact]
    public void WhenEliteWritesNoAveragePriceThePurchasesAreTheBasis()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon),
            Buy(Noon.AddMinutes(1), 50, 40_000),
            Buy(Noon.AddMinutes(2), 50, 44_000),
            Sell(Noon.AddMinutes(9), 100, 50_000, paid: 0),
        ]);

        // Average paid is 42,000 a tonne.
        Assert.Equal(800_000, ledger.Session(Fid, "Palladium").Net);
    }

    [Fact]
    public void WithNothingKnownAboutTheCostTheSaleIsGross()
    {
        var ledger = new CommodityLedger();

        ledger.Apply([LoadGame(Noon), Sell(Noon.AddMinutes(9), 14, 58_371, paid: 0)]);

        Assert.Equal(817_194, ledger.Session(Fid, "Palladium").Net);
    }

    [Fact]
    public void TheSessionResetsOnLoadGameAndTheDayDoesNot()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon.AddHours(-5)),
            Sell(Noon.AddHours(-4), 10, 50_000, 40_000),
            LoadGame(Noon),
            Sell(Noon.AddMinutes(1), 10, 50_000, 40_000),
        ]);

        Assert.Equal(100_000, ledger.Session(Fid, "Palladium").Net);
        Assert.Equal(200_000, ledger.Between(Fid, "Palladium", CommodityLedger.Today(Noon)).Net);
    }

    [Fact]
    public void TodayIsTheCalendarDayInTheClocksOwnOffset()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon.AddDays(-1)),
            Sell(Noon.AddDays(-1), 10, 50_000, 40_000),
            Sell(Noon, 10, 50_000, 40_000),
        ]);

        Assert.Equal(100_000, ledger.Between(Fid, "Palladium", CommodityLedger.Today(Noon)).Net);

        // At 01:00 in a zone four hours behind UTC, yesterday's UTC sale at 12:00 is today.
        var local = new DateTimeOffset(2026, 9, 4, 20, 0, 0, TimeSpan.FromHours(-4));

        Assert.Equal(100_000, ledger.Between(Fid, "Palladium", CommodityLedger.Today(local)).Net);
    }

    [Fact]
    public void TheWeekIsTheLiveGoalsWindowFromFirstSightingToExpiry()
    {
        var ledger = new CommodityLedger();
        var firstSeen = Noon.AddDays(-3);
        var expiry = Noon.AddDays(4);

        ledger.Apply(
        [
            LoadGame(Noon.AddDays(-4)),
            Sell(Noon.AddDays(-4), 10, 50_000, 40_000),
            Goal(firstSeen, 900, "Palladium Drive", expiry),
            Sell(Noon.AddDays(-2), 10, 50_000, 40_000),
            Goal(Noon.AddDays(-1), 900, "Palladium Drive", expiry),
            Sell(Noon, 10, 50_000, 40_000),
        ]);

        var week = ledger.Week(Noon);

        Assert.Equal(firstSeen, week.From);
        Assert.Equal(expiry, week.To);
        Assert.Equal("Palladium Drive", week.Label);
        Assert.Equal(200_000, ledger.Between(Fid, "Palladium", week).Net);
    }

    [Fact]
    public void WithNoLiveGoalTheWeekIsMondayToMonday()
    {
        var ledger = new CommodityLedger();

        ledger.Apply([Goal(Noon.AddDays(-20), 800, "Over", Noon.AddDays(-12))]);

        // 2026-09-05 is a Saturday.
        var week = ledger.Week(Noon);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero), week.From);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), week.To);
        Assert.Equal("this week", week.Label);
    }

    [Fact]
    public void TheSameSaleFoldedTwiceCountsOnce()
    {
        var ledger = new CommodityLedger();
        var sale = Sell(Noon, 10, 50_000, 40_000);

        ledger.Apply([LoadGame(Noon), sale]);
        ledger.Apply([LoadGame(Noon), sale]);

        Assert.Equal(1, ledger.Session(Fid, "Palladium").Sales);

        // Two sales that differ only by market are two sales.
        ledger.Apply(Sell(Noon, 10, 50_000, 40_000, market: 3));

        Assert.Equal(2, ledger.Session(Fid, "Palladium").Sales);
    }

    [Fact]
    public void OtherCommoditiesAndOtherCommandersAreKeptApart()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon),
            Sell(Noon, 10, 50_000, 40_000),
            Sell(Noon.AddMinutes(1), 10, 9_000, 1_000, type: "gold"),
            LoadGame(Noon.AddMinutes(2), fid: "F9"),
            Sell(Noon.AddMinutes(3), 10, 50_000, 40_000),
        ]);

        Assert.Equal(100_000, ledger.Session(Fid, "Palladium").Net);
        Assert.Equal(80_000, ledger.Session(Fid, "Gold").Net);
        Assert.Equal(100_000, ledger.Session("F9", "Palladium").Net);
        Assert.Equal("F9", ledger.CurrentCommander);
    }

    [Fact]
    public void TheLocalisedSpellingIsWhatTheSaleIsFiledUnder()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon),
            Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":2, "Type":"lowtemperaturediamonds", "Type_Localised":"Low Temperature Diamonds", "Count":1, "SellPrice":100, "TotalSale":100, "AvgPricePaid":40 }"""),
        ]);

        Assert.Equal(60, ledger.Session(Fid, "Low Temperature Diamonds").Net);
        Assert.Equal(60, ledger.Session(Fid, "lowtemperaturediamonds").Net);
    }

    [Fact]
    public void ChangedFiresForASaleAndNotForAJump()
    {
        var ledger = new CommodityLedger();
        var fired = 0;
        ledger.Changed += () => fired++;

        ledger.Apply([LoadGame(Noon), Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"FSDJump", "StarSystem":"Ega", "JumpDist":10.0 }""")]);

        Assert.Equal(0, fired);

        ledger.Apply(Sell(Noon, 10, 50_000, 40_000));

        Assert.Equal(1, fired);
    }

    [Fact]
    public void TheSpokenFigureIsMillionsToOneDecimalAndNumeralsBelow()
    {
        Assert.Equal("2.1 million up", new LedgerTotal(2_100_000, 1, 1, 0, 0).Said);
        Assert.Equal("412,000 up", new LedgerTotal(412_000, 1, 1, 0, 0).Said);
        Assert.Equal("1.5 million down", new LedgerTotal(-1_500_000, 1, 1, 0, 0).Said);
        Assert.Equal("level", new LedgerTotal(0, 1, 1, 0, 0).Said);
    }

    [Fact]
    public void HistoryIsFoldedFromTheJournalFilesThatCoverTheWindow()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            // One file well before the window, one just before it that runs into it, one inside.
            Write(folder, Noon.AddDays(-30), [LoadGame(Noon.AddDays(-30)), Sell(Noon.AddDays(-30), 10, 50_000, 40_000)]);
            Write(folder, Noon.AddDays(-11), [LoadGame(Noon.AddDays(-11)), Sell(Noon.AddDays(-9), 10, 50_000, 40_000)]);
            Write(folder, Noon.AddDays(-2), [LoadGame(Noon.AddDays(-2)), Sell(Noon.AddDays(-2), 10, 50_000, 40_000)]);

            var ledger = new CommodityLedger();

            var read = ledger.FoldHistory(folder, Noon - CommodityLedger.Lookback, NullLogger.Instance);

            Assert.Equal(2, read);

            var window = new LedgerWindow(Noon.AddDays(-12), Noon, "the window");

            Assert.Equal(200_000, ledger.Between(Fid, "Palladium", window).Net);
            Assert.Equal(200_000, ledger.Between(Fid, "Palladium", new LedgerWindow(Noon.AddDays(-40), Noon, "all")).Net);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AMissingJournalFolderLeavesTheLedgerEmpty()
    {
        var ledger = new CommodityLedger();

        Assert.Equal(0, ledger.FoldHistory(Path.Combine(Path.GetTempPath(), "nowhere-" + Guid.NewGuid()), Noon, NullLogger.Instance));
        Assert.Equal(LedgerTotal.Empty, ledger.Session(Fid, "Palladium"));
    }

    [Fact]
    public void AJournalFileNameSaysWhenItWasOpened()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 9, 51, 27, TimeSpan.Zero),
            CommodityLedger.OpenedAt(@"C:\x\Journal.2026-09-03T095127.01.log"));

        Assert.Null(CommodityLedger.OpenedAt("Journal.log"));
    }

    private static void Write(string folder, DateTimeOffset opened, IEnumerable<JournalEvent> events)
    {
        var path = Path.Combine(folder, $"Journal.{opened:yyyy-MM-ddTHHmmss}.01.log");

        File.WriteAllLines(path, events.Select(journalEvent => journalEvent.Raw.GetRawText()));
    }
}
