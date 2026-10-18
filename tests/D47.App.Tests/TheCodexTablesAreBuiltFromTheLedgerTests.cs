using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The rows behind Commander › Ledgers › Codex, built from a ledger, a period, a region and a search (#1290).</summary>
public class TheCodexTablesAreBuiltFromTheLedgerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static string Codex(
        string time,
        long id,
        string region,
        string sub,
        string system = "Sol",
        bool isNew = false,
        string category = "Biological and Geological",
        string extra = ",\"Latitude\":12.4512,\"Longitude\":-108.2231") =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"" + category
        + "\",\"SubCategory\":\"$Codex_SubCategory_X;\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"" + region
        + "\",\"System\":\"" + system + "\",\"SystemAddress\":1,\"BodyID\":5" + (isNew ? ",\"IsNewEntry\":true" : string.Empty) + extra + "}";

    private static string Line(string time, string kind) => "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\"}";

    private static readonly string[] Lines =
    [
        Codex("2026-09-01T10:00:00Z", 101, "$Codex_RegionName_18;", "Organic structures", isNew: true),
        Line("2026-10-01T09:00:00Z", "LoadGame"),
        Codex("2026-10-02T10:00:00Z", 101, "$Codex_RegionName_18;", "Organic structures"),
        Line("2026-10-03T10:00:00Z", "SellExplorationData"),
        Codex("2026-10-04T10:00:00Z", 102, "$Codex_RegionName_18;", "Stars", "Alpha", true, "Stellar"),
        Codex("2026-10-05T10:00:00Z", 103, "$Codex_RegionName_18;", "Organic structures"),
        Line("2026-10-06T10:00:00Z", "LoadGame"),
        Codex("2026-10-07T10:00:00Z", 104, "$Codex_RegionName_35;", "Geology", "Beta", true, extra: string.Empty),
        Codex("2026-10-07T11:00:00Z", 105, "$Codex_RegionName_99;", "Stars", "Gamma", true, "Stellar"),
        Codex("2026-10-07T12:00:00Z", 106, "nonsense", "Stars", "Delta", true, "Stellar"),
    ];

    private static CodexLedger Ledger(params string[] lines)
    {
        var ledger = CodexLedger.None;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            ledger = ledger.Apply(parsed!);
        }

        return ledger;
    }

    private static CodexRootView Regions(CodexPeriod period, string? query = null, HistoryState history = HistoryState.Done, params string[] lines) =>
        CodexModel.Regions(Ledger(lines.Length == 0 ? Lines : lines), period, Now, history, query);

    private static CodexRegionView Entries(
        CodexPeriod period,
        int? region = 18,
        string? query = null,
        CodexLedger? ledger = null,
        string? here = "Alpha") =>
        CodexModel.Entries(
            ledger ?? Ledger(Lines), period, region, Now, HistoryState.Done, here, (_, body) => body == 5 ? "Sol A 4 c" : null, query);

    [Fact]
    public void ThisTripIsEverythingWhenNoExplorationDataWasSold()
    {
        var view = Regions(CodexPeriod.Trip, null, HistoryState.Done, Lines[0], Lines[1]);

        Assert.Equal(1, view.Entries);
        Assert.Equal("No sale of exploration data on record, so this trip is everything since 2026-09-01.", view.Line);
    }

    [Fact]
    public void EachPeriodNamesItsStartInTheGroupHeadLine()
    {
        Assert.Equal("Since you sold exploration data on 3 October at 10:00 UTC.", Regions(CodexPeriod.Trip).Line);
        Assert.Equal("Since you loaded the game on 6 October at 10:00 UTC.", Regions(CodexPeriod.Session).Line);
        Assert.Equal(
            "Since you loaded the game at 08:00 UTC today.",
            Regions(CodexPeriod.Session, null, HistoryState.Done, Lines[0], Line("2026-10-07T08:00:00Z", "LoadGame")).Line);
        Assert.Equal("Everything on record, since 2026-09-01.", Regions(CodexPeriod.Everything).Line);
    }

    [Fact]
    public void TheStripCountsTheEntriesTheNewOnesTheDistinctEntriesAndTheRegions()
    {
        var everything = Regions(CodexPeriod.Everything);

        Assert.Equal((7, 5, 6, 4), (everything.Entries, everything.New, everything.Logged, everything.Regions));

        var trip = Regions(CodexPeriod.Trip);

        Assert.Equal((5, 4, 5, 4), (trip.Entries, trip.New, trip.Logged, trip.Regions));
        Assert.Equal(3, Regions(CodexPeriod.Session).Entries);
    }

    [Fact]
    public void TheRegionsAreOrderedMostEntriesFirstAndAnUnknownRegionIsNotNamed()
    {
        var rows = Regions(CodexPeriod.Everything).Rows;

        Assert.Equal(
            ["Inner Orion Spur", "A region D47 cannot name", "A region D47 cannot name", "Outer Orion Spur"],
            rows.Select(row => row.Name));
        Assert.Equal(18, rows[0].RegionId);
        Assert.Equal(4, rows[0].Entries);
        Assert.False(rows.Single(row => row.RegionId == 99).Named);
        Assert.False(rows.Single(row => row.RegionId is null).Named);
    }

    [Fact]
    public void ARegionsLineGivesItsThreeLargestSubcategories()
    {
        var lines = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            lines.Add(Codex("2026-10-01T1" + i + ":00:00Z", 200 + i, "$Codex_RegionName_18;", "Organic structures"));
        }

        lines.Add(Codex("2026-10-02T10:00:00Z", 210, "$Codex_RegionName_18;", "Stars"));
        lines.Add(Codex("2026-10-02T11:00:00Z", 211, "$Codex_RegionName_18;", "Stars"));
        lines.Add(Codex("2026-10-02T12:00:00Z", 212, "$Codex_RegionName_18;", "Geology"));
        lines.Add(Codex("2026-10-02T13:00:00Z", 213, "$Codex_RegionName_18;", "Anomalies"));

        var row = Assert.Single(Regions(CodexPeriod.Everything, null, HistoryState.Done, [.. lines]).Rows);

        Assert.Equal("Organic structures 3 · Stars 2 · Anomalies 1", row.Detail);
    }

    [Fact]
    public void EachRegionHeadingSortsAndNoHeadingIsMarkedByDefault()
    {
        var rows = Regions(CodexPeriod.Everything).Rows;
        var head = new SortHeader<CodexRegionRow>("t", CodexModel.RegionHeads());

        Assert.Null(head.Order);
        Assert.Equal(18, head.Sort(rows)[0].RegionId);

        head.Press(0);
        Assert.Equal(["A region D47 cannot name", "A region D47 cannot name", "Inner Orion Spur", "Outer Orion Spur"], head.Sort(rows).Select(row => row.Name));

        var byEntries = new SortHeader<CodexRegionRow>("t", CodexModel.RegionHeads());
        byEntries.Press(3);
        Assert.Equal(18, byEntries.Sort(rows)[0].RegionId);

        var byNew = new SortHeader<CodexRegionRow>("t", CodexModel.RegionHeads());
        byNew.Press(1);
        Assert.Equal(new[] { 2, 1, 1, 1 }, byNew.Sort(rows).Select(row => row.New));
    }

    [Fact]
    public void ARegionSearchKeepsRegionsByNameOrByAnEntryAndCountsTheMatches()
    {
        var byName = Assert.Single(Regions(CodexPeriod.Everything, "INNER ORION").Rows);
        Assert.Equal(18, byName.RegionId);
        Assert.Equal("Organic structures 3 · Stars 1", byName.Detail);

        var bySystem = Assert.Single(Regions(CodexPeriod.Everything, "alpha").Rows);
        Assert.Equal(18, bySystem.RegionId);
        Assert.Equal("1 of 4 entries match", bySystem.Detail);

        var byEntry = Assert.Single(Regions(CodexPeriod.Everything, "entry 104").Rows);
        Assert.Equal(35, byEntry.RegionId);

        Assert.Empty(Regions(CodexPeriod.Everything, "zzz").Rows);
        Assert.Equal(4, Regions(CodexPeriod.Everything, "zzz").Regions);
    }

    [Fact]
    public void ANothingPeriodReadsItsOwnEmptyText()
    {
        var sold = Ledger(Lines[0], Lines[3]);

        Assert.Equal("No codex entries since you sold exploration data on 3 October.", CodexModel.Regions(sold, CodexPeriod.Trip, Now, HistoryState.Done).Empty);
        Assert.Equal("No codex entries this session.", CodexModel.Regions(Ledger(Lines[1]), CodexPeriod.Session, Now, HistoryState.Done).Empty);
        Assert.Equal("No codex entries on record.", CodexModel.Regions(CodexLedger.None, CodexPeriod.Everything, Now, HistoryState.Done).Empty);
    }

    [Fact]
    public void ARegionsEmptyTextNamesTheRegion()
    {
        var sold = Ledger(Lines[0], Lines[3]);

        Assert.Equal(
            "No codex entries in Inner Orion Spur since you sold exploration data on 3 October.",
            CodexModel.Entries(sold, CodexPeriod.Trip, 18, Now, HistoryState.Done, null, (_, _) => null).Empty);
        Assert.Equal("No codex entries in Outer Orion Spur this session.", Entries(CodexPeriod.Session, 35, null, Ledger(Lines[0], Lines[6])).Empty);
        Assert.Equal("No codex entries in Outer Orion Spur on record.", Entries(CodexPeriod.Everything, 35, null, Ledger(Lines[0])).Empty);
    }

    [Fact]
    public void TheHistoryStateShowsRunningOrFailedAndTheFailedTimeComesFromTheRecord()
    {
        Assert.True(Regions(CodexPeriod.Trip, null, HistoryState.Running).Running);
        Assert.True(Regions(CodexPeriod.Trip, null, HistoryState.Pending).Running);

        var failed = Regions(CodexPeriod.Trip, null, HistoryState.Stopped);

        Assert.True(failed.Failed);
        Assert.False(failed.Running);
        Assert.Equal(
            "D47 couldn't finish reading your older journals, so this ledger starts when D47 started, at 09:00 UTC today. Codex entries from here on are still recorded.",
            CodexModel.FailedText(DateTimeOffset.Parse("2026-10-07T09:00:00Z", CultureInfo.InvariantCulture)));
        Assert.Equal("2026-10-07 09:00", CodexModel.OnRecord(DateTimeOffset.Parse("2026-10-07T09:00:00Z", CultureInfo.InvariantCulture), true));
        Assert.Equal("2026-10-07", CodexModel.OnRecord(DateTimeOffset.Parse("2026-10-07T09:00:00Z", CultureInfo.InvariantCulture), false));
    }

    [Fact]
    public void TheEntriesAreGroupedLargestGroupFirstAndNewestFirstWithinIt()
    {
        var view = Entries(CodexPeriod.Everything);

        Assert.Equal(
            ["Biological and Geological · Organic structures", "Stellar · Stars"],
            view.Groups.Select(group => group.Label));
        Assert.Equal([3, 1], view.Groups.Select(group => group.Count));
        Assert.Equal(
            [DateTimeOffset.Parse("2026-10-05T10:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-10-02T10:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-09-01T10:00:00Z", CultureInfo.InvariantCulture)],
            view.Groups[0].Rows.Select(row => row.At));
        Assert.Equal((4, 2, 3, 2), (view.Entries, view.New, view.Logged, view.Subcategories));
    }

    [Fact]
    public void TheEntriesTableSortsWithinEachGroupAndTheGroupsKeepTheirOrder()
    {
        var view = Entries(CodexPeriod.Everything);
        var organic = view.Groups[0].Rows;

        var status = new SortHeader<CodexEntryRow>("t", CodexModel.EntryHeads());
        status.Press(3);
        Assert.Equal([true, false, false], status.Sort(organic).Select(row => row.IsNew));
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T10:00:00Z", CultureInfo.InvariantCulture), status.Sort(organic)[1].At);

        var time = new SortHeader<CodexEntryRow>("t", CodexModel.EntryHeads());
        time.Press(2);
        Assert.Equal(organic.Select(row => row.At), time.Sort(organic).Select(row => row.At));
        time.Press(2);
        Assert.Equal(organic.Select(row => row.At).Reverse(), time.Sort(organic).Select(row => row.At));

        var entry = new SortHeader<CodexEntryRow>("t", CodexModel.EntryHeads());
        entry.Press(0);
        Assert.Equal(["Entry 101", "Entry 101", "Entry 103"], entry.Sort(organic).Select(row => row.Name));

        var more = Entries(CodexPeriod.Everything, 18, null, Ledger([.. Lines, Codex("2026-10-08T10:00:00Z", 107, "$Codex_RegionName_18;", "Organic structures", "Zeta")]));
        var mixed = more.Groups[0].Rows;

        var system = new SortHeader<CodexEntryRow>("t", CodexModel.EntryHeads());
        system.Press(1);
        Assert.Equal(["Sol", "Sol", "Sol", "Zeta"], system.Sort(mixed).Select(row => row.System));
        system.Press(1);
        Assert.Equal(["Zeta", "Sol", "Sol", "Sol"], system.Sort(mixed).Select(row => row.System));
    }

    [Fact]
    public void AnAlreadyLoggedEntryGivesTheDateOfTheFirstEntryFromOutsideThePeriod()
    {
        var view = Entries(CodexPeriod.Trip);
        var row = view.Groups.SelectMany(group => group.Rows).Single(candidate => candidate.Name == "Entry 103");

        Assert.False(row.IsNew);
        Assert.Null(row.First);

        var again = Entries(CodexPeriod.Session, 18, null, Ledger([.. Lines, Codex("2026-10-07T13:00:00Z", 101, "$Codex_RegionName_18;", "Organic structures")]));
        var logged = Assert.Single(again.Groups.SelectMany(group => group.Rows));

        Assert.Equal(DateTimeOffset.Parse("2026-09-01T10:00:00Z", CultureInfo.InvariantCulture), logged.First);
        Assert.Null(logged.Origin);
        Assert.Equal("2026-09-01", CodexModel.Date(logged.First!.Value));
    }

    [Fact]
    public void AnAlreadyLoggedEntryWithNoEarlierOneSaysBeforeYourJournalsOnceTheHistoryIsWhole()
    {
        var live = Ledger(Lines);
        var row = Entries(CodexPeriod.Everything).Groups.SelectMany(group => group.Rows).Single(candidate => candidate.Name == "Entry 103");
        var whole = Entries(CodexPeriod.Everything, 18, null, CodexLedger.None.With(live)).Groups.SelectMany(group => group.Rows).Single(candidate => candidate.Name == "Entry 103");

        Assert.Equal("before D47 started", row.Origin);
        Assert.Equal("before your journals", whole.Origin);
        Assert.Null(Entries(CodexPeriod.Everything).Groups[0].Rows[^1].Origin);
    }

    [Fact]
    public void TheBodyAndPositionLineHandlesANameNoNameAndNeither()
    {
        Assert.Equal("A 4 c · 12.4512, −108.2231", CodexModel.Place("Sol", "Sol A 4 c", 12.4512, -108.2231));
        Assert.Equal("12.4512, −108.2231", CodexModel.Place("Sol", null, 12.4512, -108.2231));
        Assert.Equal("A 4 c", CodexModel.Place("Sol", "Sol A 4 c", null, null));
        Assert.Equal(string.Empty, CodexModel.Place("Sol", null, null, null));

        var rows = Entries(CodexPeriod.Everything).Groups.SelectMany(group => group.Rows).ToList();

        Assert.Equal("A 4 c · 12.4512, −108.2231", rows.First(row => row.Name == "Entry 103").Place);
        Assert.Equal("Sol A 4 c · 12.4512, −108.2231", rows.First(row => row.Name == "Entry 102").Place);
        Assert.True(rows.Single(row => row.Name == "Entry 102").IsHere);
        Assert.False(rows.First(row => row.Name == "Entry 103").IsHere);
        Assert.Equal("Sol A 4 c", Entries(CodexPeriod.Everything, 35).Groups[0].Rows[0].Place);
    }

    [Fact]
    public void AnEntrySearchKeepsEntriesByNameOrSystemAndAllOfThemWhenTheRegionNameMatches()
    {
        Assert.Equal(1, Entries(CodexPeriod.Everything, 18, "ALPHA").Shown);
        Assert.Equal(1, Entries(CodexPeriod.Everything, 18, "entry 103").Shown);
        Assert.Equal(4, Entries(CodexPeriod.Everything, 18, "orion spur").Shown);
        Assert.Equal(0, Entries(CodexPeriod.Everything, 18, "zzz").Shown);
        Assert.Equal(4, Entries(CodexPeriod.Everything, 18, "zzz").Entries);
    }
}
