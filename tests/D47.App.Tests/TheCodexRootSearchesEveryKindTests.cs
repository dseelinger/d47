using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Codex root's title-line search lists every kind whose name contains the text, from every family (#2611).</summary>
public class TheCodexRootSearchesEveryKindTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-19T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Small = CodexTable.Parse(
        "101\tAlpha One\tBiological\tOrganic structures\tAlpha\t18:5\n"
        + "102\tAlpha Two\tBiological\tOrganic structures\tAlpha\t3:4\n"
        + "301\tVent One\tGeology\tGeology and anomalies\tVents\t18:1\n"
        + "401\tGuardian One\tGuardian\tGuardian objects\t\t3:2\n");

    private static readonly CodexTree SmallTree = CodexTree.Of(Small);

    private static CommanderGameState State(params string[] lines) => JournalScript.Commander("F735466", "John Deparagon", lines);

    private static string Codex(long id) =>
        "{\"timestamp\":\"2026-11-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name_Localised\":\"Entry " + id
        + "\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Jump\",\"IsNewEntry\":true}";

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static string[] KindKeys(Control page) =>
        [.. In<Button>(page).Where(button => button.Name?.StartsWith("CodexKind.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexKind.".Length..])];

    private static string[] GroupKeys(Control page) =>
        [.. In<Button>(page).Where(button => button.Name?.StartsWith("CodexGroup.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexGroup.".Length..])];

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static PanelNavigator Nav()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);

        return nav;
    }

    private static CodexCataloguePage Root(
        CommanderGameState state, CodexTree tree, PanelNavigator nav, CodexCatalogueChoice? choice = null, bool headset = false) =>
        new(() => state, () => HistoryState.Done, choice ?? new CodexCatalogueChoice(), tree.Root, headset, nav, tree: tree, period: new CodexChoice(), now: () => Now);

    private static string Key(string name) => Small.All.Single(entry => entry.Name == name).Key;

    [AvaloniaFact]
    public void TypingPartOfANameListsTheMatchingKindsFromEveryFamilyAndMissingKeepsTheMissingOnes()
    {
        var choice = new CodexCatalogueChoice();
        var state = State(Codex(101));
        var (window, page) = PageHost.Open(() => Root(state, SmallTree, Nav(), choice));

        Assert.Equal(3, GroupKeys(page).Length);

        page.Filter("one");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(GroupKeys(page));
        Assert.Equal(
            new[] { Key("Alpha One"), Key("Vent One"), Key("Guardian One") }.Order(), KindKeys(page).Order());
        Assert.True(Shows(page, "Guardian One"));
        Assert.True(Shows(page, "Vent One"));

        choice.Status = CodexKindStatus.Missing;
        page.Tick();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { Key("Vent One"), Key("Guardian One") }.Order(), KindKeys(page).Order());
        window.Close();
    }

    [AvaloniaFact]
    public void TheRealCodexListsFullNamesAcrossFamilies()
    {
        var state = State();
        var (window, page) = PageHost.Open(() => Root(state, CodexCatalogue.Tree, Nav()));

        page.Filter("thin carbon dioxide");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Rocky body, not terraformable, Thin Carbon dioxide"));

        page.Filter("Bacterium Aurasus - Teal");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Bacterium Aurasus - Teal"));
        Assert.Single(KindKeys(page));
        window.Close();
    }

    [AvaloniaFact]
    public void ARowOpensTheKindsEntryPageAndBackReturnsToTheResults()
    {
        var nav = Nav();
        var state = State();
        var (window, page) = PageHost.Open(() => Root(state, SmallTree, nav));

        page.Filter("vent");
        Dispatcher.UIThread.RunJobs();
        var key = Key("Vent One");
        var row = In<Button>(page).Single(button => button.Name == "CodexKind." + key);
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(CodexPage.IsEntry(nav.Trail[^1].Key));
        Assert.Equal("Vent One", nav.Trail[^1].Word);

        Assert.True(nav.Back());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CodexPage.PageKey, nav.Trail[^1].Key);
        Assert.Equal([key], KindKeys(page));
        window.Close();
    }

    [AvaloniaFact]
    public void NoMatchShowsAGreyLineAndClearingBringsTheFamilyRowsBack()
    {
        var state = State();
        var (window, page) = PageHost.Open(() => Root(state, SmallTree, Nav()));

        page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "No kind in the Codex matches that search."));
        Assert.Empty(KindKeys(page));
        Assert.Empty(GroupKeys(page));

        page.Filter("");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SmallTree.Root.Children.Select(family => family.Key), GroupKeys(page));
        Assert.False(Shows(page, "No kind in the Codex matches that search."));
        window.Close();
    }

    [AvaloniaFact]
    public void OnlyTheDesktopRootHasTheField()
    {
        var state = State();
        var (window, desktop) = PageHost.Open(() => Root(state, SmallTree, Nav()));
        var (other, headset) = PageHost.Open(() => Root(state, SmallTree, Nav(), headset: true));
        var family = new CodexCataloguePage(() => state, () => HistoryState.Done, new CodexCatalogueChoice(), SmallTree.Root.Children[0], false, Nav(), tree: SmallTree);

        Assert.True(desktop.Filters);
        Assert.Equal("Search every kind", desktop.FilterPlaceholder);
        Assert.False(headset.Filters);
        Assert.False(family.Filters);

        headset.Filter("one");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SmallTree.Root.Children.Select(node => node.Key), GroupKeys(headset));
        Assert.Empty(KindKeys(headset));
        window.Close();
        other.Close();
    }

    [Fact]
    public void TheResultsLineCountsTheKindsShown()
    {
        var state = State(Codex(101));
        var found = CodexCatalogueModel.Search(state.CodexLedger, SmallTree.Root, null, CodexKindStatus.Missing, "one");

        Assert.Equal(2, found.Shown);
        Assert.Equal(3, found.Counted);
        Assert.Equal(2, found.Kinds.Count);
    }

    [Fact]
    public void AGenusOpensItsOwnPageUnderItsFamily()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        nav.Register(PanelTab.Commander, LedgersPage.Root);
        var tree = CodexCatalogue.Tree;
        var genus = CodexCatalogueModel.GenusNode(tree, "Bacterium");

        Assert.NotNull(genus);
        Assert.True(PageTrail.Open(nav, LedgersPage.RootKey, CodexPage.GenusTrail(tree, "Bacterium")));

        Assert.Equal(genus.Key, CodexPage.CatalogueNodeOf(nav.Trail[^1].Key));
        Assert.Equal("Organic structures", CodexCatalogueModel.FamilyOf(genus)!.Name);
        Assert.Contains(nav.Trail, crumb => crumb.Key == CodexPage.NodeCrumb(CodexCatalogueModel.FamilyOf(genus)!).Key);
    }

    [Fact]
    public void AGenusTheCatalogueDoesNotNameOpensTheCodexRoot()
    {
        var trail = CodexPage.GenusTrail(CodexCatalogue.Tree, "Nothing like it");

        Assert.Equal([CodexPage.PageKey], trail.Select(crumb => crumb.Key));
    }
}
