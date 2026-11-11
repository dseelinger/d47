using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Interface;

public sealed class ASpeciesPictureIsChosenBeforeItsVariantIsKnownTests
{
    private const string Species = "Testa Alpha";

    private static readonly CodexTable Table = CodexTable.Parse(
        "100\tTesta Alpha - Red\tBiological and Geological\tOrganic structures\t3:2\n" +
        "101\tTesta Alpha - Green\tBiological and Geological\tOrganic structures\t3:9\n" +
        "102\tTesta Alpha - Blue\tBiological and Geological\tOrganic structures\t3:5\n" +
        "200\tOther Beta - Red\tBiological and Geological\tOrganic structures\t3:50\n");

    private static readonly IReadOnlySet<CodexPictureKey> NoLocal = new HashSet<CodexPictureKey>();

    private static CodexPictureRow Row(long entryId) =>
        new(entryId, 3, "codex-pictures-1", $"{entryId}-3-abc.jpg", new string('a', 64), 10);

    [Fact]
    public void AKnownEntryIdWithAPictureWins()
    {
        var chosen = SpeciesPicture.Choose(Species, 100, 3, NoLocal, [Row(100), Row(101)], Table);

        Assert.NotNull(chosen);
        Assert.Equal(100, chosen.Picture.Key.EntryId);
        Assert.True(chosen.IsBodysOwnVariant);
    }

    [Fact]
    public void WithNoKnownPictureTheVariantWithTheMostSystemsInTheRegionAndAPictureIsChosen()
    {
        var chosen = SpeciesPicture.Choose(Species, 100, 3, NoLocal, [Row(101), Row(102)], Table);

        Assert.NotNull(chosen);
        Assert.Equal(101, chosen.Picture.Key.EntryId);
        Assert.False(chosen.IsBodysOwnVariant);
    }

    [Fact]
    public void AVariantWithMoreSystemsAndNoPictureIsSkipped()
    {
        var chosen = SpeciesPicture.Choose(Species, null, 3, NoLocal, [Row(102)], Table);

        Assert.NotNull(chosen);
        Assert.Equal(102, chosen.Picture.Key.EntryId);
    }

    [Fact]
    public void NoPictureForAnyVariantGivesNull() =>
        Assert.Null(SpeciesPicture.Choose(Species, null, 3, NoLocal, [Row(200)], Table));

    [Fact]
    public void ALocalPictureCountsAsAPicture()
    {
        var local = new HashSet<CodexPictureKey> { new(100, 3) };

        var chosen = SpeciesPicture.Choose(Species, null, 3, local, [], Table);

        Assert.NotNull(chosen);
        Assert.Equal(CodexPictureSource.Local, chosen.Picture.Source);
        Assert.Equal(100, chosen.Picture.Key.EntryId);
    }

    [Fact]
    public void AVariantNameGivesItsEntryIdFromTheShippedTable() =>
        Assert.Equal(2340104L, CodexVariants.Named("Clypeus Lacrimam - Mauve"));

    [Fact]
    public void ASpeciesListsOnlyItsOwnVariants() =>
        Assert.Equal([100L, 101L, 102L], CodexVariants.Of(Species, Table).Select(entry => entry.EntryId));
}
