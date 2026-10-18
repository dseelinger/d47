using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The BGS work page's rows, sorts, search and figures (#1287), from values only.</summary>
public class TheBgsWorkTableIsBuiltFromTheWorkTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T09:00:00Z", CultureInfo.InvariantCulture);

    private const int System = 0;
    private const int Missions = 1;
    private const int Combat = 2;
    private const int Trade = 3;
    private const int Data = 4;

    private const string Docked =
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""";

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Sale(string time, long total, long sell, long paid, int count) =>
        At(time, "\"event\":\"MarketSell\",\"MarketID\":100,\"Type\":\"gold\",\"Count\":" + count + ",\"SellPrice\":" + sell
            + ",\"TotalSale\":" + total + ",\"AvgPricePaid\":" + paid);

    private static string Voucher(string type, string rest) =>
        At("2026-10-10T10:06:00Z", "\"event\":\"RedeemVoucher\",\"Type\":\"" + type + "\"," + rest);

    private static string Mission(string faction, string trend, string marks) =>
        At("2026-10-10T10:07:00Z",
            "\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
            + "\",\"Influence\":[{\"SystemAddress\":2,\"Trend\":\"" + trend + "\",\"Influence\":\"" + marks + "\"}]}]");

    private static BgsWork Fold(params string[] lines) => lines.Aggregate(BgsWork.None, (work, line) => work.Apply(Event(line)));

    /// <summary>
    /// Alpha Mining: a profitable sale, a sale at a loss and cartography. Gamma Pirates: a bounty. Delta Party: a bond.
    /// Beta Union: three marks up and two down. Epsilon Front: one mark down. Beta and Epsilon are in a system with no name.
    /// </summary>
    private static BgsWork Week() => Fold(
        Docked,
        Sale("2026-10-10T10:05:00Z", 500000, 50000, 40000, 10),
        Sale("2026-10-10T10:05:30Z", 120000, 10000, 42200, 12),
        Voucher("bounty", "\"Factions\":[{\"Faction\":\"Gamma Pirates\",\"Amount\":3000}]"),
        Voucher("CombatBond", "\"Faction\":\"Delta Party\",\"Amount\":7000"),
        At("2026-10-10T10:08:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":123456"),
        Mission("Beta Union", "UpGood", "+++"),
        Mission("Beta Union", "DownBad", "++"),
        Mission("Epsilon Front", "DownBad", "+"));

    private static IReadOnlyList<BgsWorkRow> Rows(BgsWork? work = null, DateTimeOffset? tick = null) =>
        BgsWorkPage.Rows(work ?? Week(), tick, Now, SystemStandings.Empty);

    private static string[] Order(IReadOnlyList<BgsWorkRow> rows, int column, SortDirection direction) =>
        [.. ColumnSort.Sorted(rows, BgsWorkPage.Heads, new SortOrder(column, direction)).Select(row => row.Faction)];

    private static JournalEvent Jump(string at, double influence) =>
        Event("{\"timestamp\":\"" + at + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Alpha\",\"SystemAddress\":1,"
            + "\"Factions\":[{\"Name\":\"Alpha Mining\",\"Influence\":" + influence.ToString(CultureInfo.InvariantCulture) + "}]}");

    [Fact]
    public void RowsAreMostEntriesFirstUntilAHeadingIsPressed() =>
        Assert.Equal(
            ["Alpha Mining", "Beta Union", "Gamma Pirates", "Delta Party", "Epsilon Front"],
            Rows().Select(row => row.Faction));

    [Fact]
    public void MissionsSortByMarksUpMinusMarksDownWithEmptyRowsLast() =>
        Assert.Equal(
            ["Beta Union", "Epsilon Front", "Alpha Mining", "Gamma Pirates", "Delta Party"],
            Order(Rows(), Missions, SortDirection.Descending));

    [Fact]
    public void CombatSortsByBountiesPlusBondsWithEmptyRowsLast() =>
        Assert.Equal(
            ["Delta Party", "Gamma Pirates", "Alpha Mining", "Beta Union", "Epsilon Front"],
            Order(Rows(), Combat, SortDirection.Descending));

    [Fact]
    public void TradeSortsByProfitWithEmptyRowsLast() =>
        Assert.Equal(
            ["Alpha Mining", "Beta Union", "Gamma Pirates", "Delta Party", "Epsilon Front"],
            Order(Rows(), Trade, SortDirection.Descending));

    [Fact]
    public void DataSortsByCartographyPlusOrganicWithEmptyRowsLast() =>
        Assert.Equal(
            ["Alpha Mining", "Beta Union", "Gamma Pirates", "Delta Party", "Epsilon Front"],
            Order(Rows(), Data, SortDirection.Descending));

    [Fact]
    public void SystemSortsBySystemThenFactionAToZWithTheUnnamedSystemLast() =>
        Assert.Equal(
            ["Alpha Mining", "Delta Party", "Gamma Pirates", "Beta Union", "Epsilon Front"],
            Order(Rows(), System, SortDirection.Ascending));

    [Fact]
    public void ASystemNamedAsAPrefixOfAnotherSortsFirst()
    {
        var work = Fold(
            Docked,
            """{"timestamp":"2026-10-10T10:01:00Z","event":"Docked","StationName":"Far Port","MarketID":200,"StarSystem":"Alpha Centauri","SystemAddress":3,"StationFaction":{"Name":"Zeta Group"}}""",
            At("2026-10-10T10:02:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":10"),
            """{"timestamp":"2026-10-10T10:03:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""",
            At("2026-10-10T10:04:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":10"));

        Assert.Equal(
            ["Alpha Mining", "Zeta Group"],
            Order(BgsWorkPage.Rows(work, null, Now, SystemStandings.Empty), System, SortDirection.Ascending));
    }

    [Fact]
    public void ANegativeProfitHasATrueMinusAndAnAbsentValueIsADash()
    {
        Assert.Equal(-286400, Rows().Single(row => row.Faction == "Alpha Mining").Profit);
        Assert.Equal("−386,400 CR", BgsWorkPage.Credits(-386400));
        Assert.Equal("1,284,500 CR", BgsWorkPage.Credits(1284500));
        Assert.Equal("—", BgsWorkPage.Credits(null));
        Assert.Null(Rows().Single(row => row.Faction == "Gamma Pirates").Profit);
        Assert.Equal("+6", BgsWorkPage.MarksUpText(6));
        Assert.Equal("−3", BgsWorkPage.MarksDownText(3));
        Assert.Equal("—", BgsWorkPage.MarksDownText(null));
    }

    [Fact]
    public void WithNoTickTheWorkStartsTwentyFourHoursBack()
    {
        var work = Fold(
            Docked,
            At("2026-10-09T18:00:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":1000"),
            At("2026-10-09T21:00:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":2000"));

        Assert.Equal(2000, Assert.Single(Rows(work)).Cartography);
        Assert.Equal(Now - TimeSpan.FromHours(24), BgsWork.PeriodStart(null, Now));
        Assert.Equal(Tick, BgsWork.PeriodStart(Tick, Now));
    }

    [Fact]
    public void ATickTimeDropsTheWorkBeforeIt()
    {
        var tick = DateTimeOffset.Parse("2026-10-10T10:06:30Z", CultureInfo.InvariantCulture);

        Assert.Equal(
            ["Beta Union", "Epsilon Front", "Alpha Mining"],
            BgsWorkPage.Rows(Week(), tick, Now, SystemStandings.Empty).Select(row => row.Faction));
    }

    [Fact]
    public void SearchMatchesSystemOrFactionIgnoringCase()
    {
        var rows = Rows();

        Assert.Equal(
            ["Alpha Mining", "Delta Party", "Gamma Pirates"],
            BgsWorkPage.Matching(rows, "ALPHA").Select(row => row.Faction).Order());
        Assert.Equal(["Gamma Pirates"], BgsWorkPage.Matching(rows, "pirates").Select(row => row.Faction));
        Assert.Equal(rows, BgsWorkPage.Matching(rows, "  "));
        Assert.Empty(BgsWorkPage.Matching(rows, "nowhere"));
    }

    [Fact]
    public void ASystemTheJournalsCannotNameMatchesOnItsText()
    {
        var unnamed = BgsWorkPage.Matching(Rows(), "recent journals");

        Assert.Equal(["Beta Union", "Epsilon Front"], unnamed.Select(row => row.Faction).Order());
        Assert.All(unnamed, row => Assert.Equal(BgsWorkPage.UnnamedSystem, row.SystemText));
    }

    [Fact]
    public void InfluenceIsReadOnlyWithATickTime()
    {
        var standings = new[] { Jump("2026-10-10T08:00:00Z", 0.384), Jump("2026-10-10T19:42:00Z", 0.419) }
            .Aggregate(SystemStandings.Empty, (all, jump) => all.Apply(jump));

        var withTick = BgsWorkPage.Rows(Week(), Tick, Now, standings).Single(row => row.Faction == "Alpha Mining");
        var withoutTick = BgsWorkPage.Rows(Week(), null, Now, standings).Single(row => row.Faction == "Alpha Mining");

        Assert.Equal("38.4% → 41.9%", BgsWorkPage.InfluenceText(withTick.Influence!.Value));
        Assert.Equal("VISIT 19:42", BgsWorkPage.VisitText(withTick.Influence.Value));
        Assert.Null(withoutTick.Influence);
        Assert.Null(BgsWorkPage.Rows(Week(), Tick, Now, SystemStandings.Empty)[0].Influence);
    }

    [Fact]
    public void TheAgeIsInHoursAndMinutes()
    {
        Assert.Equal("3 hours 28 minutes ago", BgsWorkPage.Ago(new TimeSpan(3, 28, 30)));
        Assert.Equal("1 hour ago", BgsWorkPage.Ago(TimeSpan.FromMinutes(60)));
        Assert.Equal("1 hour 1 minute ago", BgsWorkPage.Ago(TimeSpan.FromMinutes(61)));
        Assert.Equal("28 minutes ago", BgsWorkPage.Ago(TimeSpan.FromMinutes(28)));
        Assert.Equal("less than a minute ago", BgsWorkPage.Ago(TimeSpan.FromSeconds(20)));
    }
}
