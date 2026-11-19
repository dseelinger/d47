using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A star or planet kind from <c>CodexBodies.tsv</c> has a Search tile and a group row has none (#2587), and a body's
/// Codex tile opens the body's own kind, never the group row named like its sub-type.
/// </summary>
public sealed class AStarOrPlanetKindIsSearchedAndOpenedAsItsOwnKindTests
{
    private static string Kind(string key, string name, string family, string parent, string subType, string regions) =>
        string.Join('\t', key, name, family, parent, subType, "", "", "", "", "", regions) + "\n";

    private static readonly CodexTable Table = CodexTable.Parse(
        "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + Kind("star.neutron_star", "Neutron Star", "Stars", "", "Neutron Star", "18:900")
        + Kind("star.black_hole", "Black Hole", "Stars", "", "Black Hole", "18:800")
        + Kind("planet.rocky_body", "Rocky body", "Terrestrial planets", "", "Rocky body", "")
        + Kind("planet.rocky_body.not_terraformable", "Rocky body, not terraformable", "Terrestrial planets", "planet.rocky_body", "Rocky body", "")
        + Kind(
            "planet.rocky_body.not_terraformable.no_atmosphere",
            "Rocky body, not terraformable, No atmosphere",
            "Terrestrial planets",
            "planet.rocky_body.not_terraformable",
            "Rocky body",
            "18:700"));

    private static Button? Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Name == name);

    private static Window Shown(Control page)
    {
        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static CommanderGameState State() => new(new CommanderIdentity("F735466", "Test Commander"));

    [AvaloniaFact]
    public void ADiscoveriesRowForAStarHasASearchTile()
    {
        var link = new BodySearchLink(_ => { });
        var page = new CodexDiscoveriesPage(State, () => HistoryState.Done, 18, Table, bodySearch: () => link);
        var window = Shown(page);

        Assert.NotNull(Find(page, "CodexMissing.star.neutron_star"));
        Assert.NotNull(Find(page, "CodexSearch.star.neutron_star"));
        Assert.NotNull(Find(page, "CodexSearch.2"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheEntryPageForAStarHasASearchTileAndAGroupHasNone()
    {
        var link = new BodySearchLink(_ => { });
        var star = new CodexEntryPage(State, "star.neutron_star", 18, "Neutron Star", null, table: Table, bodySearch: () => link);
        var window = Shown(star);

        Assert.NotNull(Find(star, "CodexSearch"));
        window.Close();

        var group = new CodexEntryPage(State, "planet.rocky_body", 18, "Rocky body", null, table: Table, bodySearch: () => link);
        window = Shown(group);

        Assert.Null(Find(group, "CodexSearch"));
        window.Close();

        var tree = new CodexEntryPage(State, "2", 18, "Beta tree", null, table: Table, bodySearch: () => link);
        window = Shown(tree);

        Assert.NotNull(Find(tree, "CodexSearch"));
        window.Close();
    }

    [Theory]
    [InlineData("Planet", "Rocky body", "planet.rocky_body.not_terraformable.no_atmosphere")]
    [InlineData("Star", "Neutron Star", "star.neutron_star")]
    [InlineData("Star", "Black Hole", "star.black_hole")]
    public void ABodyNamedLikeAGroupRowOpensItsOwnKind(string type, string subType, string key)
    {
        var body = new BodyProfile { BodyId = 1, Name = "Fixture 1", Type = type, SubType = subType };

        Assert.Equal(key, new BodyCodex(Table).EntryOf(body)?.Key);
        Assert.Equal(key, new BodyCodex(CodexCatalogue.Table).EntryOf(body)?.Key);
    }

    [Fact]
    public void ATerraformableRockyBodyWithAThinCarbonDioxideAtmosphereOpensThatKind()
    {
        var body = new BodyProfile
        {
            BodyId = 1,
            Name = "Fixture 1",
            Type = "Planet",
            SubType = "Rocky body",
            TerraformingState = "Candidate for terraforming",
            Atmosphere = "Thin Carbon dioxide",
        };

        Assert.Equal("planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide", new BodyCodex(CodexCatalogue.Table).EntryOf(body)?.Key);
    }

    [Fact]
    public void AKGiantOpensTheKGiantKindNotTheKDwarfKind()
    {
        var body = new BodyProfile { BodyId = 1, Name = "Fixture 1", Type = "Star", SubType = "K (Yellow-Orange giant) Star", Luminosity = "III" };

        Assert.Equal("star.k_yellow_orange_giant_star.giant", new BodyCodex(CodexCatalogue.Table).EntryOf(body)?.Key);
    }

    [Fact]
    public void ABodyWithNoSubTypeOpensNothing() =>
        Assert.Null(new BodyCodex(CodexCatalogue.Table).EntryOf(new BodyProfile { BodyId = 1, Name = "Fixture 1", Type = "Planet" }));
}
