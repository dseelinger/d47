using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers: the ledgers in the page sidebar at every depth, the sub-tab row and the breadcrumb, and the tick (#2685).</summary>
public class TheLedgersSubTabGroupsTheLedgersTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-10-10T16:14:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] SubTabs =
        ["Checklist", "Missions", "Progress", "Statistics", "BGS", "Ledgers", "Commanders"];

    private static readonly string[] Work =
    [
        """{"timestamp":"2026-10-10T17:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""",
        """{"timestamp":"2026-10-10T17:05:00Z","event":"RedeemVoucher","Type":"bounty","Factions":[{"Faction":"Gamma Pirates","Amount":3000}]}""",
        """{"timestamp":"2026-10-10T17:08:00Z","event":"SellExplorationData","TotalEarnings":123456}""",
        """{"timestamp":"2026-10-10T17:09:00Z","event":"MissionCompleted","FactionEffects":[{"Faction":"Beta Union","Influence":[{"SystemAddress":2,"Trend":"UpGood","Influence":"+++"}]}]}""",
    ];

    private static readonly string[] Kills =
    [
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
        """{"timestamp":"2026-10-10T10:01:00Z","event":"Bounty","Target":"anaconda","TotalReward":1240000,"VictimFaction":"Raiders","Rewards":[{"Faction":"Beta","Reward":1240000}]}""",
    ];

    private static void Apply(CommanderGameState state, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }
    }

    private static PanelNavigator Declared(params string[] keys)
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, LedgersPage.Root);
        nav.HoldPages(LedgersPage.RootKey, LedgersPage.Order);

        foreach (var key in keys)
        {
            var word = key switch
            {
                CombatStatsPage.PageKey => "Combat stats",
                CodexPage.PageKey => "Codex",
                SessionPage.RootKey => "This session",
                _ => "Credits",
            };

            Assert.True(nav.Declare(LedgersPage.RootKey, new NavCrumb(key, word)));
        }

        return nav;
    }

    [Fact]
    public void ThePagesFollowTheSidebarsOrderWhateverOrderTheLedgersWereDeclaredIn()
    {
        var nav = Declared(CodexPage.PageKey, CreditsPage.PageKey, CombatStatsPage.PageKey, SessionPage.RootKey);

        Assert.Equal(["This session", "Combat stats", "Credits", "Codex"], nav.PagesOf(LedgersPage.RootKey).Select(page => page.Word));
    }

    [Fact]
    public void OnlyTheLedgersASurfaceEnabledAreListed()
    {
        Assert.Equal(["Combat stats"], Declared(CombatStatsPage.PageKey).PagesOf(LedgersPage.RootKey).Select(page => page.Word));
    }

    private static ChecklistService Checklists()
    {
        var root = TestSurface.MemoryFolder("d47-ledgers-page-tests");

        return TestSurface.Checklists(root);
    }

    private static CommanderRoster Roster() => new(() => null, () => 0, () => null, () => false, _ => { });

    private static CampaignSources Campaign(CommanderGameState state) => new(
        () => state,
        new D47.Core.Campaigns.CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<D47.Core.Campaigns.CampaignStore>.Instance),
        () => null,
        () => null,
        () => null,
        D47.Core.Capabilities.CapabilityRegistry.Build([]));

    /// <summary>The desktop's Commander tab, enabled in the order MainWindow enables it.</summary>
    private static PanelView Desktop(CommanderGameState state)
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableChecklist(Checklists());
        view.EnableMissions(() => state, () => Now);
        view.EnableStanding(() => state);
        view.EnableStatistics(() => state);
        view.EnableCampaign(Campaign(state));
        view.EnableCombatStats(() => state, () => HistoryState.Done, () => Now);
        view.EnableCredits(() => state, () => HistoryState.Done, () => Now);
        view.EnableCodex(() => state, () => HistoryState.Done, () => Now);
        view.EnableSession(() => state);
        view.EnableCommanders(Roster());

        return view;
    }

    private static PanelView BigPanel(CommanderGameState state)
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices
        {
            Checklists = Checklists(),
            GameState = () => state,
            HistoryState = () => HistoryState.Done,
            Commanders = Roster(),
            Campaign = Campaign(state),
        }.Furnish(view);

        return view;
    }

    private static Window Show(PanelView view)
    {
        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 640,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static string[] Crumbs(PanelView view) =>
        [.. view.FindControl<StackPanel>("CrumbRow")!.Children.OfType<Button>().Select(button => (string)button.Content!)];

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void SubTabsFitOneRow(PanelView view, Window window, string capture)
    {
        Assert.Equal(SubTabs, view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));

        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var buttons = In<RadioButton>(view.FindControl<TextChoice>("ModeReadings")!).ToList();
        var lines = buttons.Select(button => button.TranslatePoint(default, window)!.Value.Y).Distinct().Count();
        var widths = string.Join(", ", buttons.Select(button => $"{button.Bounds.Width:0}"));

        Assert.Equal(SubTabs.Length, buttons.Count);
        Assert.True(lines == 1, $"The sub-tabs wrap onto {lines} lines; item widths {widths}.");

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture(capture);
        }
    }

    [AvaloniaFact]
    public void TheDesktopsSubTabsFitOneRowAt1024()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        SubTabsFitOneRow(view, window, "ledgers-desktop.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelsSubTabsFitOneRowAt1024()
    {
        using var look = AppLook.Put();
        var view = BigPanel(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        SubTabsFitOneRow(view, window, "ledgers-big-panel.png");

        window.Close();
    }

    private static Sidebar PageSidebar(PanelView view) => In<Sidebar>(view).Single();

    private static string Label(Border item) => item.GetVisualDescendants().OfType<TextBlock>().First().Text!;

    private static void PressItem(PanelView view, Window window, string word)
    {
        var item = PageSidebar(view).Items.Single(row => Label(row) == word.ToUpperInvariant());
        var point = item.TranslatePoint(new Point(20, 20), window)!.Value;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void OpenLedgers(PanelView view)
    {
        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OpeningLedgersOpensThisSessionFirstAndThenTheLedgerLastOpen()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);

        Assert.Equal(["Ledgers", "This session"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<SessionPage>(view));

        PressItem(view, window, "Credits");
        view.Nav.SelectRoot(PanelTab.Commander, MissionsPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(In<Sidebar>(view));

        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Credits"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CreditsPage>(view));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageSidebarListsTheLedgersWithTheOpenOneSolid()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);

        var sidebar = PageSidebar(view);

        Assert.Equal(["THIS SESSION", "COMBAT STATS", "CREDITS", "CODEX"], sidebar.Items.Select(Label));
        Assert.All(sidebar.Items, row => Assert.Equal(TypeScale.MinimumTarget, row.Bounds.Height));
        Assert.Equal(SessionPage.RootKey, sidebar.Selected);
        Assert.Equal(
            ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.AKey]!).Color,
            ((ISolidColorBrush)sidebar.Items[0].Background!).Color);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("ledgers-sidebar-desktop.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void PressingASidebarItemOpensItsLedgerAndTheSubTabRowStays()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);

        foreach (var (word, page) in new (string, Type)[]
        {
            ("Combat stats", typeof(CombatStatsPage)),
            ("Credits", typeof(CreditsPage)),
            ("Codex", typeof(CodexCataloguePage)),
            ("This session", typeof(SessionPage)),
        })
        {
            PressItem(view, window, word);

            Assert.Equal(["Ledgers", word], view.Nav.Trail.Select(crumb => crumb.Word));
            Assert.Empty(Crumbs(view));
            Assert.True(view.FindControl<Control>("ModePicker")!.IsVisible);
            Assert.Equal(view.Nav.Trail[1].Key, PageSidebar(view).Selected);
            Assert.Single(In<LedgersPage>(view));
            Assert.Single(In<Control>(view), control => control.GetType() == page);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void BelowALedgerTheBreadcrumbStartsAtTheTabAndTheSidebarStaysWithTheLedgerSelected()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);
        PressItem(view, window, "Codex");

        Assert.True(view.Nav.Drill(CodexPage.RegionCrumb(null)));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(In<CodexRegionPage>(view));
        Assert.Empty(In<LedgersPage>(view));
        Assert.False(view.FindControl<Control>("ModePicker")!.IsVisible);
        Assert.Equal(["Commander", "Ledgers", "Codex", view.Nav.Trail[^1].Word], Crumbs(view));
        Assert.Equal(CodexPage.PageKey, PageSidebar(view).Selected);

        Assert.True(view.Nav.Back());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CodexCataloguePage>(view));
        Assert.Empty(Crumbs(view));
        Assert.True(view.FindControl<Control>("ModePicker")!.IsVisible);
        Assert.False(view.Nav.Back());

        window.Close();
    }

    [AvaloniaFact]
    public void OnePressOnASidebarItemOpensThatLedgerFromACodexEntry()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        Assert.True(PageTrail.Open(view.Nav, LedgersPage.RootKey, [
            LedgersPage.Ledger(CodexPage.PageKey, "Codex"),
            CodexPage.RegionsCrumb(),
            CodexPage.RegionCrumb(null),
            CodexPage.EntryCrumb(null, "1", "Entry")]));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex"], view.Nav.Trail.Take(2).Select(crumb => crumb.Word));
        Assert.Equal(5, view.Nav.Trail.Count);
        Assert.Equal(CodexPage.PageKey, PageSidebar(view).Selected);
        Assert.Equal("Commander", Crumbs(view)[0]);

        PressItem(view, window, "Credits");

        Assert.Equal(["Ledgers", "Credits"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CreditsPage>(view));
        Assert.DoesNotContain(In<Control>(view), control => control is CodexCataloguePage);
        Assert.Empty(Crumbs(view));

        window.Close();
    }

    [AvaloniaFact]
    public void AnOpenLedgerScrollsWithTheLedgersPageNotInsideItself()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);
        PressItem(view, window, "Credits");

        var outer = In<ScrollViewer>(In<LedgersPage>(view).Single()).First();
        var inner = In<ScrollViewer>(In<CreditsPage>(view).Single()).Single();

        Assert.NotNull(outer);
        Assert.Equal(inner.Viewport.Height, inner.Extent.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void TheTitleLineSummarisesTheOpenLedger()
    {
        using var look = AppLook.Put();
        var view = Desktop(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);

        Assert.Equal("What you have earned and done since the game started.", In<LedgersPage>(view).Single().Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageSidebarIsOnTheBigPanelToo()
    {
        using var look = AppLook.Put();
        var view = BigPanel(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        OpenLedgers(view);
        PressItem(view, window, "Codex");

        Assert.Equal(CodexPage.PageKey, PageSidebar(view).Selected);
        Assert.Equal(["Ledgers", "Codex"], view.Nav.Trail.Select(crumb => crumb.Word));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("ledgers-sidebar-big-panel.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void EachLedgerIsADestinationUnderLedgersThatOpensWithTheSidebar()
    {
        using var look = AppLook.Put();
        var view = BigPanel(JournalScript.Commander("F735466", "John Deparagon"));
        var window = Show(view);

        var underLedgers = view.Nav.Destinations.Where(destination => destination.Root.Key == LedgersPage.RootKey).ToList();
        Assert.Equal(
            ["Ledgers (Commander)", "Combat stats (Ledgers)", "Credits (Ledgers)", "Codex (Ledgers)", "This session (Ledgers)"],
            underLedgers.Select(destination => destination.Describe()));
        Assert.Equal(underLedgers.Count - 1, view.Nav.PagesOf(LedgersPage.RootKey).Count);

        Assert.True(view.Nav.Show(CombatStatsPage.PageKey));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Commander, view.Tab);
        Assert.Equal(["Ledgers", "Combat stats"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Equal(CombatStatsPage.PageKey, view.Nav.DestinationKey);
        Assert.Equal(CombatStatsPage.PageKey, PageSidebar(view).Selected);

        view.Nav.SelectRoot(PanelTab.Commander, MissionsPage.RootKey);
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Combat stats"], view.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void CombatStatsStillRedrawOnTheTickUnderLedgers()
    {
        using var look = AppLook.Put();
        var state = JournalScript.Commander("F735466", "John Deparagon");
        var view = Desktop(state);
        var window = Show(view);

        Assert.True(view.Nav.Show(CombatStatsPage.PageKey));
        Dispatcher.UIThread.RunJobs();
        view.TickCommander();
        Assert.False(Shows(In<CombatStatsPage>(view).Single(), "Eagle"));

        Apply(state, Kills);
        Assert.True(view.TickCommander());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(In<CombatStatsPage>(view).Single(), "Eagle"));

        window.Close();
    }
}
