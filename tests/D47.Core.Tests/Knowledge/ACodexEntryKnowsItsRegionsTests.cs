using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The codex table gives an entry's name, subcategory and per-region system counts.</summary>
public class ACodexEntryKnowsItsRegionsTests
{
    private const string Tsv =
        "# header\n" +
        "2320501\tBacterium Vesicula - Gold\tBiological and Geological\tOrganic structures\t18:7999,34:12\n" +
        "2320502\tBacterium Other\tBiological and Geological\tOrganic structures\t18:40\n" +
        "1200402\tGreen Gas Giant\tAstronomical Bodies\tGas giant planets\t\n";

    private static readonly CodexTable Table = CodexTable.Parse(Tsv);

    [Fact]
    public void AnEntryCarriesItsNameSubCategoryAndRegionCounts()
    {
        var entry = Table.Entry(2320501);

        Assert.NotNull(entry);
        Assert.Equal("Bacterium Vesicula - Gold", entry.Name);
        Assert.Equal("Organic structures", entry.SubCategory);
        Assert.Equal(7999, entry.Regions[18]);
        Assert.Equal(12, entry.Regions[34]);
    }

    [Fact]
    public void AnEntryWithNoRegionsIsFoundButIsInNoRegion()
    {
        var entry = Table.Entry(1200402);

        Assert.NotNull(entry);
        Assert.Empty(entry.Regions);
        Assert.DoesNotContain(Table.InRegion(18), found => found.Entry.EntryId == 1200402);
    }

    [Fact]
    public void ARegionListsItsEntriesMostReportedFirst()
    {
        var ids = Table.InRegion(18).Select(found => found.Entry.EntryId).ToArray();

        Assert.Equal([2320501L, 2320502L], ids);
    }

    [Fact]
    public void AnUnknownEntryIsNull() => Assert.Null(Table.Entry(1));

    [Fact]
    public void TheShippedTableHasGoldBacteriumInInnerOrionSpur()
    {
        var found = CodexCatalogue.InRegion(18).Single(item => item.Entry.EntryId == 2320501);

        Assert.Equal("Bacterium Vesicula - Gold", found.Entry.Name);
        Assert.True(found.Systems > 0);
    }
}
