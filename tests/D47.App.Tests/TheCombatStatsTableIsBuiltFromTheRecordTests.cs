using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Combat stats page's rows, sorts, search and texts (#1288), from values only.</summary>
public class TheCombatStatsTableIsBuiltFromTheRecordTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private const int Name = 0;
    private const int Kills = 1;
    private const int Credits = 2;

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CombatRecord Fold(params string[] lines) =>
        lines.Aggregate(CombatRecord.None, (record, line) => record.Apply(Event(line)));

    private static string Bounty(string time, string target, long total, string victim, string payers) =>
        At(time, "\"event\":\"Bounty\",\"Target\":\"" + target + "\",\"TotalReward\":" + total
            + ",\"VictimFaction\":\"" + victim + "\",\"Rewards\":[" + payers + "]");

    private static string Pay(string faction, long reward) => "{\"Faction\":\"" + faction + "\",\"Reward\":" + reward + "}";

    private static string Bond(string time, long reward, string payer) =>
        At(time, "\"event\":\"FactionKillBond\",\"Reward\":" + reward + ",\"AwardingFaction\":\"" + payer + "\",\"VictimFaction\":\"Losers\"");

    private static readonly string Sol = At("2026-10-10T09:00:00Z", "\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":10");
    private static readonly string Grey = At("2026-10-10T09:01:00Z", "\"event\":\"Loadout\",\"Ship\":\"anaconda\",\"ShipID\":7,\"ShipName\":\"Grey Lady\"");

    private static CombatRecord Week() => Fold(
        Sol,
        Grey,
        Bounty("2026-10-10T10:00:00Z", "eagle", 1000, "Pirates", Pay("Alpha", 1000)),
        Bounty("2026-10-10T10:01:00Z", "eagle", 3000, "Pirates", Pay("Alpha", 1000) + "," + Pay("Beta", 2000)),
        Bounty("2026-10-10T10:02:00Z", "anaconda", 9000, "Raiders", Pay("Beta", 9000)),
        Bond("2026-10-10T10:03:00Z", 500, "Holder"),
        At("2026-10-10T10:04:00Z", "\"event\":\"Disembark\",\"SRV\":false"),
        Bounty("2026-10-10T10:05:00Z", "assaultsuitai_class1", 200, "Pirates", Pay("Alpha", 200)));

    private static CombatStatsView View(
        CombatRecord? record = null,
        CombatPeriod period = CombatPeriod.Everything,
        CombatGrouping grouping = CombatGrouping.EnemyShip,
        HistoryState history = HistoryState.Done) =>
        CombatStatsPage.Rows(record ?? Week(), period, grouping, Now, history);

    private static string[] Order(CombatGrouping grouping, int column, SortDirection direction, CombatRecord? record = null)
    {
        var view = View(record, grouping: grouping);

        return [.. ColumnSort.Sorted(view.Rows, CombatStatsPage.Heads(grouping), new SortOrder(column, direction)).Select(row => row.Text)];
    }

    private static CombatRow Row(CombatStatsView view, string text) => view.Rows.Single(row => row.Text == text);

    [Fact]
    public void EnemyShipRowsAreKillsThenCreditsLargestFirstUntilAHeadingIsPressed() =>
        Assert.Equal(
            ["Eagle", "Anaconda", CombatStatsPage.BondText, "assaultsuitai"],
            View().Rows.Select(row => row.Text));

    [Fact]
    public void ANameSortsAToZIgnoringCaseWithTheBondLast() =>
        Assert.Equal(
            ["Anaconda", "assaultsuitai", "Eagle", CombatStatsPage.BondText],
            Order(CombatGrouping.EnemyShip, Name, SortDirection.Ascending));

    [Fact]
    public void KillsAndCreditsSortLargestFirst()
    {
        Assert.Equal(
            ["Eagle", "Anaconda", CombatStatsPage.BondText, "assaultsuitai"],
            Order(CombatGrouping.EnemyShip, Kills, SortDirection.Descending));
        Assert.Equal(
            ["Anaconda", "Eagle", CombatStatsPage.BondText, "assaultsuitai"],
            Order(CombatGrouping.EnemyShip, Credits, SortDirection.Descending));
    }

    [Fact]
    public void RowsWithoutTheValueSortLastEitherWay()
    {
        Assert.Equal(
            ["Pirates", "Raiders", "Losers", "Beta", "Alpha", "Holder"],
            Order(CombatGrouping.Faction, Kills, SortDirection.Descending));
        Assert.Equal(
            ["Beta", "Alpha", "Holder", "Pirates", "Raiders", "Losers"],
            Order(CombatGrouping.Faction, Credits, SortDirection.Descending));
        Assert.Equal(
            ["Holder", "Alpha", "Beta", "Pirates", "Raiders", "Losers"],
            Order(CombatGrouping.Faction, Credits, SortDirection.Ascending));
    }

    [Fact]
    public void TheFactionTableMergesVictimAndPayingListsWithADashForTheMissingSide()
    {
        var view = View(grouping: CombatGrouping.Faction);

        Assert.Equal((3L, (long?)null), (Row(view, "Pirates").Kills, Row(view, "Pirates").Awarded));
        Assert.Equal(((long?)null, 2200L), (Row(view, "Alpha").Kills, Row(view, "Alpha").Awarded));
        Assert.Equal(((long?)null, 11000L), (Row(view, "Beta").Kills, Row(view, "Beta").Awarded));
        Assert.Equal("—", CombatStatsPage.Number(Row(view, "Alpha").Kills));
        Assert.Equal("—", CombatStatsPage.Credits(Row(view, "Pirates").Awarded));
        Assert.Equal("2,200 CR", CombatStatsPage.Credits(Row(view, "Alpha").Awarded));
        Assert.Equal(6, view.Rows.Count);
    }

    [Fact]
    public void ABondHasNoShipTypeAndAnOnFootTargetIsMarkedOnFoot()
    {
        var view = View();

        Assert.Equal(CombatNameStyle.Unnamed, Row(view, CombatStatsPage.BondText).Style);
        Assert.Equal("on foot", Row(view, "assaultsuitai").Suffix);
        Assert.Null(Row(view, "Eagle").Suffix);
    }

    [Fact]
    public void OwnShipRowsNameTheHullTheCommanderOnFootAndASrv()
    {
        var record = Week().Apply(Event(At("2026-10-10T10:06:00Z", "\"event\":\"LaunchSRV\""))).Apply(
            Event(Bounty("2026-10-10T10:07:00Z", "eagle", 10, "Pirates", Pay("Alpha", 10))));
        var view = View(record, grouping: CombatGrouping.OwnShip);

        Assert.Equal(["Grey Lady (Anaconda)", "On foot", "In an SRV"], view.Rows.Select(row => row.Text));
        Assert.Equal((4L, 13500L), (view.Rows[0].Kills, view.Rows[0].Awarded));
    }

    [Fact]
    public void AShipTheRecordCannotNameIsSaidToBeAbsentFromTheJournals()
    {
        var record = Fold(Bounty("2026-10-10T10:00:00Z", "eagle", 10, "Pirates", Pay("Alpha", 10)));

        Assert.Equal([CombatStatsPage.UnnamedShip], View(record, grouping: CombatGrouping.OwnShip).Rows.Select(row => row.Text));
    }

    [Fact]
    public void SystemRowsNameTheSystemAndAnUnknownOneIsSaidToBeAbsentFromTheJournals()
    {
        var named = View(grouping: CombatGrouping.System).Rows;
        var unnamed = View(
            Fold(Bounty("2026-10-10T10:00:00Z", "eagle", 10, "Pirates", Pay("Alpha", 10))),
            grouping: CombatGrouping.System).Rows;

        Assert.Equal(("Sol", CombatNameStyle.System, 10L), (named[0].Text, named[0].Style, named[0].SystemAddress));
        Assert.Equal((CombatStatsPage.UnnamedSystem, CombatNameStyle.Unnamed), (unnamed[0].Text, unnamed[0].Style));
    }

    [Fact]
    public void ThePeriodCountsOnlyTheKillsInsideIt()
    {
        var record = Week().Apply(Event(Bounty("2026-09-01T10:00:00Z", "eagle", 70, "Pirates", Pay("Alpha", 70))));

        Assert.Equal(5, View(record, CombatPeriod.Week).Kills);
        Assert.Equal(5, View(record, CombatPeriod.Month).Kills);
        Assert.Equal(6, View(record, CombatPeriod.Everything).Kills);
        Assert.Equal(5, View(record, CombatPeriod.Day).Kills);
        Assert.Equal(Now - TimeSpan.FromHours(24), CombatStatsPage.PeriodStart(CombatPeriod.Day, Now));
        Assert.Null(CombatStatsPage.PeriodStart(CombatPeriod.Everything, Now));
    }

    [Fact]
    public void ASearchIgnoresCaseAndMatchesTheUnnamedTexts()
    {
        var rows = View().Rows;

        Assert.Equal(["Eagle"], CombatStatsPage.Matching(rows, "EAGLE").Select(row => row.Text));
        Assert.Equal([CombatStatsPage.BondText], CombatStatsPage.Matching(rows, "ship type not").Select(row => row.Text));
        Assert.Empty(CombatStatsPage.Matching(rows, "zzz"));
        Assert.Equal(rows.Count, CombatStatsPage.Matching(rows, "  ").Count);
    }

    [Theory]
    [InlineData(CombatPeriod.Day, "No kills in the last 24 hours.")]
    [InlineData(CombatPeriod.Week, "No kills in the last 7 days.")]
    [InlineData(CombatPeriod.Month, "No kills in the last 30 days.")]
    [InlineData(CombatPeriod.Everything, "No kills on record.")]
    public void EachPeriodHasItsOwnEmptyText(CombatPeriod period, string text) =>
        Assert.Equal(text, CombatStatsPage.EmptyText(period));

    [Theory]
    [InlineData(HistoryState.Pending, true, false)]
    [InlineData(HistoryState.Running, true, false)]
    [InlineData(HistoryState.Done, false, false)]
    [InlineData(HistoryState.Failed, false, true)]
    [InlineData(HistoryState.Stopped, false, true)]
    public void TheHistoryStateSetsRunningAndFailed(HistoryState history, bool running, bool failed)
    {
        var view = View(history: history);

        Assert.Equal((running, failed), (view.Running, view.Failed));
    }

    [Fact]
    public void TheFailedNoticeNamesTheTimeTheRecordStartsAt()
    {
        var view = View(history: HistoryState.Failed);

        Assert.Equal(
            "D47 couldn't finish reading your older journals, so these counts start at 09:00 UTC today. Kills from here on are still counted.",
            CombatStatsPage.FailedText(view.OnRecordSince));
    }

    [Fact]
    public void ACountReadsInTheSingularForOne()
    {
        Assert.Equal("35 targets", CombatStatsPage.CountText(CombatGrouping.EnemyShip, 35));
        Assert.Equal("1 ship", CombatStatsPage.CountText(CombatGrouping.OwnShip, 1));
        Assert.Equal("9 systems", CombatStatsPage.CountText(CombatGrouping.System, 9));
        Assert.Equal("11 factions", CombatStatsPage.CountText(CombatGrouping.Faction, 11));
    }
}
