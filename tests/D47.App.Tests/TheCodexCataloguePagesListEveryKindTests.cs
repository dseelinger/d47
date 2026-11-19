using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › Codex and the pages under it: one page for any node of the codex tree, under a shared filter (#2608).</summary>
public class TheCodexCataloguePagesListEveryKindTests
{
    private const string RockyBody = "planet.rocky_body";
    private const string NoAtmosphere = "planet.rocky_body.not_terraformable.no_atmosphere";
    private const string WithAtmosphere = "planet.rocky_body.not_terraformable.atmosphere";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-19T20:00:00Z", CultureInfo.InvariantCulture);

    private static CodexTree Tree => CodexCatalogue.Tree;

    private static string Arrive(int region)
    {
        var at = GalacticRegions.LabelAt(region)!.Value;

        return "{\"timestamp\":\"2026-11-01T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Jump\",\"SystemAddress\":77,\"StarPos\":["
               + at.X.ToString("R", CultureInfo.InvariantCulture) + ",0," + at.Z.ToString("R", CultureInfo.InvariantCulture) + "]}";
    }

    private static string Codex(long id, int region, string sub = "Organic structures") =>
        "{\"timestamp\":\"2026-11-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name_Localised\":\"Entry " + id
        + "\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_" + region + ";\",\"System\":\"Jump\",\"IsNewEntry\":true}";

    private const string RockyScan =
        "{\"timestamp\":\"2026-11-01T09:05:00Z\",\"event\":\"Scan\",\"ScanType\":\"AutoScan\",\"BodyName\":\"Jump 2\",\"StarSystem\":\"Jump\","
        + "\"SystemAddress\":77,\"PlanetClass\":\"Rocky body\",\"TerraformState\":\"\",\"AtmosphereType\":\"None\",\"Atmosphere\":\"\"}";

    private static CommanderGameState State(params string[] lines) => JournalScript.Commander("F735466", "John Deparagon", lines);

    /// <summary>Three families: Organic structures with two kinds, Geology with one, Guardian objects with one.</summary>
    private static readonly CodexTable Small = CodexTable.Parse(
        "101\tAlpha One\tBiological\tOrganic structures\tAlpha\t18:5\n"
        + "102\tAlpha Two\tBiological\tOrganic structures\tAlpha\t3:4\n"
        + "301\tVent One\tGeology\tGeology and anomalies\tVents\t18:1\n"
        + "401\tGuardian One\tGuardian\tGuardian objects\t\t3:2\n");

    private static readonly CodexTree SmallTree = CodexTree.Of(Small);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static void Dispatch() => Dispatcher.UIThread.RunJobs();

    private static void Press(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatch();
    }

    private static Button Named(Control root, string name) => In<Button>(root).First(button => button.Name == name);

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static string[] Texts(Control root) => [.. In<TextBlock>(root).Select(block => block.Text ?? string.Empty)];

    private static string[] KindKeys(Control page) =>
        [.. In<Button>(page).Where(button => button.Name?.StartsWith("CodexKind.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexKind.".Length..])];

    private static string[] GroupKeys(Control page) =>
        [.. In<Button>(page).Where(button => button.Name?.StartsWith("CodexGroup.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexGroup.".Length..])];

    private static void Pick(Control page, string token, int option)
    {
        Press(Named(page, "CodexCatalogueToken." + token));
        In<Segment>(page).Single(segment => AutomationProperties.GetName(segment) == token + " options")
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatch();
    }

    private static CodexCataloguePage Page(
        CommanderGameState state, CodexNode node, CodexCatalogueChoice? choice = null, PanelNavigator? nav = null, bool headset = false, CodexTree? tree = null) =>
        new(() => state, () => HistoryState.Done, choice ?? new CodexCatalogueChoice(), node, headset, nav, tree: tree);

    private static CodexCataloguePage Root(CommanderGameState state, PanelNavigator nav, CodexChoice choice, bool headset = false) =>
        new(() => state, () => HistoryState.Done, new CodexCatalogueChoice(), Tree.Root, headset, nav, period: choice, now: () => Now);

    [AvaloniaFact]
    public void CodexOpensOnTheFamilyRowsWithTheFilterAboveAndTheRegionsRowBeneath()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        var state = State(Arrive(18), RockyScan);

        var (window, page) = PageHost.Open(() => Root(state, nav, new CodexChoice { Period = (int)CodexPeriod.Everything }));

        Assert.Equal(Tree.Root.Children.Select(family => family.Key), GroupKeys(page));
        Assert.DoesNotContain(In<Button>(page), button => button.Name == "CodexCatalogue");
        Assert.DoesNotContain(Texts(page), text => text.Equals("Catalogue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(In<Button>(page), button => button.Name == "CodexPossibleFirsts");

        var all = In<Control>(page).ToList();
        var filter = all.IndexOf(In<CodexCatalogueFilters>(page).Single());
        var firstFamily = all.IndexOf(Named(page, "CodexGroup." + Tree.Root.Children[0].Key));
        var regions = all.IndexOf(Named(page, "CodexRegions"));
        Assert.True(filter < firstFamily && firstFamily < regions);

        var row = Named(page, "CodexRegions");
        Assert.True(Shows(row, "Regions"));
        Assert.True(Shows(row, CodexModel.RegionsRowLine));
        Assert.Contains("0 regions · 0 entries on record", Texts(row));

        Press(row);

        Assert.Equal(CodexPage.RegionsKey, nav.Trail[^1].Key);
        Assert.Equal("Regions", nav.Trail[^1].Word);
        window.Close();
    }

    [AvaloniaFact]
    public void TheRegionsRowFigureFollowsThePeriodChosenOnTheRegionsPage()
    {
        var nav = new PanelNavigator();
        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };
        var state = State(Arrive(18), Codex(101, 18));

        var (window, page) = PageHost.Open(() => Root(state, nav, choice));

        Assert.Contains("1 region · 1 entry on record", Texts(Named(page, "CodexRegions")));

        choice.Period = (int)CodexPeriod.Session;
        Assert.True(page.Tick());
        Dispatch();

        Assert.Contains(Texts(Named(page, "CodexRegions")), text => text.EndsWith("this session", StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetRootSummaryDropsTheCounts()
    {
        var nav = new PanelNavigator();
        var state = State(Arrive(18));

        var (window, page) = PageHost.Open(() => Root(state, nav, new CodexChoice(), headset: true));

        Assert.Equal("Every kind of object in the Codex.", ((IPageSummary)page).Summary);
        window.Close();

        var (desk, deskPage) = PageHost.Open(() => Root(state, nav, new CodexChoice()));

        Assert.StartsWith("Every kind of object in the Codex. ", ((IPageSummary)deskPage).Summary);
        Assert.EndsWith(" are on your record.", ((IPageSummary)deskPage).Summary);
        desk.Close();
    }

    private static int Leaves(CodexNode node) =>
        node.Children.Count == 0 ? (node.Entry is null ? 0 : 1) : node.Children.Sum(Leaves);

    [AvaloniaFact]
    public void TheCatalogueHasARowPerFamilyWithItsThreeCounts()
    {
        var state = State(Arrive(18), RockyScan);
        var (window, page) = PageHost.Open(() => Page(state, Tree.Root));

        Assert.Equal(Tree.Root.Children.Select(family => family.Key), GroupKeys(page));
        Assert.DoesNotContain(In<Segment>(page), segment => segment.Name == "CodexCatalogueSegment");

        var terrestrial = Tree.Node("terrestrial_planets")!;
        var counted = Leaves(terrestrial);
        var cells = Texts(Named(page, "CodexGroup.terrestrial_planets"));

        Assert.Contains("Terrestrial planets", cells);
        Assert.Contains("By type, then terraforming and atmosphere", cells);
        Assert.Equal(
            [CodexModel.Number(counted), "1", CodexModel.Number(counted - 1)],
            cells.Where(text => text.Length > 0 && text.All(c => char.IsDigit(c) || c == ',')).ToArray());
        Assert.StartsWith("Every kind of object in the Codex. 1 of ", page.Summary, StringComparison.Ordinal);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-catalogue-root.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void APlanetTypeShowsItsTerraformingSegmentsOverTheNoAtmosphereKindThenTheAtmospheres()
    {
        var state = State();
        var node = Tree.Node(RockyBody)!;
        var choice = new CodexCatalogueChoice();
        choice.ChooseSegment(RockyBody, 1);
        var (window, page) = PageHost.Open(() => Page(state, node, choice));

        var segment = In<Segment>(page).Single(item => item.Name == "CodexCatalogueSegment");
        Assert.Equal(["Terraformable", "Not terraformable"], segment.ItemsSource);
        Assert.All(segment.Statuses, status => Assert.Matches("^[0-9,]+ kinds? · 0 on record$", status!.Text));
        Assert.Empty(GroupKeys(page));

        var view = CodexCatalogueModel.View(CodexLedger.None, Tree, node, null, CodexKindStatus.All, 1);
        Assert.Equal(NoAtmosphere, view.Kinds[0].Entry.Key);
        Assert.All(view.Kinds.Skip(1), kind => Assert.StartsWith(WithAtmosphere + ".", kind.Entry.Key, StringComparison.Ordinal));
        Assert.Equal(Tree.Node(WithAtmosphere)!.Children.Count + 1, view.Kinds.Count);
        Assert.DoesNotContain(view.Segments, item => item.Node.Key == WithAtmosphere);

        var keys = KindKeys(page);
        Assert.Equal(NoAtmosphere, keys[0]);
        Assert.Equal(view.Kinds.Count, keys.Length);
        Assert.Contains(In<TextBlock>(page), block => block.Text == "No atmosphere");

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-catalogue-rocky-body.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void MissingListsOnlyMissingKindsAndLeavesOutAGroupWithNone()
    {
        var state = State(Arrive(18), Codex(301, 18, "Geology and anomalies"));
        var choice = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => Page(state, SmallTree.Root, choice, tree: SmallTree));

        Assert.Equal(["organic_structures", "geology_and_anomalies", "guardian_objects"], GroupKeys(page));

        Pick(page, "Status", 1);

        Assert.Equal(CodexKindStatus.Missing, choice.Status);
        Assert.Equal(["organic_structures", "guardian_objects"], GroupKeys(page));
        Assert.True(Shows(Named(page, "CodexCatalogueToken.Status"), "MISSING"));

        window.Close();

        var (kindsWindow, kinds) = PageHost.Open(() => Page(state, SmallTree.Node("organic_structures")!, choice, tree: SmallTree));
        Assert.Equal(["101", "102"], KindKeys(kinds).Order(StringComparer.Ordinal));
        kindsWindow.Close();
    }

    [AvaloniaFact]
    public void ChoosingTheCommandersRegionCountsThatRegionOnly()
    {
        var state = State(Arrive(18));
        var choice = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => Page(state, SmallTree.Root, choice, tree: SmallTree));

        Assert.Contains("2", Texts(Named(page, "CodexGroup.organic_structures")));
        Assert.True(Shows(Named(page, "CodexCatalogueToken.Region"), "EVERY REGION"));

        Pick(page, "Region", 1);

        Assert.True(choice.Here);
        Assert.True(Shows(Named(page, "CodexCatalogueToken.Region"), "INNER ORION SPUR"));
        Assert.DoesNotContain("2", Texts(Named(page, "CodexGroup.organic_structures")));
        Assert.Equal(["organic_structures", "geology_and_anomalies"], GroupKeys(page));
        Assert.True(Shows(page, "Known in Inner Orion Spur"));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoPositionTheRegionChoiceHasOnlyEveryRegion()
    {
        var (window, page) = PageHost.Open(() => Page(State(), SmallTree.Root, tree: SmallTree));

        Press(Named(page, "CodexCatalogueToken.Region"));
        var options = In<Segment>(page).Single(segment => AutomationProperties.GetName(segment) == "Region options");

        Assert.Equal([CodexCatalogueModel.EveryRegion], options.ItemsSource);
        Assert.Equal(CodexCatalogueModel.EveryRegionStatus.ToUpperInvariant(), Texts(options).Last());

        window.Close();
    }

    [AvaloniaFact]
    public void ClearFiltersReturnsBothValuesToTheirDefaults()
    {
        var state = State(Arrive(18));
        var choice = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => Page(state, SmallTree.Root, choice, tree: SmallTree));

        Assert.False(Named(page, "CodexCatalogueClear").IsVisible);

        Pick(page, "Status", 2);
        Pick(page, "Region", 1);
        Assert.True(Named(page, "CodexCatalogueClear").IsVisible);

        Press(Named(page, "CodexCatalogueClear"));

        Assert.True(choice.IsDefault);
        Assert.True(Shows(Named(page, "CodexCatalogueToken.Status"), "ALL"));
        Assert.True(Shows(Named(page, "CodexCatalogueToken.Region"), "EVERY REGION"));
        Assert.False(Named(page, "CodexCatalogueClear").IsVisible);
        Assert.Equal(["organic_structures", "geology_and_anomalies", "guardian_objects"], GroupKeys(page));

        window.Close();
    }

    [AvaloniaFact]
    public void AKindRowOpensItsEntryPageInTheFiltersRegion()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        var state = State(Arrive(18));
        var choice = new CodexCatalogueChoice();
        var node = SmallTree.Node("organic_structures")!;

        var (window, page) = PageHost.Open(() => Page(state, node, choice, nav, tree: SmallTree));

        Press(Named(page, "CodexKind.102"));
        Assert.Equal((CodexPage.EntryCrumb(18, "102", "Alpha Two").Key, "Alpha Two"), (nav.Trail[^1].Key, nav.Trail[^1].Word));
        window.Close();

        nav.Back();
        choice.Here = true;
        (window, page) = PageHost.Open(() => Page(state, node, choice, nav, tree: SmallTree));
        Press(Named(page, "CodexKindOpen.101"));
        Assert.Equal((CodexPage.EntryCrumb(18, "101", "Alpha One").Key, "Alpha One"), (nav.Trail[^1].Key, nav.Trail[^1].Word));
        window.Close();

        nav.Back();
        choice.Here = false;
        (window, page) = PageHost.Open(() => Page(State(), node, choice, nav, tree: SmallTree));
        Press(Named(page, "CodexKind.101"));
        Assert.Equal((CodexPage.EntryCrumb(null, "101", "Alpha One").Key, "Alpha One"), (nav.Trail[^1].Key, nav.Trail[^1].Word));
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetKindRowsShowPictureKindSystemsAndStatusUnderAPagerPastTwentyFive()
    {
        var state = State(Arrive(18), RockyScan);
        var choice = new CodexCatalogueChoice();
        choice.ChooseSegment(RockyBody, 1);
        var (window, page) = PageHost.Open(() => Page(state, Tree.Node(RockyBody)!, choice, headset: true), 1024, 640);

        var segment = In<Segment>(page).Single(item => item.Name == "CodexCatalogueSegment");
        Assert.Empty(segment.Statuses);

        var head = In<Control>(page).First(control => control.Name == "CodexKindRows");
        Assert.True(Shows(head, "Kind"));
        Assert.Contains(Texts(head), text => text.StartsWith("SYSTEMS", StringComparison.OrdinalIgnoreCase));
        Assert.True(Shows(head, "Status"));
        Assert.False(Shows(head, "First found"));

        Assert.Equal(FactionModel.PageSize, KindKeys(page).Length);
        Assert.False(Named(page, CodexCataloguePage.PreviousName + ".Kinds").IsEnabled);
        Assert.True(Named(page, CodexCataloguePage.NextName + ".Kinds").IsEnabled);

        var first = In<Button>(page).First(button => button.Name == "CodexKind." + NoAtmosphere);
        Assert.True(Shows(first, "ON RECORD"));
        Assert.True(Shows(first, "2026-11-01"));

        Press(Named(page, CodexCataloguePage.NextName + ".Kinds"));
        Assert.True(Named(page, CodexCataloguePage.PreviousName + ".Kinds").IsEnabled);
        Assert.DoesNotContain(NoAtmosphere, KindKeys(page));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-catalogue-headset.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheDesktopKindRowsShowFirstFoundAndBothSystemFiguresInARegion()
    {
        var state = State(Arrive(18), RockyScan);
        var choice = new CodexCatalogueChoice { Here = true };
        choice.ChooseSegment(RockyBody, 1);
        var (window, page) = PageHost.Open(() => Page(state, Tree.Node(RockyBody)!, choice));

        var row = Named(page, "CodexKind." + NoAtmosphere);
        var entry = Tree.Node(NoAtmosphere)!.Entry!;

        Assert.True(Shows(page, "First found"));
        Assert.Contains(CodexModel.Number(entry.Regions[18]), Texts(row));
        Assert.Contains(CodexModel.Number(entry.Regions.Values.Sum()), Texts(row));
        Assert.Contains("2026-11-01 09:05", Texts(row));
        Assert.True(Shows(page, CodexCatalogueModel.NotFoundHere));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-catalogue-region.png");
        }

        window.Close();
    }
}
