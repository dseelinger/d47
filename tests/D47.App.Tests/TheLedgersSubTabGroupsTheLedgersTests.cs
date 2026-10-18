using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

/// <summary>Commander › Ledgers: the list, the drill to each ledger, its place among the sub-tabs and the tick (#1291).</summary>
public class TheLedgersSubTabGroupsTheLedgersTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-10-10T16:14:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] SevenSubTabs =
        ["Checklist", "Missions", "Standing", "Statistics", "This session", "Ledgers", "Commanders"];

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

        foreach (var key in keys)
        {
            var word = key switch
            {
                BgsWorkPage.PageKey => "BGS work",
                CombatStatsPage.PageKey => "Combat stats",
                _ => "Credits",
            };

            Assert.True(nav.Declare(LedgersPage.RootKey, new NavCrumb(key, word)));
        }

        return nav;
    }

    [Fact]
    public void TheRowsFollowTheListsOrderWhateverOrderTheLedgersWereDeclaredIn()
    {
        var rows = LedgersPage.Rows(Declared(CreditsPage.PageKey, BgsWorkPage.PageKey, CombatStatsPage.PageKey));

        Assert.Equal(["BGS work", "Combat stats", "Credits"], rows.Select(row => row.Name));
        Assert.Equal(
            [
                "The work done for minor factions since the BGS tick.",
                "Your kills and the credits they were awarded.",
                "Where your credits came from and where they went.",
            ],
            rows.Select(row => row.Description));
        Assert.Equal([BgsWorkPage.PageKey, CombatStatsPage.PageKey, CreditsPage.PageKey], rows.Select(row => row.Crumb.Key));
    }

    [Fact]
    public void OnlyTheLedgersASurfaceEnabledAreListed()
    {
        var rows = LedgersPage.Rows(Declared(CombatStatsPage.PageKey));

        Assert.Equal(["Combat stats"], rows.Select(row => row.Name));
    }

    private static ChecklistService Checklists()
    {
        var root = TestSurface.MemoryFolder("d47-ledgers-page-tests");

        return new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
    }

    private static CommanderRoster Roster() => new(() => null, () => 0, () => null, () => false, _ => { });

    /// <summary>The desktop's Commander tab, enabled in the order MainWindow enables it.</summary>
    private static PanelView Desktop(CommanderGameState state)
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableChecklist(Checklists());
        view.EnableMissions(() => state, () => Now);
        view.EnableStanding(() => state);
        view.EnableStatistics(() => state);
        view.EnableSession(() => state);
        view.EnableBgsWork(() => state, () => BgsTick, () => Now);
        view.EnableCombatStats(() => state, () => HistoryState.Done, () => Now);
        view.EnableCredits(() => state, () => HistoryState.Done, () => Now);
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
        Assert.Equal(SevenSubTabs, view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));

        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var buttons = In<RadioButton>(view.FindControl<TextChoice>("ModeReadings")!).ToList();
        var lines = buttons.Select(button => button.TranslatePoint(default, window)!.Value.Y).Distinct().Count();
        var widths = string.Join(", ", buttons.Select(button => $"{button.Bounds.Width:0}"));

        Assert.Equal(SevenSubTabs.Length, buttons.Count);
        Assert.True(lines == 1, $"The sub-tabs wrap onto {lines} lines; item widths {widths}.");

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture(capture);
        }
    }

    [AvaloniaFact]
    public void TheDesktopsSevenSubTabsFitOneRowAt1024()
    {
        using var look = AppLook.Put();
        var view = Desktop(new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon")));
        var window = Show(view);

        SubTabsFitOneRow(view, window, "ledgers-desktop.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelsSevenSubTabsFitOneRowAt1024()
    {
        using var look = AppLook.Put();
        var view = BigPanel(new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon")));
        var window = Show(view);

        SubTabsFitOneRow(view, window, "ledgers-big-panel.png");

        window.Close();
    }

    [AvaloniaFact]
    public void TheListDrillsToEachLedgerAndTheLedgersCrumbReturnsToIt()
    {
        using var look = AppLook.Put();
        var view = Desktop(new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon")));
        var window = Show(view);

        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var list = In<LedgersPage>(view).Single();
        Assert.Equal(LedgersPage.SummaryText, list.Summary);
        Assert.IsNotAssignableFrom<IFilterablePage>(list);

        var rows = In<Button>(list).Where(button => button.Name?.StartsWith("Ledger.", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(["Ledger.bgs-work", "Ledger.combat-stats", "Ledger.credits"], rows.Select(row => row.Name));
        Assert.All(rows, row => Assert.True(row.Bounds.Height >= TypeScale.MinimumTarget));
        Assert.True(Shows(list, "BGS WORK"));
        Assert.True(Shows(list, "The work done for minor factions since the BGS tick."));

        foreach (var (row, word, page) in new (string, string, Type)[]
        {
            ("Ledger.bgs-work", "BGS work", typeof(BgsWorkPage)),
            ("Ledger.combat-stats", "Combat stats", typeof(CombatStatsPage)),
            ("Ledger.credits", "Credits", typeof(CreditsPage)),
        })
        {
            Press(In<Button>(view).Single(button => button.Name == row));

            Assert.Equal(["Ledgers", word], Crumbs(view));
            Assert.False(view.FindControl<Control>("ModePicker")!.IsVisible);
            Assert.Single(In<Control>(view), control => control.GetType() == page);
            Assert.Empty(In<LedgersPage>(view));

            if (TestSurface.CapturesWanted && row == "Ledger.credits")
            {
                using var frame = window.CaptureRenderedFrame()!;
                frame.SaveCapture("ledgers-credits-desktop.png");
            }

            Press(view.FindControl<StackPanel>("CrumbRow")!.Children.OfType<Button>().First());

            Assert.Equal(["Ledgers"], view.Nav.Trail.Select(crumb => crumb.Word));
            Assert.Single(In<LedgersPage>(view));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void EachLedgerIsADestinationUnderLedgersThatOpensWithTheBreadcrumb()
    {
        using var look = AppLook.Put();
        var view = BigPanel(new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon")));
        var window = Show(view);

        var underLedgers = view.Nav.Destinations.Where(destination => destination.Root.Key == LedgersPage.RootKey).ToList();
        Assert.Equal(
            ["Ledgers (Commander)", "BGS work (Ledgers)", "Combat stats (Ledgers)", "Credits (Ledgers)"],
            underLedgers.Select(destination => destination.Describe()));
        Assert.Equal(underLedgers.Count - 1, LedgersPage.Rows(view.Nav).Count);

        Assert.True(view.Nav.Show(CombatStatsPage.PageKey));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Commander, view.Tab);
        Assert.Equal(["Ledgers", "Combat stats"], Crumbs(view));
        Assert.Equal(CombatStatsPage.PageKey, view.Nav.DestinationKey);

        view.Nav.SelectRoot(PanelTab.Commander, MissionsPage.RootKey);
        view.Nav.SelectRoot(PanelTab.Commander, LedgersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Combat stats"], Crumbs(view));

        window.Close();
    }

    [AvaloniaFact]
    public void BgsWorkAndCombatStatsStillRedrawOnTheTickUnderLedgers()
    {
        using var look = AppLook.Put();
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));
        var view = Desktop(state);
        var window = Show(view);

        Assert.True(view.Nav.Show(BgsWorkPage.PageKey));
        Dispatcher.UIThread.RunJobs();
        view.TickCommander();
        Assert.True(Shows(In<BgsWorkPage>(view).Single(), "0 FACTIONS"));

        Apply(state, Work);
        Assert.True(view.TickCommander());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(In<BgsWorkPage>(view).Single(), "3 FACTIONS"));

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
