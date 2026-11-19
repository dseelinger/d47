using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A kind's row names its parent, so <see cref="CodexKinds"/> walks from a kind up to the top of its family and its placeholder.</summary>
public class ACodexKindWalksUpToItsFamilyTests
{
    private static readonly CodexTable Table = CodexTable.Parse(
        "2320501\tBacterium Vesicula - Gold\tBiological and Geological\tOrganic structures\t18:40\n"
        + KindRows.RockyBodyAndNeutronStar()
        + KindRows.Row("star.o_blue_white_star", "O (Blue-White) Star", "Stars", subType: "O (Blue-White) Star", usualBand: "dwarf")
        + KindRows.Row(
            "star.o_blue_white_star.dwarf",
            "O (Blue-White) Star, dwarf",
            "Stars",
            "star.o_blue_white_star",
            "O (Blue-White) Star",
            band: "dwarf",
            regions: "18:5"));

    [Fact]
    public void APlanetKindWalksUpThroughEachLevelToItsSubType()
    {
        const string Kind = "planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide";

        Assert.Equal(
            [Kind, "planet.rocky_body.terraformable.atmosphere", "planet.rocky_body.terraformable", "planet.rocky_body"],
            CodexKinds.Lineage(Table, Kind).Select(entry => entry.Key));
        Assert.Equal("planet.rocky_body.terraformable.atmosphere", CodexKinds.Parent(Table, Kind));
        Assert.Null(CodexKinds.Parent(Table, "planet.rocky_body"));
        Assert.Equal("terrestrial-planets.jpg", CodexKinds.FamilyFile(Table, Kind));
    }

    [Fact]
    public void AStarWithNoBandIsTheTopOfItsFamily()
    {
        Assert.Equal(["star.neutron_star"], CodexKinds.Lineage(Table, "star.neutron_star").Select(entry => entry.Key));
        Assert.Equal("stars.jpg", CodexKinds.FamilyFile(Table, "star.neutron_star"));
    }

    [Fact]
    public void ACanonnEntryIsNotAKind()
    {
        Assert.Empty(CodexKinds.Lineage(Table, "2320501"));
        Assert.Null(CodexKinds.FamilyFile(Table, "2320501"));
        Assert.Null(CodexKinds.Parent(Table, "planet.no_such_body"));
    }

    [Theory]
    [InlineData("star.m_red_dwarf_star.dwarf", "stars.jpg")]
    [InlineData("gas.class_i_gas_giant.no_rings", "gas-giant-planets.jpg")]
    [InlineData("planet.rocky_body.not_terraformable.no_atmosphere", "terrestrial-planets.jpg")]
    public void AShippedKindWalksUpToItsFamily(string key, string file)
    {
        var lineage = CodexKinds.Lineage(CodexCatalogue.Table, key);

        Assert.NotEmpty(lineage);
        Assert.Equal(key, lineage[0].Key);
        Assert.Null(lineage[^1].Kind!.ParentKey);
        Assert.Equal(file, CodexKinds.FamilyFile(CodexCatalogue.Table, key));
    }

    [Fact]
    public void AStarWithNoLuminosityTakesItsSubTypesUsualBand()
    {
        var star = new BodyProfile { BodyId = 1, Name = "Fixture A", Type = "Star", SubType = "O (Blue-White) Star" };

        Assert.Equal("star.o_blue_white_star.dwarf", CodexKinds.OfSpansh(star, Table));
        Assert.Null(CodexKinds.OfSpansh(star with { SubType = "B (Blue-White) Star" }, Table));
        Assert.Equal("star.o_blue_white_star.giant", CodexKinds.OfSpansh(star with { Luminosity = "IIIb" }, Table));
    }

    [Fact]
    public void ABodyWithNoSubTypeHasNoKind() =>
        Assert.Null(CodexKinds.OfSpansh(new BodyProfile { BodyId = 1, Name = "Fixture A", Type = "Star" }, Table));
}
