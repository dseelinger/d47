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

/// <summary>Commander › Credits as drawn: the kinds table, the transactions page, the history states and the sub-tab row (#1289).</summary>
public class TheCreditsPageShowsTheLedgerOnTheCommanderTabTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] Lines =
    [
        """{"timestamp":"2026-10-09T09:00:00Z","event":"LoadGame","Credits":1000000}""",
        """{"timestamp":"2026-10-09T10:00:00Z","event":"MarketSell","TotalSale":4812000}""",
        """{"timestamp":"2026-10-09T11:00:00Z","event":"MarketBuy","TotalCost":3120400}""",
        """{"timestamp":"2026-10-09T12:00:00Z","event":"CarrierBankTransfer","Deposit":200000,"PlayerBalance":2491600}""",
        """{"timestamp":"2026-10-09T13:00:00Z","event":"Repair","Cost":5000}""",
        """{"timestamp":"2026-10-09T14:00:00Z","event":"LoadGame","Credits":2000000000}""",
        """{"timestamp":"2026-10-09T15:00:00Z","event":"LoadGame","Credits":1999999500}""",
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

    private static (Window Window, T Page) Open<T>(Func<T> make)
        where T : Control
    {
        var look = AppLook.Put();
        var page = make();
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static CreditsPage Root(CommanderGameState state, HistoryState history, CreditsChoice? choice = null, PanelNavigator? nav = null, bool headset = false) =>
        new(() => state, () => history, () => Now, choice ?? new CreditsChoice(), nav ?? new PanelNavigator(), headset);

    private static CreditTransactionsPage Transactions(CommanderGameState state, CreditKind? kind = null, CreditsChoice? choice = null, bool headset = false) =>
        new(() => state, () => HistoryState.Done, () => Now, choice ?? new CreditsChoice(), kind, headset);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static string[] Cells(Control root, string name) =>
        [.. In<TextBlock>(In<Control>(root).First(control => control.Name == name)).Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheRootDrawsTheStripTheKindsAndTheMovedKindsBelow()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done));

        Assert.Single(In<Segment>(page));
        Assert.Equal(["IN", "+4,812,000 CR", "earned"], Cells(page, "CreditsStrip.In"));
        Assert.Equal(["OUT", "−3,125,400 CR", "spent"], Cells(page, "CreditsStrip.Out"));
        Assert.Equal(["CARRIER BANK", "−200,000 CR", "net, moved to your carrier"], Cells(page, "CreditsStrip.Carrierbank"));
        Assert.Equal("2 gaps with the game", Cells(page, "CreditsStrip.Unaccounted")[2]);
        Assert.True(Shows(page, "Trade"));
        Assert.True(Shows(page, "goods and trade data"));
        Assert.True(Shows(page, CreditsModel.MovedLabel.ToUpperInvariant()) || Shows(page, CreditsModel.MovedLabel));
        Assert.True(Shows(page, "4 KINDS"));
        Assert.True(page.Filters);
        Assert.Empty(In<Notice>(page));
        Assert.All(In<Button>(page).Where(button => button.Name?.StartsWith("CreditsKind.", StringComparison.Ordinal) == true), button => Assert.True(button.Bounds.Height >= 44));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("credits-by-kind.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAKindRowOrTheAllTileDrillsToTheTransactionsPage()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CreditsPage.RootKey, "Credits"));
        nav.Select(PanelTab.Commander);

        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done, nav: nav));

        In<Button>(page).First(button => button.Name == "CreditsKind.Trade").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("credits-kind-Trade", nav.Trail[^1].Key);
        Assert.Equal(CreditKind.Trade, CreditsPage.KindOf(nav.Trail[^1].Key));

        nav.Back();
        In<Button>(page).First(button => button.Name == "CreditsAll").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(CreditsPage.AllKey, nav.Trail[^1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public void WhileHistoryRunsOnlyTheReadingLineShowsAndTheSearchHasNothingToFilter()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Running));

        Assert.True(Shows(page, CreditsModel.RunningText));
        Assert.All(In<Segment>(page), segment => Assert.False(segment.IsEffectivelyVisible));
        Assert.DoesNotContain(In<TextBlock>(page), block => block.Text == "Trade");
        Assert.False(page.Filters);

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedWalkShowsTheWarningAboveTheLiveRecord()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Failed));

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(CreditsModel.FailedLabel, notice.Label);
        Assert.Contains("at 09:00 UTC today", notice.Text);
        Assert.True(Shows(page, "Trade"));

        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptyPeriodSaysSoWithNoStripAndNoSortHeader()
    {
        var (window, page) = Open(() => Root(State(), HistoryState.Done));

        Assert.True(Shows(page, "No credits moved in the last 7 days."));
        Assert.DoesNotContain(In<Control>(page), control => control.Name?.StartsWith("CreditsStrip.", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(In<Button>(page), button => button.Name?.StartsWith("CreditsHead", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchWithNoMatchSaysSoAndANarrowedCountReadsNOfM()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done));

        page.Filter("repair");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "1 OF 4 KINDS"));

        page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, CreditsModel.NoMatchText));

        window.Close();
    }

    [AvaloniaFact]
    public void TheTransactionsPageDrawsTheKindsRowsAndTheGapLines()
    {
        var state = State(Lines);
        var (window, page) = Open(() => Transactions(state));

        Assert.True(Shows(page, "6 ENTRIES"));
        Assert.True(Shows(page, CreditsModel.GapLine));
        Assert.False(Shows(page, CreditsModel.NoBalanceLine));
        Assert.True(Shows(page, "2026-10-09 12:00"));
        Assert.True(Shows(page, "Moved to your carrier. Not spent."));
        Assert.True(Shows(page, "Not in the journal. The game's balance at this login was 1,997,513,400 CR more than the journal adds up to."));
        Assert.True(Shows(page, "1 small gap"));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("credits-transactions.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void AKindWithNothingInThePeriodSaysSo()
    {
        var (window, page) = Open(() => Transactions(State(Lines), CreditKind.Rescue));

        Assert.True(Shows(page, "No Rescue entries in the last 7 days."));

        window.Close();
    }

    [AvaloniaFact]
    public void TheTransactionsListIsVirtualized()
    {
        var lines = new List<string> { """{"timestamp":"2026-10-09T00:00:00Z","event":"LoadGame","Credits":1000}""" };
        var at = DateTimeOffset.Parse("2026-10-09T00:00:01Z", CultureInfo.InvariantCulture);

        for (var i = 0; i < 3000; i++)
        {
            lines.Add($$"""{"timestamp":"{{at.AddSeconds(i):yyyy-MM-ddTHH:mm:ssZ}}","event":"MarketSell","TotalSale":10}""");
        }

        var (window, page) = Open(() => Transactions(State([.. lines])));

        var drawn = In<Border>(In<ItemsControl>(page).First(list => list.Name == "CreditsRows")).Count(border => border.MinHeight >= 44);

        Assert.InRange(drawn, 1, 100);
        Assert.True(Shows(page, "3,000 ENTRIES"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheTransactionsListKeepsItsScrollPositionWhenTheMinuteTurns()
    {
        var lines = new List<string> { """{"timestamp":"2026-10-09T00:00:00Z","event":"LoadGame","Credits":1000}""" };
        var at = DateTimeOffset.Parse("2026-10-09T00:00:01Z", CultureInfo.InvariantCulture);

        for (var i = 0; i < 3000; i++)
        {
            lines.Add($$"""{"timestamp":"{{at.AddSeconds(i):yyyy-MM-ddTHH:mm:ssZ}}","event":"MarketSell","TotalSale":10}""");
        }

        var state = State([.. lines]);
        var now = Now;
        var (window, page) = Open(() => new CreditTransactionsPage(() => state, () => HistoryState.Done, () => now, new CreditsChoice(), null));

        var scroll = In<ScrollViewer>(page).First(viewer => viewer.Content is Border { Child: ItemsControl });
        scroll.Offset = new Vector(0, 2000);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2000, scroll.Offset.Y);

        var list = In<ItemsControl>(page).First(items => items.Name == "CreditsRows");
        var before = list.ItemsSource;

        now = now.AddMinutes(1);
        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();

        Assert.Same(before, list.ItemsSource);
        Assert.Equal(2000, scroll.Offset.Y);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePeriodChosenOnOnePageCarriesToTheOther()
    {
        var choice = new CreditsChoice();
        var state = State(Lines);
        var (window, panel) = Open(() => new StackPanel
        {
            Children = { Root(state, HistoryState.Done, choice), Transactions(state, null, choice) },
        });
        var root = panel.Children.OfType<CreditsPage>().Single();
        var transactions = panel.Children.OfType<CreditTransactionsPage>().Single();

        In<RadioButton>(In<Segment>(transactions).First()).ElementAt(3).IsChecked = true;
        Assert.Equal(3, choice.Period);

        Assert.True(root.Tick());
        Assert.Equal(3, In<Segment>(root).First().SelectedIndex);
        Assert.True(Shows(root, "EVERYTHING") || Shows(root, "Everything"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelListsCreditsAfterCombatStatsAndBeforeCommanders()
    {
        using var look = AppLook.Put();

        var root = TestSurface.MemoryFolder("d47-credits-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = State([.. Lines.Select(line => line.Replace("2026-10-09", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))]);
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices
        {
            Checklists = checklists,
            GameState = () => state,
            HistoryState = () => HistoryState.Done,
            Commanders = new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }),
        }.Furnish(view);

        Assert.Equal(
            ["Checklist", "Missions", "Standing", "Statistics", "This session", "BGS work", "Combat stats", "Credits", "Commanders"],
            view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));

        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, CreditsPage.RootKey);
        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(In<CreditsPage>(view));

        var buttons = In<RadioButton>(view.FindControl<TextChoice>("ModeReadings")!).ToList();
        var lines = buttons.Select(button => button.TranslatePoint(default, window)!.Value.Y).Distinct().Count();
        var widths = string.Join(", ", buttons.Select(button => $"{button.Bounds.Width:0}"));

        Assert.True(lines == 1, $"The sub-tabs wrap onto {lines} lines; item widths {widths}.");

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("credits-big-panel.png");
        }

        window.Close();
    }
}
