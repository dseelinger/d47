using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Discoveries row on a region's Codex page opens the Codex catalogue set to the kinds missing in that region (#2612).</summary>
public class TheRegionPagesDiscoveriesRowOpensTheMissingCatalogueTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse(
        "1\tAlpha tree\tBiological and Geological\tOrganic structures\t18:50\n"
        + "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n"
        + "3\tGamma tree\tBiological and Geological\tOrganic structures\t18:10\n"
        + "4\tDelta vent\tBiological and Geological\tGeology\t18:20\n"
        + "5\tOther region\tBiological and Geological\tGeology\t35:99\n");

    private static string Codex(string time, long id, int region, string sub, bool isNew) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"$Codex_SubCategory_X;\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_" + region
        + ";\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5"
        + (isNew ? ",\"IsNewEntry\":true" : string.Empty) + "}";

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        Codex("2026-10-02T10:00:00Z", 2, 18, "Organic structures", true),
        "{\"timestamp\":\"2026-10-03T10:00:00Z\",\"event\":\"SellExplorationData\"}",
        Codex("2026-10-04T10:00:00Z", 900, 18, "Organic structures", true),
    ];

    private static string Arrive(int region)
    {
        var at = GalacticRegions.LabelAt(region)!.Value;

        return "{\"timestamp\":\"2026-10-05T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Jump\",\"SystemAddress\":77,\"StarPos\":["
               + at.X.ToString("R", CultureInfo.InvariantCulture) + ",0," + at.Z.ToString("R", CultureInfo.InvariantCulture) + "]}";
    }

    private static CommanderGameState State(params string[] lines) => JournalScript.Commander("F735466", "John Deparagon", lines);

    private static CodexRegionPage Region(
        CommanderGameState state, int? id = 18, HistoryState history = HistoryState.Done, PanelNavigator? nav = null, CodexCatalogueChoice? catalogue = null, bool headset = false) =>
        new(() => state, () => history, () => Now, new CodexChoice(), id, headset, nav, Table, catalogue: catalogue);

    private static PanelNavigator CodexNav(int? region)
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(LedgersPage.RootKey, "Ledgers"));
        nav.Select(PanelTab.Commander);
        nav.Drill(LedgersPage.Ledger(CodexPage.PageKey, "Codex"));
        nav.Drill(CodexPage.RegionsCrumb());
        nav.Drill(CodexPage.RegionCrumb(region));

        return nav;
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static Button Row(Control root) => In<Button>(root).First(button => button.Name == "CodexDiscoveries");

    private static Button Token(Control page, string kind) =>
        In<Button>(page).First(button => button.Name == "CodexCatalogueToken." + kind);

    [AvaloniaFact]
    public void TheRegionPageDrawsADiscoveriesRowWhoseFiguresIgnoreThePeriod()
    {
        var (window, page) = PageHost.Open(() => Region(State(Lines)));

        Assert.True(Shows(page, CodexModel.DiscoveriesRowLine));
        Assert.True(Shows(page, "3 missing · 1 of 4 on record"));
        Assert.True(Shows(Row(page), "›"));
        Assert.True(Row(page).IsEnabled);
        Assert.True(Row(page).Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void PressingTheRowOpensTheCatalogueSetToMissingInThatRegion(bool headset)
    {
        var nav = CodexNav(18);
        var catalogue = new CodexCatalogueChoice();
        var state = State(Lines);
        var (window, page) = PageHost.Open(() => Region(state, nav: nav, catalogue: catalogue, headset: headset));

        Row(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["Ledgers", "Codex"], nav.Trail.Select(crumb => crumb.Word));
        Assert.Equal(CodexKindStatus.Missing, catalogue.Status);
        Assert.Equal(18, catalogue.Other);
        Assert.False(catalogue.Here);

        var (other, codex) = PageHost.Open(
            () => new CodexCataloguePage(() => state, () => HistoryState.Done, catalogue, CodexCatalogue.Tree.Root, headset, nav));

        Assert.True(Shows(Token(codex, "Status"), "MISSING"));
        Assert.True(Shows(Token(codex, "Region"), "INNER ORION SPUR"));
        Assert.True(Shows(codex, "Known in Inner Orion Spur"));

        window.Close();
        other.Close();
    }

    [AvaloniaFact]
    public void TheRegionTheCommanderIsInIsChosenAsHere()
    {
        var nav = CodexNav(18);
        var catalogue = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => Region(State(Arrive(18)), nav: nav, catalogue: catalogue));

        Row(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(CodexKindStatus.Missing, catalogue.Status);
        Assert.True(catalogue.Here);
        Assert.Null(catalogue.Other);

        window.Close();
    }

    [AvaloniaFact]
    public void AnEarlierChoiceOfAnotherRegionAndStatusIsReplaced()
    {
        var nav = CodexNav(35);
        var catalogue = new CodexCatalogueChoice { Here = true, Status = CodexKindStatus.OnRecord };
        var (window, page) = PageHost.Open(() => Region(State(Lines), id: 35, nav: nav, catalogue: catalogue));

        Row(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(CodexKindStatus.Missing, catalogue.Status);
        Assert.False(catalogue.Here);
        Assert.Equal(35, catalogue.Other);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowShowsUnderTheEmptyMessageForARegionWithNoEntriesInThePeriod()
    {
        var (window, page) = PageHost.Open(() => Region(State(Lines), id: 35));

        Assert.True(Shows(page, "1 missing · 0 of 1 on record"));
        Assert.True(Row(page).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowIsDisabledForTheRegionD47CannotNameAndOpensNothing()
    {
        var nav = CodexNav(null);
        var catalogue = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => Region(State(Lines), id: null, nav: nav, catalogue: catalogue));

        Assert.False(Row(page).IsEnabled);
        Assert.True(Shows(Row(page), CodexModel.DiscoveriesNoRegionLine));
        Assert.False(Shows(Row(page), "›"));
        Assert.DoesNotContain(In<TextBlock>(Row(page)), block => block.Text?.Contains("missing", StringComparison.Ordinal) == true);

        Row(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(catalogue.IsDefault);
        Assert.Equal(["Ledgers", "Codex", "Regions", CodexModel.NoRegionName], nav.Trail.Select(crumb => crumb.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void ARunningWalkDrawsNoDiscoveriesRow()
    {
        var (window, page) = PageHost.Open(() => Region(State(Lines), history: HistoryState.Running));

        Assert.DoesNotContain(In<Button>(page), button => button.Name == "CodexDiscoveries");

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensTheCatalogueFromTheRow()
    {
        using var look = AppLook.Put();

        var checklists = TestSurface.Checklists(TestSurface.MemoryFolder("d47-codex-discoveries-row-tests"));
        var state = State(Lines);
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices
        {
            Checklists = checklists,
            GameState = () => state,
            HistoryState = () => HistoryState.Done,
            Commanders = new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }),
        }.Furnish(view);

        Assert.True(view.Nav.Show(CodexPage.PageKey));
        Assert.True(view.Nav.Drill(CodexPage.RegionsCrumb()));
        Assert.True(view.Nav.Drill(CodexPage.RegionCrumb(18)));

        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(In<CodexRegionPage>(view));

        Row(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Empty(In<CodexRegionPage>(view));

        var codex = Assert.Single(In<CodexCataloguePage>(view));

        Assert.True(Shows(Token(codex, "Status"), "MISSING"));
        Assert.True(Shows(Token(codex, "Region"), "INNER ORION SPUR"));

        window.Close();
    }
}
