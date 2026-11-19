using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Journal;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Star and planet kinds come from <c>CodexBodies.tsv</c>, read beside <c>Codex.tsv</c> as one table, so a region's
/// Discoveries list them beside the Canonn entries, and a star or planet a Commander logged in the codex is not counted
/// among the entries the table does not list.
/// </summary>
public class AStarOrPlanetKindIsACodexEntryTests
{
    private const string Codex =
        "2320501\tBacterium Vesicula - Gold\tBiological and Geological\tOrganic structures\t18:40\n"
        + "1200402\tGreen Gas Giant\tAstronomical Bodies\tGas giant planets\t34:5\n";

    private static readonly CodexTable Table = CodexTable.Parse(Codex + KindRows.RockyBodyAndNeutronStar());

    private static CodexLedger Ledger(params (long Id, string SubCategory)[] entries) =>
        JournalScript.Store(
        [
            """{"timestamp":"2026-07-03T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            """{"timestamp":"2026-07-03T09:01:00Z","event":"LoadGame","Credits":1}""",
            .. entries.Select(entry =>
                "{\"timestamp\":\"2026-10-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + entry.Id
                + ",\"Name_Localised\":\"Entry\",\"SubCategory\":\"$Codex_SubCategory_" + entry.SubCategory
                + ";\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"IsNewEntry\":true}"),
        ]).Active!.CodexLedger;

    [Fact]
    public void AKindRowCarriesItsParentAndTheSpanshFieldsOfTheKind()
    {
        var entry = Table.Entry("planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide");

        Assert.NotNull(entry);
        Assert.Null(entry.EntryId);
        Assert.Equal("Astronomical Bodies", entry.Category);
        Assert.Equal("Terrestrial planets", entry.SubCategory);
        Assert.Equal(
            new CodexKind("planet.rocky_body.terraformable.atmosphere", "Rocky body", null, "yes", "Thin Carbon dioxide", null, null),
            entry.Kind);
        Assert.Equal(300, entry.Regions[18]);
    }

    [Fact]
    public void ACanonnRowHasNoKind() => Assert.Null(Table.Entry(2320501)!.Kind);

    [Fact]
    public void AGroupRowIsNotListedInAnyRegion()
    {
        Assert.Empty(Table.Entry("planet.rocky_body")!.Regions);
        Assert.DoesNotContain(Table.InRegion(18), found => found.Entry.Key == "planet.rocky_body");
        Assert.Contains(Table.InRegion(18), found => found.Entry.Key == "star.neutron_star");
    }

    [Fact]
    public void AStarLoggedInTheCodexIsNotCountedAsUnlisted()
    {
        var discoveries = CodexDiscoveries.For(Ledger((1102300, "Stars"), (1300602, "Terrestrials")), Table, 18);

        Assert.Empty(discoveries.Uncovered);
        Assert.Empty(discoveries.Logged);
    }

    [Fact]
    public void ACanonnGasGiantLoggedWhereTheTableDoesNotListItIsStillUnlisted()
    {
        var discoveries = CodexDiscoveries.For(Ledger((1200402, "Gas_Giants")), Table, 18);

        Assert.Equal(1200402, Assert.Single(discoveries.Uncovered).EntryId);
    }

    [Fact]
    public void TheShippedKindsAreKindKeysAndEveryParentHasARow()
    {
        var kinds = CodexTable.Parse(CodexCatalogue.Text(CodexCatalogue.BodiesResourceName)).All.ToList();
        var parents = kinds.Select(entry => entry.Kind!.ParentKey).OfType<string>().ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(kinds);
        Assert.All(kinds, entry => Assert.True(CodexKeys.IsKind(entry.Key), entry.Key));
        Assert.All(kinds, entry => Assert.Null(entry.EntryId));
        Assert.All(parents, parent => Assert.NotNull(CodexCatalogue.Table.Entry(parent)?.Kind));
        Assert.All(kinds, entry => Assert.Equal(!parents.Contains(entry.Key), entry.Regions.Count > 0));
    }

    [Fact]
    public void ABandedStarsSubTypeRowHasItsUsualBand()
    {
        var group = CodexCatalogue.Table.Entry("star.m_red_dwarf_star");

        Assert.Equal("dwarf", group?.Kind?.UsualBand);
        Assert.Null(CodexCatalogue.Table.Entry("star.neutron_star")?.Kind?.UsualBand);
    }

    [Fact]
    public void ARegionsDiscoveriesListStarGasGiantAndPlanetKindsBesideTheCanonnEntries()
    {
        var canonn = CodexTable.Parse(CodexCatalogue.Text(CodexCatalogue.ResourceName));
        var known = CodexDiscoveries.For(Ledger(), CodexCatalogue.Table, 18).Known;

        foreach (var family in new[] { "Stars", "Gas giant planets", "Terrestrial planets" })
        {
            Assert.Contains(known, item => item.Entry.Kind is not null && item.Entry.SubCategory == family && item.Systems > 0);
        }

        Assert.Equal(
            canonn.InRegion(18).Select(found => (found.Entry.Key, found.Systems)).Order(),
            known.Where(item => item.Entry.Kind is null).Select(item => (item.Entry.Key, item.Systems)).Order());
    }

    [Theory]
    [InlineData("Stars", "stars.jpg")]
    [InlineData("Terrestrial planets", "terrestrial-planets.jpg")]
    [InlineData("Gas giant planets", "gas-giant-planets.jpg")]
    public void AKindFamilyHasItsPlaceholder(string family, string file) =>
        Assert.Equal(file, CodexFamilies.FileOfTableName(family));
}
