using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Star and planet types come from <c>CodexBodies.tsv</c>, read beside <c>Codex.tsv</c> as one table, so a region's
/// Discoveries list them and a Commander's star or planet entry reads as on record.
/// </summary>
public class AStarOrPlanetTypeIsACodexEntryTests
{
    private const string Codex =
        "2320501\tBacterium Vesicula - Gold\tBiological and Geological\tOrganic structures\t18:40\n";

    private const string Bodies =
        "1102300\tNeutron Star\tAstronomical Bodies\tStars\tNeutron Star\tVII\t\t\t18:900,34:12\n"
        + "1301602\tRocky Body with Atmosphere, Terraformable\tAstronomical Bodies\tTerrestrial planets\tRocky body\t\tyes\tyes\t18:300\n";

    private static readonly CodexTable Table = CodexTable.Parse(Codex + Bodies);

    private static CodexLedger Ledger(params (long Id, string SubCategory)[] entries) =>
        JournalScript.Store(
        [
            """{"timestamp":"2026-07-03T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-07-03T09:01:00Z","event":"LoadGame","Credits":1}""",
            .. entries.Select(entry =>
                "{\"timestamp\":\"2026-10-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + entry.Id
                + ",\"Name_Localised\":\"Entry\",\"SubCategory_Localised\":\"" + entry.SubCategory
                + "\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"IsNewEntry\":true}"),
        ]).Active!.CodexLedger;

    [Fact]
    public void ABodyRowCarriesTheSpanshFieldsThatSelectIt()
    {
        var entry = Table.Entry(1301602);

        Assert.NotNull(entry);
        Assert.Equal("Terrestrial planets", entry.SubCategory);
        Assert.Equal(new CodexBodyType("Rocky body", "", "yes", "yes"), entry.Body);
        Assert.Equal(300, entry.Regions[18]);
        Assert.Equal(new CodexBodyType("Neutron Star", "VII", "", ""), Table.Entry(1102300)!.Body);
    }

    [Fact]
    public void ACodexRowHasNoBodyFields() => Assert.Null(Table.Entry(2320501)!.Body);

    [Fact]
    public void AStarEntryInTheLedgerIsOnRecordAndNotUncovered()
    {
        var discoveries = CodexDiscoveries.For(Ledger((1102300, "Stars")), Table, 18);

        Assert.Equal(1102300, Assert.Single(discoveries.Logged).Entry.EntryId);
        Assert.Empty(discoveries.Uncovered);
    }

    [Fact]
    public void TheShippedTablesShareNoEntry()
    {
        var codex = CodexTable.Parse(CodexCatalogue.Text(CodexCatalogue.ResourceName)).All.Select(entry => entry.EntryId);
        var bodies = CodexTable.Parse(CodexCatalogue.Text(CodexCatalogue.BodiesResourceName)).All.Select(entry => entry.EntryId);

        Assert.NotEmpty(bodies);
        Assert.Empty(codex.Intersect(bodies));
    }

    [Theory]
    [InlineData("Stars")]
    [InlineData("Terrestrial planets")]
    [InlineData("Gas giant planets")]
    public void TheShippedTableListsEachBodySubcategoryInInnerOrionSpur(string subCategory)
    {
        var rows = CodexCatalogue.Table.InRegion(18).Where(found => found.Entry.SubCategory == subCategory && found.Entry.Body is not null);

        Assert.NotEmpty(rows);
        Assert.All(rows, found => Assert.True(found.Systems > 0));
        Assert.All(rows, found => Assert.Equal("Astronomical Bodies", found.Entry.Category));
    }

    [Theory]
    [InlineData("Stars", "stars.jpg")]
    [InlineData("Terrestrial planets", "terrestrial-planets.jpg")]
    public void ABodySubcategoryHasItsFamilyPlaceholder(string subCategory, string file) =>
        Assert.Equal(file, CodexFamilies.FileOfTableName(subCategory));
}
