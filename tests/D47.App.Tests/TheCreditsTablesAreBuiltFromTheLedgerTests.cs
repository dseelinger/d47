using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The rows behind Commander › Credits, built from a ledger, a period, a kind and a time (#1289).</summary>
public class TheCreditsTablesAreBuiltFromTheLedgerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] Lines =
    [
        """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame","Credits":1000000}""",
        """{"timestamp":"2026-10-02T09:00:00Z","event":"MarketSell","TotalSale":500000}""",
        """{"timestamp":"2026-10-03T09:00:00Z","event":"MarketBuy","TotalCost":100000}""",
        """{"timestamp":"2026-10-04T09:00:00Z","event":"CarrierBankTransfer","Deposit":200000,"PlayerBalance":1200000}""",
        """{"timestamp":"2026-10-05T09:00:00Z","event":"Repair","Cost":5000}""",
        """{"timestamp":"2026-10-06T09:00:00Z","event":"LoadGame","Credits":3195000}""",
        """{"timestamp":"2026-10-07T09:00:00Z","event":"LoadGame","Credits":3194500}""",
        """{"timestamp":"2026-10-08T09:00:00Z","event":"CarrierBankTransfer","Withdraw":50000,"PlayerBalance":3244500}""",
        """{"timestamp":"2026-10-09T09:00:00Z","event":"LoadGame","Credits":3244200}""",
    ];

    internal static CreditLedger Ledger(params string[] lines)
    {
        var ledger = CreditLedger.None;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            ledger = ledger.Apply(parsed!);
        }

        return ledger;
    }

    private static CreditsView Everything(HistoryState history = HistoryState.Done) =>
        CreditsModel.Rows(Ledger(Lines), CombatPeriod.Everything, Now, history);

    private static CreditTxView All(CreditKind? kind = null, string? query = null) =>
        CreditsModel.Transactions(Ledger(Lines), CombatPeriod.Everything, kind, Now, HistoryState.Done, query);

    private static string[] Events(IEnumerable<CreditTxRow> rows) => [.. rows.Select(row => row.Event)];

    [Fact]
    public void InAndOutLeaveOutTheCarrierBankAndTheUnaccountedGaps()
    {
        var view = Everything();

        Assert.Equal(500_000, view.In);
        Assert.Equal(105_000, view.Out);
        Assert.Equal(-150_000, view.CarrierBank);
        Assert.Equal(1_999_200, view.Unaccounted);
        Assert.Equal(3, view.Gaps);
        Assert.Equal(3_244_200, view.Balance);
    }

    [Fact]
    public void TheKindsAreOrderedByMovementAndTheTwoMovedKindsSitBelowThem()
    {
        var view = Everything();

        Assert.Equal([CreditKind.Trade, CreditKind.Upkeep], view.Kinds.Select(row => row.Kind));
        Assert.Equal([CreditKind.CarrierBank, CreditKind.Unaccounted], view.Moved.Select(row => row.Kind));
        Assert.Equal(4, view.Total);
        Assert.Null(view.Kinds[1].In);
        Assert.Equal(5_000, view.Kinds[1].Out);
    }

    [Fact]
    public void EachKindHeadingSortsWithAbsentValuesLast()
    {
        var kinds = Everything().Kinds;
        string[] Names(SortHeader<CreditKindRow> head) => [.. head.Sort(kinds).Select(row => row.Name)];

        var byName = new SortHeader<CreditKindRow>("t", CreditsModel.KindHeads());
        byName.Press(0);
        Assert.Equal(["Trade", "Upkeep"], Names(byName));
        byName.Press(0);
        Assert.Equal(["Upkeep", "Trade"], Names(byName));

        var byIn = new SortHeader<CreditKindRow>("t", CreditsModel.KindHeads());
        byIn.Press(1);
        Assert.Equal(["Trade", "Upkeep"], Names(byIn));
        byIn.Press(1);
        Assert.Equal(["Trade", "Upkeep"], Names(byIn));

        var byOut = new SortHeader<CreditKindRow>("t", CreditsModel.KindHeads());
        byOut.Press(2);
        Assert.Equal(["Trade", "Upkeep"], Names(byOut));
        byOut.Press(2);
        Assert.Equal(["Upkeep", "Trade"], Names(byOut));

        var byEntries = new SortHeader<CreditKindRow>("t", CreditsModel.KindHeads());
        byEntries.Press(3);
        Assert.Equal(["Trade", "Upkeep"], Names(byEntries));
    }

    [Fact]
    public void AmountsAreSignedWithTheTrueMinusSign()
    {
        Assert.Equal("+4,812,000 CR", CreditsModel.Signed(4_812_000));
        Assert.Equal("−3,120,400 CR", CreditsModel.Signed(-3_120_400));
    }

    [Fact]
    public void TheCarrierBankAndUnaccountedRowsGetASecondLine()
    {
        Assert.Equal("Moved to your carrier. Not spent.", CreditsModel.NoteFor(CreditKind.CarrierBank, "CarrierBankTransfer", -200_000));
        Assert.Equal("Moved from your carrier. Not earned.", CreditsModel.NoteFor(CreditKind.CarrierBank, "CarrierBankTransfer", 50_000));
        Assert.Equal(
            "Not in the journal. The game's balance at this login was 2,000,000,000 CR more than the journal adds up to.",
            CreditsModel.NoteFor(CreditKind.Unaccounted, "LoadGame", 2_000_000_000));
        Assert.Equal(
            "Not in the journal. The game's balance at this carrier bank transfer was 1,500 CR less than the journal adds up to.",
            CreditsModel.NoteFor(CreditKind.Unaccounted, "CarrierBankTransfer", -1_500));
        Assert.Null(CreditsModel.NoteFor(CreditKind.Trade, "MarketSell", 5));
    }

    [Fact]
    public void TransactionsAreNewestFirstWithSmallGapsFoldedIntoOneLastRow()
    {
        var view = All();

        Assert.Equal(
            ["CarrierBankTransfer", "LoadGame", "Repair", "CarrierBankTransfer", "MarketBuy", "MarketSell"],
            Events(view.Rows));
        Assert.Equal(8, view.Entries);

        var fold = Assert.IsType<CreditTxRow>(view.Fold);
        Assert.Equal("2 small gaps", fold.Event);
        Assert.Equal(-800, fold.Amount);
        Assert.Null(fold.Balance);
        Assert.Equal(CreditsModel.FoldNote, fold.Note);
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T09:00:00Z", CultureInfo.InvariantCulture), fold.From);
        Assert.Equal(DateTimeOffset.Parse("2026-10-09T09:00:00Z", CultureInfo.InvariantCulture), fold.At);
    }

    [Fact]
    public void NoSmallGapsMeansNoFoldedRow()
    {
        var view = All(CreditKind.Trade);

        Assert.Null(view.Fold);
        Assert.False(view.HasGap);
        Assert.Equal(2, view.Entries);
    }

    [Fact]
    public void EachTransactionHeadingSortsAndTheFoldedRowStaysOutOfTheSort()
    {
        var rows = All().Rows;
        var head = new SortHeader<CreditTxRow>("t", CreditsModel.TxHeads());

        head.Press(2);
        Assert.Equal(
            ["LoadGame", "MarketSell", "CarrierBankTransfer", "Repair", "MarketBuy", "CarrierBankTransfer"],
            Events(head.Sort(rows)));

        var byKind = new SortHeader<CreditTxRow>("t", CreditsModel.TxHeads());
        byKind.Press(1);
        Assert.Equal(
            ["CarrierBankTransfer", "CarrierBankTransfer", "MarketBuy", "MarketSell", "LoadGame", "Repair"],
            Events(byKind.Sort(rows)));

        var byTime = new SortHeader<CreditTxRow>("t", CreditsModel.TxHeads());
        byTime.Press(0);
        Assert.Equal(Events(rows), Events(byTime.Sort(rows)));
        byTime.Press(0);
        Assert.Equal("MarketSell", byTime.Sort(rows)[0].Event);
    }

    [Fact]
    public void NullBalancesSortLastEitherWay()
    {
        var ledger = Ledger(
            """{"timestamp":"2026-10-01T09:00:00Z","event":"MarketSell","TotalSale":100}""",
            """{"timestamp":"2026-10-02T09:00:00Z","event":"LoadGame","Credits":1000}""",
            """{"timestamp":"2026-10-03T09:00:00Z","event":"MarketSell","TotalSale":200}""",
            """{"timestamp":"2026-10-04T09:00:00Z","event":"MarketSell","TotalSale":50}""");
        var view = CreditsModel.Transactions(ledger, CombatPeriod.Everything, null, Now, HistoryState.Done);
        var head = new SortHeader<CreditTxRow>("t", CreditsModel.TxHeads());

        Assert.True(view.HasNoBalance);

        head.Press(3);
        Assert.Equal([1250L, 1200L], head.Sort(view.Rows).Where(row => row.Balance is not null).Select(row => row.Balance!.Value));
        Assert.Null(head.Sort(view.Rows)[^1].Balance);

        head.Press(3);
        Assert.Equal([1200L, 1250L], head.Sort(view.Rows).Where(row => row.Balance is not null).Select(row => row.Balance!.Value));
        Assert.Null(head.Sort(view.Rows)[^1].Balance);
    }

    [Fact]
    public void ASearchIgnoresCaseAndMatchesEventNamesOnBothPages()
    {
        Assert.Equal(["Upkeep"], CreditsModel.Matching(Everything().Kinds, "REPAIR").Select(row => row.Name));
        Assert.Equal(["Trade"], CreditsModel.Matching(Everything().Kinds, "trade").Select(row => row.Name));
        Assert.Empty(CreditsModel.Matching(Everything().Kinds, "zzz"));

        var view = All(query: "marketsell");
        Assert.Single(view.Rows);
        Assert.Equal(1, view.Shown);
        Assert.Equal(8, view.Entries);

        Assert.Equal(3, All(query: "UNACCOUNTED").Shown);
    }

    [Fact]
    public void TheTransactionsPageTotalsFollowTheKind()
    {
        Assert.Equal(500_000, All().In);
        Assert.Equal(105_000, All().Out);
        Assert.Equal(500_000, All(CreditKind.Trade).In);
        Assert.Equal(100_000, All(CreditKind.Trade).Out);
        Assert.Equal(200_000, All(CreditKind.CarrierBank).Out);
    }

    [Fact]
    public void EachPeriodHasItsEmptyText()
    {
        Assert.Equal("No credits moved in the last 24 hours.", CreditsModel.EmptyText(CombatPeriod.Day));
        Assert.Equal("No credits moved in the last 7 days.", CreditsModel.EmptyText(CombatPeriod.Week));
        Assert.Equal("No credits moved in the last 30 days.", CreditsModel.EmptyText(CombatPeriod.Month));
        Assert.Equal("No credits moved on record.", CreditsModel.EmptyText(CombatPeriod.Everything));
        Assert.Equal("No Rescue entries in the last 24 hours.", CreditsModel.EmptyKindText(CreditKind.Rescue, CombatPeriod.Day));
        Assert.Equal("No Rescue entries on record.", CreditsModel.EmptyKindText(CreditKind.Rescue, CombatPeriod.Everything));
        Assert.Equal(0, CreditsModel.Rows(CreditLedger.None, CombatPeriod.Week, Now, HistoryState.Done).Total);
    }

    [Fact]
    public void TheHistoryStatesSetTheRunningAndFailedFlagsAndTheNoticeTime()
    {
        Assert.True(Everything(HistoryState.Running).Running);
        Assert.True(Everything(HistoryState.Pending).Running);
        Assert.True(Everything(HistoryState.Failed).Failed);
        Assert.True(Everything(HistoryState.Stopped).Failed);
        Assert.False(Everything().Running || Everything().Failed);

        Assert.Equal(
            "D47 couldn't finish reading your older journals, so this ledger starts when D47 started, at 14:02 UTC today. Transactions from here on are still recorded.",
            CreditsModel.FailedText(DateTimeOffset.Parse("2026-10-10T14:02:30Z", CultureInfo.InvariantCulture)));
        Assert.Equal(Ledger(Lines).OnRecordSince, Everything().OnRecordSince);
    }
}
