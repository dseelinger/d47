using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A kind with no picture of its own shows its nearest ancestor's, else nothing and the family placeholder (#2603).</summary>
public class AKindWithoutAPictureShowsItsGroupsTests
{
    private const string Kind = "planet.high_metal_content_world.not_terraformable.atmosphere.thin_sulphur_dioxide";
    private const string Group = "planet.high_metal_content_world.not_terraformable.atmosphere";
    private const string Terraformability = "planet.high_metal_content_world.not_terraformable";
    private const string SubType = "planet.high_metal_content_world";
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static CodexPictureRow Row(string key, int regionId) =>
        new(key, regionId, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(new CodexPictureKey(key, regionId), Sha), Sha, 1000);

    private static HashSet<CodexPictureKey> Local(params (string Key, int Region)[] keys) =>
        [.. keys.Select(key => new CodexPictureKey(key.Key, key.Region))];

    [Fact]
    public void TheKindsChainRunsUpToItsFamily()
    {
        Assert.Equal(Group, CodexKinds.Parent(CodexCatalogue.Table, Kind));
        Assert.Equal(Terraformability, CodexKinds.Parent(CodexCatalogue.Table, Group));
        Assert.Equal(SubType, CodexKinds.Parent(CodexCatalogue.Table, Terraformability));
    }

    [Fact]
    public void AKindWithItsOwnPictureShowsIt()
    {
        var found = CodexPictures.Find(Kind, 18, Local((Group, 18)), [Row(Kind, 18), Row(Group, 18)]);

        Assert.NotNull(found);
        Assert.Equal(new CodexPictureKey(Kind, 18), found.Key);
        Assert.Equal(CodexPictureSource.Stock, found.Source);
    }

    [Fact]
    public void AKindWithoutOneShowsItsNearestAncestors()
    {
        var found = CodexPictures.Find(Kind, 18, Local((SubType, 18)), [Row(Terraformability, 18)]);

        Assert.NotNull(found);
        Assert.Equal(new CodexPictureKey(Terraformability, 18), found.Key);
        Assert.Equal(Row(Terraformability, 18), found.Row);
    }

    [Fact]
    public void TheKindsOwnPictureFromAnotherRegionBeatsItsParentsInThisRegion()
    {
        var found = CodexPictures.Find(Kind, 18, Local((Group, 18)), [Row(Kind, 3)]);

        Assert.NotNull(found);
        Assert.Equal(new CodexPictureKey(Kind, 3), found.Key);
        Assert.True(found.FromOtherRegion);
    }

    [Fact]
    public void AKindWithNoPictureOnItsChainShowsTheFamilyPlaceholder()
    {
        Assert.Null(CodexPictures.Find(Kind, 18, Local(("planet.rocky_body", 18)), [Row("star.k_yellow_orange_star.dwarf", 18)]));
        Assert.NotNull(CodexKinds.FamilyFile(CodexCatalogue.Table, Kind));
    }

    [Fact]
    public void AnEntryIdKeyIsNotWalked() =>
        Assert.Null(CodexPictures.Find("2420101", 18, Local(("2420100", 18)), []));
}
