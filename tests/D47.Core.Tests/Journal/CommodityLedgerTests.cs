using System.Text.Json;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What the Community Goal commodity made or lost, net of cost, across sessions.</summary>
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
    public void TheWeekIsTheEliteWeekFromTheThursdayUtcBoundaryByDefault()
    {
        // 2026-09-05 12:00 UTC is a Saturday; 2026-09-03 is the Thursday just before it.
        var week = CommodityLedger.Week(Noon, DayOfWeek.Thursday, 7);

        Assert.Equal(new DateTimeOffset(2026, 9, 3, 7, 0, 0, TimeSpan.Zero), week.From);
        Assert.Equal(new DateTimeOffset(2026, 9, 10, 7, 0, 0, TimeSpan.Zero), week.To);
        Assert.Equal("this week", week.Label);
    }

    [Fact]
    public void TheBoundarySplitsTheWeekAtTheMinute()
    {
        var justBefore = new DateTimeOffset(2026, 9, 3, 6, 59, 0, TimeSpan.Zero);
        var justAfter = new DateTimeOffset(2026, 9, 3, 7, 1, 0, TimeSpan.Zero);

        var before = CommodityLedger.Week(justBefore, DayOfWeek.Thursday, 7);
        var after = CommodityLedger.Week(justAfter, DayOfWeek.Thursday, 7);

        Assert.Equal(new DateTimeOffset(2026, 8, 27, 7, 0, 0, TimeSpan.Zero), before.From);
        Assert.Equal(new DateTimeOffset(2026, 9, 3, 7, 0, 0, TimeSpan.Zero), after.From);
        Assert.NotEqual(before.From, after.From);
    }

    [Fact]
    public void TheSettingMovesTheBoundary()
    {
        var week = CommodityLedger.Week(Noon, DayOfWeek.Monday, 0);

        // The most recent Monday 00:00 UTC before Saturday 2026-09-05 is 2026-08-31.
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero), week.From);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero), week.To);
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
    public void TheSpokenFigureIsBandedAtMillionsBillionsAndTrillionsAndNumeralsBelow()
    {
        Assert.Equal("2.1 million up", new LedgerTotal(2_100_000, 1, 1, 0, 0).Said);
        Assert.Equal("412,000 up", new LedgerTotal(412_000, 1, 1, 0, 0).Said);
        Assert.Equal("1.5 million down", new LedgerTotal(-1_500_000, 1, 1, 0, 0).Said);
        Assert.Equal("level", new LedgerTotal(0, 1, 1, 0, 0).Said);

        // #336: never "2100 million" — a Community Goal total bands into billions.
        Assert.Equal("2.1 billion up", new LedgerTotal(2_129_966_400, 1, 1, 0, 0).Said);
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
    public void AJournalFileNameSaysWhenItWasOpenedInLocalTime()
    {
 // Elite stamps the filename in the machine's local time, not UTC — so the expected offset is
        // whatever this machine was at that moment, asked rather than written down.
        var stamp = new DateTime(2026, 9, 3, 9, 51, 27, DateTimeKind.Unspecified);

        Assert.Equal(
            new DateTimeOffset(stamp, TimeZoneInfo.Local.GetUtcOffset(stamp)),
            CommodityLedger.OpenedAt(@"C:\x\Journal.2026-09-03T095127.01.log"));

        Assert.Null(CommodityLedger.OpenedAt("Journal.log"));
    }

    [Fact]
    public void TwoSalesInTheSameSecondWithTheSameTotalAreBothFolded()
    {
        var ledger = new CommodityLedger();

        // One second's resolution, same station, same commodity, count and total — but one from purchased
 // cargo and one from mined cargo, distinguished only by AvgPricePaid.
        ledger.Apply(
        [
            LoadGame(Noon),
            Sell(Noon, 10, 50_000, paid: 40_000),
            Sell(Noon, 10, 50_000, paid: 0),
        ]);

        Assert.Equal(2, ledger.Session(Fid, "Palladium").Sales);
    }

    [Fact]
    public void ASaleDrawsDownTheRememberedPurchaseSoALaterMinedSaleIsNotChargedTheOldLot()
    {
        var ledger = new CommodityLedger();

        ledger.Apply(
        [
            LoadGame(Noon),

            // Bought and sold: the purchase is fully drawn down by the sale.
            Buy(Noon.AddMinutes(1), 50, 40_000),
            Sell(Noon.AddMinutes(2), 50, 50_000, paid: 0),

            // Mined separately and sold: nothing bought remains, so this sale is gross.
            Sell(Noon.AddMinutes(3), 50, 50_000, paid: 0, market: 3),
        ]);

        Assert.Equal(2, ledger.Session(Fid, "Palladium").Sales);

        // The second sale must not fall back to the first lot's 40,000-a-tonne cost: with the purchase drawn
 // down to nothing, it is gross.
        var second = ledger.LastSale(Fid, "Palladium");

        Assert.NotNull(second);
        Assert.Equal(0, second!.CostBasis);
        Assert.Equal(2_500_000, second.Net);
    }

    [Fact]
    public void AContinuationFileWithNoLoadGameIsAttributedToTheCommanderItContinues()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            // The opening file carries the identity and sits before the fold's start; the continuation that
 // becomes the fold's start file carries none of its own, as a real Journal.*.02.log would.
            Write(folder, Noon.AddDays(-11), [LoadGame(Noon.AddDays(-11))]);
            Write(folder, Noon.AddDays(-11).AddMinutes(1), [Sell(Noon.AddDays(-11).AddMinutes(2), 10, 50_000, 40_000)]);
            Write(folder, Noon.AddDays(-9), [Sell(Noon.AddDays(-9), 10, 50_000, 40_000)]);

            var ledger = new CommodityLedger();

            var read = ledger.FoldHistory(folder, Noon - CommodityLedger.Lookback, NullLogger.Instance);

            Assert.Equal(2, read);
            Assert.Equal(200_000, ledger.Session(Fid, "Palladium").Net);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void Write(string folder, DateTimeOffset opened, IEnumerable<JournalEvent> events)
    {
        var path = Path.Combine(folder, $"Journal.{opened:yyyy-MM-ddTHHmmss}.01.log");

        File.WriteAllLines(path, events.Select(journalEvent => journalEvent.Raw.GetRawText()));
    }
}
