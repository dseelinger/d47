using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every tab draws the same header: tab row, title line, 1px rule, with the 104px avatar beside them (#777).</summary>
[Trait("Category", "Integration")]
public class EveryTabDrawsOneHeaderTests
{
    private const double Width = 1280;

    private const double Height = 800;

    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private static readonly PanelTab[] Every =
    [
        PanelTab.Transcript, PanelTab.Stories, PanelTab.Commander, PanelTab.Assets,
        PanelTab.Navigation, PanelTab.Settings,
    ];

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    /// <summary>Furnishes Stories, Commander, Asset Mgmt and Navigation from real pages.</summary>
    internal static void FurnishEveryTabButSettings(PanelView panel)
    {
        var root = TempFolders.Create("d47-one-header");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);

        panel.EnableLoadout(ships, checklists, () => state, onFoot);
        panel.EnableEngineers(new EngineerPlanService(builds, kit, checklists, () => state), ships, () => state, onFoot, checklists: checklists);
        panel.EnableChecklist(checklists);
        panel.EnableRouting(new RoutingSurface(() => new NavRoute(), () => "Sol", CapabilityRegistry.Build([]), Plans: null, () => true));

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(root, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);

        panel.EnableAdventures(new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => false, () => false, () => null, () => { }));
    }

    /// <summary>The desktop panel, 1280 wide unless told otherwise, with every tab furnished from real pages.</summary>
    private static SettingsHost Open(double width = Width)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: Height);
        var panel = host.Panel;

        FurnishEveryTabButSettings(panel);

        Dispatcher.UIThread.RunJobs();
        return host;
    }

    private static void Show(PanelView panel, PanelTab tab)
    {
        panel.Tab = tab;
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static double Top(PanelView panel, string name) =>
        panel.GetControl<Control>(name).TranslatePoint(default, panel)!.Value.Y;

    private static void Capture(Window window, string name) =>
        window.CaptureRenderedFrame()!.SaveCapture(name);

    [AvaloniaFact]
    public void EveryTabsH1IsItsNameAndTheRuleSitsAtOneHeight()
    {
        var host = Open();
        var panel = host.Panel;
        var rules = new List<double>();

        foreach (var tab in Every)
        {
            Show(panel, tab);

            var name = panel.GetControl<RadioButton>($"{tab}Tab").Content as string;

            Assert.Equal(name, panel.GetControl<TextBlock>("PageTitle").Text);
            Assert.True(panel.GetControl<Control>("TitleRule").IsEffectivelyVisible, $"{tab} draws no title rule");

            rules.Add(Top(panel, "TitleRule"));

            Capture(host.Window, $"header-{tab.ToString().ToLowerInvariant()}-1280.png");
        }

        Assert.All(rules, rule => Assert.Equal(rules[0], rule, 0.5));

        host.Close();
    }

    [AvaloniaFact]
    public void NoRootPageDrawsItsOwnScreenTitle()
    {
        var host = Open();
        var panel = host.Panel;
        var title = panel.GetControl<TextBlock>("PageTitle");

        foreach (var tab in Every)
        {
            Show(panel, tab);

            var titles = panel.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block != title && block.IsEffectivelyVisible && block.FontSize == TypeScale.Title)
                .Select(block => block.Text)
                .ToList();

            Assert.True(titles.Count == 0, $"{tab} draws {string.Join(", ", titles)} at Title size");
        }

        host.Close();
    }

    [AvaloniaFact]
    public void TheSummariesAreOnTheTitleLineAndNowhereElse()
    {
        var host = Open();
        var panel = host.Panel;
        var summary = panel.GetControl<TextBlock>("PageSummary");

        foreach (var (tab, expected) in new[]
                 {
                     (PanelTab.Assets, "1 of "),
                     (PanelTab.Stories, "Stories you fly, told by the ship's AI. Progress comes from your own journal."),
                     (PanelTab.Settings, "Changes apply as you make them."),
                 })
        {
            Show(panel, tab);

            if (tab == PanelTab.Assets)
            {
                panel.Nav.SelectRoot(EngineersPages.DirectoryRoot);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.StartsWith(expected, summary.Text);

            var elsewhere = panel.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block != summary && block.IsEffectivelyVisible && block.Text == summary.Text)
                .ToList();

            Assert.Empty(elsewhere);
        }

        Show(panel, PanelTab.Transcript);
        Assert.True(string.IsNullOrEmpty(summary.Text));

        host.Close();
    }

    /// <summary>At a width where the tab row is one row; at 1280 today's eight tabs wrap until #550 narrows them.</summary>
    [AvaloniaFact]
    public void TheAvatarRunsFromTheTabRowsTopToTheRule()
    {
        var host = Open(width: 1440);
        var panel = host.Panel;
        var avatar = panel.GetControl<AvatarView>("Avatar");

        Show(panel, PanelTab.Assets);

        Assert.Equal(new Size(PanelView.HeaderAvatarExtent, PanelView.HeaderAvatarExtent), avatar.Bounds.Size);
        Assert.Equal(Top(panel, "TabStrip"), Top(panel, "Avatar"), 0.5);
        Assert.Equal(Top(panel, "TitleRule"), Top(panel, "Avatar") + avatar.Bounds.Height, 0.5);

        host.Close();
    }

    [AvaloniaFact]
    public void ADrilledPageKeepsTheTabsNameAndTitlesItselfAtHeadingSize()
    {
        var host = Open();
        var panel = host.Panel;

        Show(panel, PanelTab.Assets);
        panel.Nav.SelectRoot(EngineersPages.DirectoryRoot);

        var liz = EngineerDirectory.All.Single(engineer => engineer.Name == "Liz Ryder");

        panel.Nav.Drill(EngineersPages.Crumb(liz));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ASSET MGMT", panel.GetControl<TextBlock>("PageTitle").Text);
        Assert.True(panel.GetControl<Control>("CrumbBar").IsVisible);

        Assert.Contains(
            panel.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "LIZ RYDER" && block.FontSize == TypeScale.Heading && block.IsEffectivelyVisible);

        Capture(host.Window, "header-engineers-drilled-1280.png");

        host.Close();
    }

    [AvaloniaFact]
    public void MiniDrawsNoHeader()
    {
        var host = Open();
        var panel = host.Panel;

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.False(panel.GetControl<Control>("PageHeader").IsVisible);
        Assert.False(panel.GetControl<Control>("TitleRule").IsVisible);

        host.Close();
    }
}
