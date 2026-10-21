using System.Globalization;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Discoveries column and the Other regions list on Commander › Ledgers › Codex (#1356).</summary>
public class TheCodexRegionsPageListsEveryRegionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse(
        "101\tA\tBiological and Geological\tOrganic structures\t18:5,35:2\n"
        + "102\tB\tBiological and Geological\tOrganic structures\t18:3\n"
        + "103\tC\tBiological and Geological\tOrganic structures\t35:1,3:4\n");

    private static string Codex(string time, long id, int region) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_" + region
        + ";\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}";

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

    private static readonly string[] Lines =
    [
        Codex("2026-09-01T10:00:00Z", 103, 35),
        "{\"timestamp\":\"2026-10-03T10:00:00Z\",\"event\":\"SellExplorationData\"}",
        Codex("2026-10-04T10:00:00Z", 101, 18),
    ];

    [Fact]
    public void EachRegionRowShowsItsRecordAgainstWhatIsKnownWhateverThePeriod()
    {
        var ledger = Ledger(Lines);

        foreach (var period in Enum.GetValues<CodexPeriod>())
        {
            var inner = CodexModel.Regions(ledger, period, Now, HistoryState.Done, table: Table).Rows.SingleOrDefault(row => row.RegionId == 18);

            if (inner is not null)
            {
                Assert.Equal((1, 2), (inner.Recorded, inner.Known));
            }
        }
    }

    [Fact]
    public void ARegionWithNothingInThePeriodIsListedUnderOtherRegionsWithItsLastLogged()
    {
        var view = CodexModel.Regions(Ledger(Lines), CodexPeriod.Trip, Now, HistoryState.Done, table: Table);

        Assert.Equal(18, Assert.Single(view.Rows).RegionId);

        var outer = Assert.Single(view.Others, row => row.RegionId == 35);
        Assert.True(outer.Other);
        Assert.Equal("Last logged 2026-09-01", outer.Detail);
        Assert.Equal((1, 2), (outer.Recorded, outer.Known));

        var never = Assert.Single(view.Others, row => row.RegionId == 3);
        Assert.Equal("Nothing on record", never.Detail);
        Assert.Equal((0, 1), (never.Recorded, never.Known));

        Assert.Equal(2, view.OtherRegions);
        Assert.Equal(1, view.Regions);
    }

    [Fact]
    public void UnderEverythingTheListHoldsOnlyRegionsNeverLogged()
    {
        var view = CodexModel.Regions(Ledger(Lines), CodexPeriod.Everything, Now, HistoryState.Done, table: Table);

        Assert.Equal(3, Assert.Single(view.Others).RegionId);
        Assert.Equal("Nothing on record. Open one to see what is left to discover.", view.OtherLine);
    }

    [Fact]
    public void TheListLineNamesThePeriod()
    {
        Assert.Equal("Nothing logged this trip. Open one to see what is left to discover.", CodexModel.OtherRegionsLine(CodexPeriod.Trip));
        Assert.Equal("Nothing logged this session. Open one to see what is left to discover.", CodexModel.OtherRegionsLine(CodexPeriod.Session));
    }

    [Fact]
    public void ASearchNarrowsOtherRegionsByNameAndLeavesTheirTotal()
    {
        var view = CodexModel.Regions(Ledger(Lines), CodexPeriod.Trip, Now, HistoryState.Done, "outer orion", Table);

        Assert.Equal(35, Assert.Single(view.Others).RegionId);
        Assert.Equal(2, view.OtherRegions);
        Assert.Empty(CodexModel.Regions(Ledger(Lines), CodexPeriod.Trip, Now, HistoryState.Done, "zzz", Table).Others);
    }

    [Fact]
    public void AnEmptyPeriodStillListsEveryRegion()
    {
        var view = CodexModel.Regions(CodexLedger.None, CodexPeriod.Everything, Now, HistoryState.Done, table: Table);

        Assert.NotNull(view.Empty);
        Assert.Equal(3, view.OtherRegions);
        Assert.All(view.Others, row => Assert.Equal(0, row.Recorded));
    }

    [Fact]
    public void TheDiscoveriesHeadSortsBothListsAndTheOtherRowsSortAsZeroInThePeriodColumns()
    {
        var view = CodexModel.Regions(Ledger(Lines), CodexPeriod.Trip, Now, HistoryState.Done, table: Table);
        var head = new SortHeader<CodexRegionRow>("t", CodexModel.RegionHeads());

        Assert.Equal("Discoveries", CodexModel.RegionHeads()[4].Label);
        Assert.Equal("Of known · whole record", CodexModel.RegionHeads()[4].Second);

        head.Press(3);
        Assert.Equal(new[] { 0, 0 }, head.Sort(view.Others).Select(row => row.Entries));
    }
}
