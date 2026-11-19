using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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

/// <summary>The Codex's Another region segment opens a Region page under the catalogue page (#2610).</summary>
public class TheCodexRegionCanBeChosenFromAPickerTests
{
    private const int Here = 18;
    private const int There = 3;

    private static readonly CodexTable Small = CodexTable.Parse(
        "101\tAlpha One\tBiological\tOrganic structures\tAlpha\t18:5\n"
        + "102\tAlpha Two\tBiological\tOrganic structures\tAlpha\t3:4\n"
        + "301\tVent One\tGeology\tGeology and anomalies\tVents\t18:1\n"
        + "401\tGuardian One\tGuardian\tGuardian objects\t\t3:2\n");

    private static readonly CodexTree SmallTree = CodexTree.Of(Small);

    private static string Arrive(int region)
    {
        var at = GalacticRegions.LabelAt(region)!.Value;

        return "{\"timestamp\":\"2026-11-01T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Jump\",\"SystemAddress\":77,\"StarPos\":["
               + at.X.ToString("R", CultureInfo.InvariantCulture) + ",0," + at.Z.ToString("R", CultureInfo.InvariantCulture) + "]}";
    }

    private static string Codex(long id, int region) =>
        "{\"timestamp\":\"2026-11-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name_Localised\":\"Entry " + id
        + "\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_" + region + ";\",\"System\":\"Jump\",\"IsNewEntry\":true}";

    private static CommanderGameState State(params string[] lines) => JournalScript.Commander("F735466", "John Deparagon", lines);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static void Dispatch() => Dispatcher.UIThread.RunJobs();

    private static void Press(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatch();
    }

    private static Button Named(Control root, string name) => In<Button>(root).First(button => button.Name == name);

    private static string[] Texts(Control root) => [.. In<TextBlock>(root).Select(block => block.Text ?? string.Empty)];

    private static string[] GroupKeys(Control page) =>
        [.. In<Button>(page).Where(button => button.Name?.StartsWith("CodexGroup.", StringComparison.Ordinal) == true)
            .Select(button => button.Name!["CodexGroup.".Length..])];

    private static Segment RegionSegment(Control page) =>
        In<Segment>(page).Single(segment => AutomationProperties.GetName(segment) == "Region options");

    private static void OpenRegionToken(Control page) => Press(Named(page, "CodexCatalogueToken.Region"));

    private static void Choose(Control page, int option)
    {
        RegionSegment(page).GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;
        Dispatch();
    }

    private sealed record Setup(Window Window, CodexCataloguePage Page, PanelNavigator Nav, PanelPrompts Prompts, CodexCatalogueChoice Choice);

    private static Setup Open(bool headset = false, CodexTree? tree = null, params string[] lines)
    {
        var state = State(lines.Length == 0 ? [Arrive(Here), Codex(101, Here), Codex(102, There)] : lines);
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        var prompts = new PanelPrompts(nav, new Avalonia.Controls.Panel());
        var choice = new CodexCatalogueChoice();
        var (window, page) = PageHost.Open(() => new CodexCataloguePage(
            () => state, () => HistoryState.Done, choice, (tree ?? SmallTree).Root, headset, nav, tree: tree ?? SmallTree, prompts: prompts));

        return new Setup(window, page, nav, prompts, choice);
    }

    private static PickerPage Picker(Setup setup) => (PickerPage)setup.Prompts.Build(setup.Nav.Trail[^1])!;

    private static ListBox List(PickerPage picker) => In<ListBox>(picker).Single(box => box.Name == "Choices");

    private static string[] Values(PickerPage picker) => [.. List(picker).Items.Cast<PickerChoice>().Select(row => row.Value)];

    private static PickerChoice Row(PickerPage picker, int region) =>
        List(picker).Items.Cast<PickerChoice>().Single(row => row.Value == region.ToString(CultureInfo.InvariantCulture));

    private static void Take(PickerPage picker, int region)
    {
        var list = List(picker);
        list.SelectedIndex = Array.FindIndex([.. list.Items.Cast<PickerChoice>()], row => row.Value == region.ToString(CultureInfo.InvariantCulture));
        Press(In<Button>(picker).Single(button => button.Name == "AcceptButton"));
    }

    private static PickerPage OpenPicker(Setup setup)
    {
        OpenRegionToken(setup.Page);
        Choose(setup.Page, 2);

        var picker = Picker(setup);
        var host = new Window { Content = picker, Width = 640, Height = 900 };
        host.Show();
        Dispatch();

        return picker;
    }

    [AvaloniaFact]
    public void TheRegionTokenOffersAThirdSegmentThatSaysHowManyRegionsThereAre()
    {
        var setup = Open();

        OpenRegionToken(setup.Page);

        var segment = RegionSegment(setup.Page);

        Assert.Equal(3, segment.ItemsSource.Count);
        Assert.Equal(["Every region", GalacticRegions.NameOf(Here)!, "Another region"], segment.ItemsSource);
        Assert.Contains("CHOOSE FROM 42", Texts(segment).Select(text => text.ToUpperInvariant()));
        Assert.Equal(42, GalacticRegions.Names.Count);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void TheThirdSegmentOpensARegionPageWhoseFirstRowIsTheRegionTheCommanderIsIn()
    {
        var setup = Open();
        var depth = setup.Nav.Trail.Count;

        var picker = OpenPicker(setup);

        Assert.Equal(depth + 1, setup.Nav.Trail.Count);
        Assert.Equal(("codex-region-choice", "Region"), (setup.Nav.Trail[^1].Key, setup.Nav.Trail[^1].Word));
        Assert.Equal(Here.ToString(CultureInfo.InvariantCulture), Values(picker)[0]);
        Assert.Equal(GalacticRegions.Names.Count, Values(picker).Length);
        Assert.Contains(CodexRegionPicker.Help, Texts(picker));

        var here = Row(picker, Here);

        Assert.Equal(GalacticRegions.NameOf(Here), here.Text);
        Assert.True(here.Marked);
        Assert.Equal("2 kinds known · 1 on record · you are here", here.Detail);
        Assert.Equal("1 missing", here.Aside);

        var there = Row(picker, There);

        Assert.False(there.Marked);
        Assert.Equal("2 kinds known · 1 on record", there.Detail);
        Assert.Equal("1 missing", there.Aside);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void RegionsWithAKindOnRecordComeBeforeTheRestWhichAreByName()
    {
        var setup = Open();
        var picker = OpenPicker(setup);
        var values = Values(picker);
        var onRecord = values.Skip(1).TakeWhile(value => Row(picker, int.Parse(value, CultureInfo.InvariantCulture)).Detail!.Contains("on record", StringComparison.Ordinal)
            && !Row(picker, int.Parse(value, CultureInfo.InvariantCulture)).Detail!.Contains("none on record", StringComparison.Ordinal)).ToArray();
        var rest = values.Skip(1 + onRecord.Length).Select(value => Row(picker, int.Parse(value, CultureInfo.InvariantCulture))).ToArray();

        Assert.Equal([There.ToString(CultureInfo.InvariantCulture)], onRecord);
        Assert.All(rest, row => Assert.Contains("none on record", row.Detail));
        Assert.Equal(rest.Select(row => row.Text).Order(StringComparer.CurrentCultureIgnoreCase), rest.Select(row => row.Text));
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void UseThisOnAnotherRegionReturnsToThePageWithThatRegionsCountsAndTheSegmentCarriesItsName()
    {
        var setup = Open();
        var depth = setup.Nav.Trail.Count;
        var picker = OpenPicker(setup);

        var before = GroupKeys(setup.Page);

        Take(picker, There);

        Assert.Equal(depth, setup.Nav.Trail.Count);
        Assert.Equal(There, setup.Choice.Other);
        Assert.False(setup.Choice.Here);

        var after = GroupKeys(setup.Page);

        Assert.NotEqual(before, after);
        Assert.Equal(CodexNodeDiscoveries.For(Ledger(setup), SmallTree.Root, There).Groups.Select(group => group.Node.Key), after);

        OpenRegionToken(setup.Page);

        var segment = RegionSegment(setup.Page);

        Assert.Equal(GalacticRegions.NameOf(There), segment.ItemsSource[2]);
        Assert.Equal(2, segment.SelectedIndex);
        Assert.Contains(GalacticRegions.NameOf(There)!.ToUpperInvariant(), Texts(setup.Page).Select(text => text.ToUpperInvariant()));
        setup.Window.Close();
    }

    private static CodexLedger Ledger(Setup setup) => State(Arrive(Here), Codex(101, Here), Codex(102, There)).CodexLedger!;

    [AvaloniaFact]
    public void BackLeavesTheFilterAsItWas()
    {
        var setup = Open();
        var depth = setup.Nav.Trail.Count;
        var before = GroupKeys(setup.Page);
        var picker = OpenPicker(setup);

        Press(In<Button>(picker).Single(button => button.Name == "BackButton"));

        Assert.Equal(depth, setup.Nav.Trail.Count);
        Assert.Null(setup.Choice.Other);
        Assert.False(setup.Choice.Here);
        Assert.True(setup.Choice.IsDefault);
        Assert.Equal(before, GroupKeys(setup.Page));

        OpenRegionToken(setup.Page);

        Assert.Equal(0, RegionSegment(setup.Page).SelectedIndex);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void BackAfterAnotherRegionWasChosenKeepsThatRegion()
    {
        var setup = Open();
        Take(OpenPicker(setup), There);

        var picker = OpenPickerFromAnotherRegion(setup);

        Assert.Equal(There.ToString(CultureInfo.InvariantCulture), Values(picker)[1]);

        Press(In<Button>(picker).Single(button => button.Name == "BackButton"));

        Assert.Equal(There, setup.Choice.Other);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void TheDefaultTileSetsTheRegionTheCommanderIsIn()
    {
        var setup = Open();
        setup.Choice.Other = There;
        var picker = OpenPickerFromAnotherRegion(setup);

        var tile = In<Button>(picker).Single(button => button.Name == "DefaultButton");

        Assert.True(tile.IsVisible);
        Assert.Contains($"Use the region I am in ({GalacticRegions.NameOf(Here)})", Texts(tile));

        Press(tile);

        Assert.True(setup.Choice.Here);
        Assert.Null(setup.Choice.Other);
        setup.Window.Close();
    }

    private static PickerPage OpenPickerFromAnotherRegion(Setup setup)
    {
        OpenRegionToken(setup.Page);
        RegionSegment(setup.Page).GetVisualDescendants().OfType<RadioButton>().ElementAt(2).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatch();

        var picker = Picker(setup);
        new Window { Content = picker, Width = 640, Height = 900 }.Show();
        Dispatch();

        return picker;
    }

    [AvaloniaFact]
    public void WithNoPositionThereIsNoDefaultTileAndAnotherRegionIsTheSecondSegment()
    {
        var setup = Open(lines: ["{\"timestamp\":\"2026-11-01T09:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"X\"}"]);

        OpenRegionToken(setup.Page);

        Assert.Equal(["Every region", "Another region"], RegionSegment(setup.Page).ItemsSource);

        Choose(setup.Page, 1);

        var picker = Picker(setup);
        new Window { Content = picker, Width = 640, Height = 900 }.Show();
        Dispatch();

        Assert.False(In<Button>(picker).Single(button => button.Name == "DefaultButton").IsVisible);
        Assert.DoesNotContain(Values(picker), value => Row(picker, int.Parse(value, CultureInfo.InvariantCulture)).Marked);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void ChoosingTheRegionTheCommanderIsInSetsHereRatherThanAnotherRegion()
    {
        var setup = Open();

        Take(OpenPicker(setup), Here);

        Assert.True(setup.Choice.Here);
        Assert.Null(setup.Choice.Other);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void ClearFiltersReturnsTheTokenToEveryRegion()
    {
        var setup = Open();
        Take(OpenPicker(setup), There);

        Press(Named(setup.Page, "CodexCatalogueClear"));

        Assert.True(setup.Choice.IsDefault);
        Assert.Null(setup.Choice.Other);

        OpenRegionToken(setup.Page);

        var segment = RegionSegment(setup.Page);

        Assert.Equal(0, segment.SelectedIndex);
        Assert.Equal("Another region", segment.ItemsSource[2]);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void OnTheHeadsetTheRegionsPageListsTwentyFiveRowsWithAPager()
    {
        var setup = Open(headset: true);
        var picker = OpenPicker(setup);

        Assert.Equal(25, Values(picker).Length);
        Assert.Equal(Here.ToString(CultureInfo.InvariantCulture), Values(picker)[0]);
        Assert.Contains("1–25 of 42", Texts(picker));
        Assert.False(In<Button>(picker).Single(button => button.Name == "PagerPrevious").IsEnabled);

        Press(In<Button>(picker).Single(button => button.Name == "PagerNext"));

        Assert.Equal(17, Values(picker).Length);
        Assert.Contains("26–42 of 42", Texts(picker));
        Assert.False(In<Button>(picker).Single(button => button.Name == "PagerNext").IsEnabled);

        Take(picker, int.Parse(Values(picker)[^1], CultureInfo.InvariantCulture));

        Assert.NotNull(setup.Choice.Other);
        setup.Window.Close();
    }

    [AvaloniaFact]
    public void TheDesktopRegionsPageListsEveryRegionOnOnePage()
    {
        var setup = Open();
        var picker = OpenPicker(setup);

        Assert.Equal(42, Values(picker).Length);
        Assert.False(In<DockPanel>(picker).Single(panel => panel.Name == "Pager").IsVisible);
        setup.Window.Close();
    }
}
