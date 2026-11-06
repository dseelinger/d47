using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The rows, counts, ages and sorts the Faction page draws, without drawing it.</summary>
public sealed class TheFactionPageCountsItsSystemsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T12:00:00Z", CultureInfo.InvariantCulture);

    private static FactionSystem Sys(
        string name,
        double? influence = 0.3,
        string[]? active = null,
        string[]? pending = null,
        bool controls = false,
        DateTimeOffset? reported = null) =>
        new()
        {
            Name = name,
            Influence = influence,
            ActiveStates = active ?? [],
            PendingStates = pending ?? [],
            Controls = controls,
            ReportedAt = reported ?? Now.AddHours(-1),
        };

    private static FactionReport Report(params FactionSystem[] systems) =>
        new("Kestrel Reach Compact", "Confederacy", "Independent", systems, []);

    private static SystemStandings Saw(string system, string faction, double influence, string at, string active)
    {
        var json = "{\"timestamp\":\"" + at + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":1,"
            + "\"Factions\":[{\"Name\":\"" + faction + "\",\"Influence\":" + influence.ToString(CultureInfo.InvariantCulture)
            + ",\"ActiveStates\":[" + active + "]}]}";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        return SystemStandings.Empty.Apply(parsed!);
    }

    private static SortHeader<FactionRow> Header() => new("T", FactionModel.Heads(() => Now), FactionModel.DefaultOrder);

    [Fact]
    public void OverviewCountsActiveStatesLargestFirstAndLeavesOutZeroes()
    {
        var report = Report(
            Sys("A", active: ["Expansion"]),
            Sys("B", active: ["Expansion", "War"]),
            Sys("C", active: ["Boom"]),
            Sys("D", active: ["Expansion"], pending: ["Election"]));

        var overview = FactionModel.Overview(report, FactionRows.Of(report, SystemStandings.Empty));

        Assert.Equal(4, overview.Present);
        Assert.Equal(["Boom"], overview.Groups[0].States.Select(s => s.Name));
        Assert.Empty(overview.Groups[1].States);
        Assert.Equal(["War"], overview.Groups[2].States.Select(s => s.Name));
        var expansion = overview.Groups[3].States[0];
        Assert.Equal(("Expansion", 3, "75%"), (expansion.Name, expansion.Count, expansion.Share));
    }

    [Theory]
    [InlineData(0, 590, "<1%")]
    [InlineData(1, 590, "<1%")]
    [InlineData(6, 590, "1%")]
    [InlineData(295, 590, "50%")]
    public void AShareBelowOnePercentReadsLessThanOne(int count, int present, string expected) =>
        Assert.Equal(expected, FactionModel.Share(count, present));

    [Fact]
    public void ASystemIsInAConflictWhenAWarCivilWarOrElectionIsActiveOrPending()
    {
        var report = Report(
            Sys("A", active: ["Civil war"]),
            Sys("B", pending: ["Election"]),
            Sys("C", active: ["Expansion"], pending: ["Boom"]),
            Sys("D", active: ["War"]));

        var overview = FactionModel.Overview(report, FactionRows.Of(report, SystemStandings.Empty));

        Assert.Equal(3, overview.InConflict);
    }

    [Theory]
    [InlineData("CivilUnrest", "Civil unrest", "Security")]
    [InlineData("Civil Unrest", "Civil unrest", "Security")]
    [InlineData("InfrastructureFailure", "Infrastructure failure", "Other")]
    [InlineData("War", "War", "Conflict")]
    [InlineData("SomethingNew", "Something new", "Other")]
    public void AStateIsSpelledTheSameFromTheJournalAndFromSpansh(string raw, string shown, string group)
    {
        Assert.Equal(shown, FactionStates.Display(raw));
        Assert.Equal(group, FactionStates.GroupOf(raw));
    }

    [Fact]
    public void TheCommandersOwnReadingReplacesASpanshReportItIsNewerThan()
    {
        var report = Report(Sys("Alpha", 0.2, ["Boom"], reported: Now.AddDays(-9)));
        var standings = Saw("Alpha", "kestrel reach compact", 0.45, "2026-11-06T10:00:00Z", "{\"State\":\"War\"}");

        var row = Assert.Single(FactionRows.Of(report, standings));

        Assert.True(row.OwnReading);
        Assert.Equal(0.45, row.Influence);
        Assert.Equal(["War"], row.Active);
        Assert.Equal("you saw it 2 hours ago", FactionModel.Age(row, Now));
        Assert.False(FactionModel.IsOld(row, Now));
    }

    [Fact]
    public void ASpanshReportNewerThanTheCommandersReadingIsKept()
    {
        var report = Report(Sys("Alpha", 0.2, ["Boom"], reported: Now.AddMinutes(-5)));
        var standings = Saw("Alpha", "Kestrel Reach Compact", 0.45, "2026-11-06T10:00:00Z", string.Empty);

        var row = Assert.Single(FactionRows.Of(report, standings));

        Assert.False(row.OwnReading);
        Assert.Equal(0.2, row.Influence);
        Assert.Equal("5 minutes ago", FactionModel.Age(row, Now));
    }

    [Fact]
    public void TheOverviewCountsTheMergedRows()
    {
        var report = Report(Sys("Alpha", 0.2, ["Boom"], reported: Now.AddDays(-9)));
        var standings = Saw("Alpha", "Kestrel Reach Compact", 0.45, "2026-11-06T10:00:00Z", "{\"State\":\"War\"}");

        var overview = FactionModel.Overview(report, FactionRows.Of(report, standings));

        Assert.Equal(1, overview.InConflict);
        Assert.Empty(overview.Groups[0].States);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(31, true)]
    public void ASpanshReportOverThirtyDaysOldIsMarkedOld(int days, bool old)
    {
        var report = Report(Sys("Alpha", reported: Now.AddDays(-days)));

        Assert.Equal(old, FactionModel.IsOld(Assert.Single(FactionRows.Of(report, SystemStandings.Empty)), Now));
    }

    [Fact]
    public void RowsComeMostInfluenceFirstAndThirdPressReturnsToThat()
    {
        var rows = FactionRows.Of(Report(Sys("Low", 0.1), Sys("High", 0.7), Sys("Mid", 0.4)), SystemStandings.Empty);
        var head = Header();

        Assert.Equal(["High", "Mid", "Low"], head.Sort(rows).Select(r => r.System));

        head.Press(1);
        Assert.Equal(["High", "Mid", "Low"], head.Sort(rows).Select(r => r.System));
        head.Press(1);
        Assert.Equal(["Low", "Mid", "High"], head.Sort(rows).Select(r => r.System));
        head.Press(1);
        Assert.Null(head.Order);
        Assert.Equal(["High", "Mid", "Low"], head.Sort(rows).Select(r => r.System));
    }

    [Fact]
    public void SystemsWithNoStateSortLastAndControllingSystemsFirst()
    {
        var rows = FactionRows.Of(
            Report(Sys("Plain", 0.5), Sys("Boomer", 0.4, ["Boom"], controls: true), Sys("Warrior", 0.3, ["War"])),
            SystemStandings.Empty);
        var head = Header();

        head.Press(2);
        Assert.Equal(["Boomer", "Warrior", "Plain"], head.Sort(rows).Select(r => r.System));
        head.Press(2);
        Assert.Equal(["Warrior", "Boomer", "Plain"], head.Sort(rows).Select(r => r.System));

        var byControl = Header();
        byControl.Press(5);
        Assert.Equal("Boomer", byControl.Sort(rows)[0].System);
    }

    [Fact]
    public void UpdatedSortsNewestFirstOnTheFirstPress()
    {
        var rows = FactionRows.Of(
            Report(
                Sys("Old", reported: Now.AddDays(-40)),
                Sys("New", reported: Now.AddMinutes(-3)),
                Sys("Mid", reported: Now.AddDays(-2))),
            SystemStandings.Empty);
        var head = Header();

        head.Press(6);

        Assert.Equal(["New", "Mid", "Old"], head.Sort(rows).Select(r => r.System));
    }

    [Fact]
    public void ASearchKeepsTheSystemsWhoseNameContainsIt()
    {
        var rows = FactionRows.Of(Report(Sys("Hollin Reach"), Sys("Varnek 4"), Sys("Tessaly Drift")), SystemStandings.Empty);

        Assert.Equal(["Hollin Reach"], FactionModel.Matching(rows, " hollin ").Select(r => r.System));
        Assert.Equal("1 OF 3 SYSTEMS", FactionModel.CountText(1, 3));
        Assert.Equal("3 SYSTEMS", FactionModel.CountText(3, 3));
    }

    [Theory]
    [InlineData(0, 590, 0, "1–25 of 590")]
    [InlineData(1, 590, 1, "26–50 of 590")]
    [InlineData(99, 590, 23, "576–590 of 590")]
    [InlineData(5, 0, 0, "")]
    public void ThePagerReadsItsRangeAndStaysInsideTheRows(int asked, int total, int page, string readout)
    {
        Assert.Equal(page, FactionModel.ClampPage(asked, total));
        Assert.Equal(readout, FactionModel.PageReadout(page, total));
    }
}
