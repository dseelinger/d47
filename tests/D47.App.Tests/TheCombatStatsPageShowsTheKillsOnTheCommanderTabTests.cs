using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

/// <summary>Commander › Ledgers › Combat stats as drawn: the table, the history states and its place under Ledgers (#1288).</summary>
public class TheCombatStatsPageShowsTheKillsOnTheCommanderTabTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] Kills =
    [
        """{"timestamp":"2026-10-10T09:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10}""",
        """{"timestamp":"2026-10-10T09:01:00Z","event":"Loadout","Ship":"anaconda","ShipID":7,"ShipName":"Grey Lady"}""",
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
        """{"timestamp":"2026-10-10T10:01:00Z","event":"Bounty","Target":"anaconda","TotalReward":1240000,"VictimFaction":"Raiders","Rewards":[{"Faction":"Beta","Reward":1240000}]}""",
        """{"timestamp":"2026-10-10T10:02:00Z","event":"FactionKillBond","Reward":5000,"AwardingFaction":"Holder","VictimFaction":"Losers"}""",
    ];

    private static CommanderGameState State(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static (Window Window, CombatStatsPage Page) Open(CommanderGameState state, HistoryState history, bool headset = false)
    {
        var look = AppLook.Put();

        var page = new CombatStatsPage(() => state, () => history, () => Now, headset);
        var window = new Window
        {
            Content = page,
            Width = headset ? 1000 : 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    [AvaloniaFact]
    public void TheDoneStateDrawsTheControlsTheHeadTheCareerLineAndTheTable()
    {
        var (window, page) = Open(State(Kills), HistoryState.Done);

        Assert.Equal(2, In<Segment>(page).Count());
        Assert.True(Shows(page, "3 TARGETS"));
        Assert.True(Shows(page, CombatStatsPage.CareerLine));
        Assert.True(Shows(page, "Eagle"));
        Assert.True(Shows(page, "1,240,000 CR"));
        Assert.True(page.Filters);
        Assert.Empty(In<Notice>(page));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("combat-stats-enemy-ship.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void WhileHistoryRunsOnlyTheReadingLineShowsAndTheSearchHasNothingToFilter()
    {
        var (window, page) = Open(State(Kills), HistoryState.Running);

        Assert.True(Shows(page, CombatStatsPage.RunningText));
        Assert.All(In<Segment>(page), segment => Assert.False(segment.IsEffectivelyVisible));
        Assert.DoesNotContain(In<TextBlock>(page), block => block.Text == "Eagle");
        Assert.False(page.Filters);

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedWalkShowsTheWarningAboveTheLiveRecord()
    {
        var (window, page) = Open(State(Kills), HistoryState.Failed);

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(CombatStatsPage.FailedLabel, notice.Label);
        Assert.Contains("start at 09:00 UTC today", notice.Text);
        Assert.True(Shows(page, "Eagle"));

        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptyPeriodSaysSoAndHasNoSortHeader()
    {
        var (window, page) = Open(State(), HistoryState.Done);

        Assert.True(Shows(page, "No kills in the last 7 days."));
        Assert.Contains(
            In<TextBlock>(page),
            block => block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "0 kills") == true);
        Assert.DoesNotContain(In<Button>(page), button => button.Name?.StartsWith("CombatStatsHead", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchWithNoMatchSaysSo()
    {
        var (window, page) = Open(State(Kills), HistoryState.Done);

        page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, CombatStatsPage.NoMatchText));
        Assert.True(Shows(page, "0 OF 3 TARGETS"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensCombatStatsUnderLedgersWithTheTighterSubTabGap()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-combat-stats-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = State();
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices { Checklists = checklists, GameState = () => state, HistoryState = () => HistoryState.Done }.Furnish(view);

        Assert.Equal(
            ["Checklist", "Missions", "Standing", "Statistics", "This session", "Ledgers"],
            view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));

        Assert.True(view.Nav.Show(CombatStatsPage.PageKey));
        var window = new Window { Content = view, Width = 1024, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Combat stats"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CombatStatsPage>(view));
        Assert.Equal(16, view.FindControl<TextChoice>("ModeReadings")!.ItemSpacing);

        window.Close();
    }
}
