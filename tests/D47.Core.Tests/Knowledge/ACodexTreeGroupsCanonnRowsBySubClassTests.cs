using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <see cref="CodexTree"/> puts Canonn's rows under their family and group, and under their species where the group's
/// names are "Species - Colour", beside the star and planet rows.
/// </summary>
public class ACodexTreeGroupsCanonnRowsBySubClassTests
{
    private const string Organic = "Biological and Geological\tOrganic structures";

    private static readonly CodexTree Tree = CodexTree.Of(CodexTable.Parse(
        "2320501\tBacterium Vesicula - Gold\t" + Organic + "\tBacterial\t18:40\n"
        + "2320502\tBacterium Vesicula - Teal\t" + Organic + "\tBacterial\t18:4\n"
        + "2320601\tBacterium Aurasus - Grey\t" + Organic + "\tBacterial\t\n"
        + "2100201\tRoseum Brain Tree\t" + Organic + "\tBrain Tree\t\n"
        + "2100202\tGypseeum Brain Tree\t" + Organic + "\tBrain Tree\t\n"
        + "3100101\tGuardian Codex\tXenological\tGuardian objects\tGuardian\t\n"
        + "3100102\tGuardian Data Terminal\tXenological\tGuardian objects\tGuardian\t\n"
        + "1200102\tGreen Water Giant\tAstronomical Bodies\tGas giant planets\tPlanets\t3:1\n"
        + KindRows.Row("gas.water_giant", "Water giant", "Gas giant planets", subType: "Water giant")
        + KindRows.Row("gas.water_giant.no_rings", "Water giant, no rings", "Gas giant planets", "gas.water_giant", "Water giant", rings: "None", regions: "3:9")
        + KindRows.RockyBodyAndNeutronStar()));

    [Fact]
    public void AGroupOfSpeciesNamesIsNamedByTheirFirstWordAndHasSpecies()
    {
        var group = Tree.Node("organic_structures.bacterium")!;

        Assert.Equal("Bacterium", group.Name);
        Assert.Equal(
            ["organic_structures.bacterium.bacterium_aurasus", "organic_structures.bacterium.bacterium_vesicula"],
            group.Children.Select(child => child.Key));

        var species = group.Children[1];

        Assert.Equal("Bacterium Vesicula", species.Name);
        Assert.Equal("Vesicula", species.ShortName);
        Assert.Null(species.Entry);
        Assert.Equal(["2320501", "2320502"], species.Children.Select(child => child.Key));
        Assert.Equal("Gold", species.Children[0].ShortName);
        Assert.Equal("Bacterium Vesicula - Gold", species.Children[0].Entry!.Name);
    }

    [Fact]
    public void AGroupOfOtherNamesKeepsCanonnsNameAndHoldsItsEntries()
    {
        var group = Tree.Node("organic_structures.brain_tree")!;

        Assert.Equal("Brain Tree", group.Name);
        Assert.Equal(["Gypseeum Brain Tree", "Roseum Brain Tree"], group.Children.Select(child => child.Name));
        Assert.Equal(["Gypseeum", "Roseum"], group.Children.Select(child => child.ShortName));
        Assert.All(group.Children, child => Assert.NotNull(child.Entry));
    }

    [Fact]
    public void AFamilyWithOneGroupListsItsEntries()
    {
        var family = Tree.Node("guardian_objects")!;

        Assert.Equal(["3100101", "3100102"], family.Children.Select(child => child.Key));
    }

    [Fact]
    public void ACanonnGasGiantRowIsUnderGreenGasGiants()
    {
        var entry = Tree.Node("1200102")!;

        Assert.Equal("gas_giant_planets.green_gas_giants", entry.Parent!.Key);
        Assert.Equal("Green gas giants", entry.Parent.Name);
        Assert.Equal("gas_giant_planets", entry.Parent.Parent!.Key);
        Assert.Equal(
            ["gas_giant_planets.green_gas_giants", "gas.water_giant"],
            Tree.Node("gas_giant_planets")!.Children.Select(child => child.Key));
        Assert.Equal("no rings", Tree.Node("gas.water_giant.no_rings")!.ShortName);
    }

    [Fact]
    public void TheFamiliesFollowTheirOrder()
    {
        Assert.Equal(
            ["stars", "gas_giant_planets", "terrestrial_planets", "organic_structures", "guardian_objects"],
            Tree.Root.Children.Select(child => child.Key));
    }

    [Theory]
    [InlineData("2320501", "organic_structures")]
    [InlineData("2100201", "organic_structures")]
    [InlineData("3100102", "guardian_objects")]
    [InlineData("1200102", "gas_giant_planets")]
    [InlineData("planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide", "terrestrial_planets")]
    [InlineData("star.neutron_star", "stars")]
    public void WalkingANodesParentsEndsAtItsFamily(string key, string family)
    {
        var node = Tree.Node(key)!;

        while (node.Parent!.Parent is not null)
        {
            node = node.Parent;
        }

        Assert.Equal(family, node.Key);
        Assert.Equal(CodexTree.RootKey, node.Parent.Key);
    }

    [Theory]
    [InlineData("Bacterium Aurasus - Grey", "Bacterium Aurasus", "Grey")]
    [InlineData("Roseum Brain Tree", "Brain Tree", "Roseum")]
    [InlineData("Icy body, not terraformable, Argon", "Icy body, not terraformable, with atmosphere", "Argon")]
    [InlineData("Green Water Giant", "Green gas giants", "Green Water Giant")]
    [InlineData("Amphora Plant", "Amphora Plant", "Amphora Plant")]
    public void AShortNameLeavesOutWhatItsParentSays(string name, string parent, string shortName) =>
        Assert.Equal(shortName, CodexTree.ShortNameOf(name, parent));
}
