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
/// A star or planet type entry from <c>CodexBodies.tsv</c> has no Search tile, since the body search finds landmarks
/// only, and a body's Codex tile does not open it, even where the body's sub-type is the entry's name.
/// </summary>
public sealed class AStarOrPlanetTypeEntryOpensNoSearchTests
{
    private static readonly CodexTable Table = CodexTable.Parse(
        "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + "1102300\tNeutron Star\tAstronomical Bodies\tStars\tNeutron Star\tVII\t\t\t18:900\n"
        + "1102400\tBlack Hole\tAstronomical Bodies\tStars\tBlack Hole\tVII\t\t\t18:800\n"
        + "1300601\tRocky Body\tAstronomical Bodies\tTerrestrial planets\tRocky body\t\tno\tno\t18:700\n");

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
    public void ADiscoveriesRowForAStarHasNoSearchTile()
    {
        var link = new BodySearchLink(_ => { });
        var page = new CodexDiscoveriesPage(State, () => HistoryState.Done, 18, Table, bodySearch: () => link);
        var window = Shown(page);

        Assert.NotNull(Find(page, "CodexMissing.1102300"));
        Assert.Null(Find(page, "CodexSearch.1102300"));
        Assert.NotNull(Find(page, "CodexSearch.2"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheEntryPageForAStarHasNoSearchTile()
    {
        var link = new BodySearchLink(_ => { });
        var star = new CodexEntryPage(State, 1102300, 18, "Neutron Star", null, table: Table, bodySearch: () => link);
        var window = Shown(star);

        Assert.Null(Find(star, "CodexSearch"));
        window.Close();

        var tree = new CodexEntryPage(State, 2, 18, "Beta tree", null, table: Table, bodySearch: () => link);
        window = Shown(tree);

        Assert.NotNull(Find(tree, "CodexSearch"));
        window.Close();
    }

    [Theory]
    [InlineData("Planet", "Rocky body")]
    [InlineData("Star", "Neutron Star")]
    [InlineData("Star", "Black Hole")]
    public void ABodyNamedLikeATypeEntryOpensNothing(string type, string subType)
    {
        var body = new BodyProfile { BodyId = 1, Name = "Fixture 1", Type = type, SubType = subType };

        Assert.Null(new BodyCodex(Table).EntryOf(body));
        Assert.Null(new BodyCodex(CodexCatalogue.Table).EntryOf(body));
    }
}
