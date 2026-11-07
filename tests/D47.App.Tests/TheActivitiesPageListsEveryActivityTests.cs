using Avalonia;
using Avalonia.Media;
using D47.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Storage;
using D47.Core;
using D47.Core.Activities;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Activities page under Commander › Statistics: every activity and its date, the undated ones first (#587).</summary>
public sealed class TheActivitiesPageListsEveryActivityTests
{
    private const string Fid = "F1";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string at, string kind, string extra = "")
    {
        Assert.True(JournalEvent.TryParse(
            $"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}",
            NullLogger.Instance,
            out var parsed));

        return parsed!;
    }

    private static ActivityLedger Ledger()
    {
        var ledger = new ActivityLedger(new MemoryFileSystem(), NullLogger.Instance);

        ledger.Apply(
        [
            Event("2026-04-01T08:00:00Z", "Commander", ",\"FID\":\"F1\",\"Name\":\"Jameson\""),
            Event("2026-09-23T10:00:00Z", "MiningRefined"),
            Event("2026-08-18T10:00:00Z", "SAAScanComplete"),
            Event("2025-02-02T10:00:00Z", "Bounty"),
            Event("2026-10-05T10:00:00Z", "MarketBuy"),
        ],
            Fid);

        return ledger;
    }

    private static ChecklistService Checklists()
    {
        var store = new GameStateStore();
        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-10-01T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            NullLogger.Instance,
            out var parsed));
        store.Apply(parsed!);

        var paths = new AppPaths(TestSurface.MemoryFolder("d47-activities-checklist"));

        return TestSurface.Checklists(paths.Data, () => store.Active);
    }

    private static Window Open(ActivityLedger ledger, double width)
    {
        var page = new ActivitiesPage(ledger, () => Fid, () => Now);
        var window = new Window { Content = page, Width = width, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IReadOnlyList<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains(ListRow.Class))];

    private static string RowName(Border row) =>
        row.GetVisualDescendants().OfType<TextBlock>().First().Text!;

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    [AvaloniaFact]
    public void EveryActivityHasARowUndatedFirstThenOldestFirst()
    {
        using var look = AppLook.Put();
        var window = Open(Ledger(), 1280);
        var rows = Rows(window);

        Assert.Equal(ActivityCatalogue.All.Count, rows.Count);

        var names = rows.Select(RowName).ToList();

        var undated = ActivityCatalogue.All.Count - 4;

        Assert.All(names.Take(undated), name => Assert.DoesNotContain(name, new[] { "BOUNTY HUNTING", "EXPLORATION", "MINING", "TRADING" }));
        Assert.Equal(["BOUNTY HUNTING", "EXPLORATION", "MINING", "TRADING"], names.Skip(undated));

        var words = window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.Contains("2 Feb 3311", words);
        Assert.Contains("yesterday", words);
        Assert.Contains("1 year 8 months ago", words);
        Assert.Contains("From your journals since 2 Feb 3311.", words);
        Assert.Contains("Not in your journals since 2 Feb 3311", words);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingLastDoneTwiceListsTheMostRecentFirst()
    {
        using var look = AppLook.Put();
        var window = Open(Ledger(), 1280);

        Press(window, "ActivitiesHead.Lastdone");
        Press(window, "ActivitiesHead.Lastdone");

        var names = Rows(window).Select(RowName).ToList();

        Assert.Equal(["TRADING", "MINING", "EXPLORATION", "BOUNTY HUNTING"], names.Take(4));
        Assert.Equal(ActivityCatalogue.All.Count, names.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void TheNarrowCombinedHeaderSortsByActivity()
    {
        using var look = AppLook.Put();
        var window = Open(Ledger(), 640);

        Press(window, "ActivitiesHead.Activity·Lastdone");

        Assert.Equal("BOUNTY HUNTING", RowName(Rows(window)[0]));
        Assert.Equal(ActivityCatalogue.All.Select(entry => entry.Name.ToUpperInvariant()).Order().First(), RowName(Rows(window)[0]));

        window.Close();
    }

    private static void Press(Window window, string name)
    {
        window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThePageDrawsAtTheDesignsWidthsAndTheNarrowRowIsTaller()
    {
        using var look = AppLook.Put();
        var wide = Open(Ledger(), 1280);
        var wideRow = Rows(wide)[0];

        Assert.True(wideRow.Bounds.Height >= 44);
        Save(wide, "activities-1280.png");
        wide.Close();

        var narrow = Open(Ledger(), 640);
        var narrowRow = Rows(narrow)[0];

        Assert.True(narrowRow.Bounds.Height >= 52);
        Save(narrow, "activities-640.png");
        narrow.Close();
    }

    [AvaloniaFact]
    public void EveryActivityNameIsWhiteAndNoRowHasACheckbox()
    {
        using var look = AppLook.Put();
        var window = Open(Ledger(), 1280);

        Assert.Empty(window.GetVisualDescendants().OfType<CheckBox>());

        Application.Current!.Resources.TryGetResource(ThemeManager.WhiteKey, null, out var white);

        foreach (var row in Rows(window))
        {
            var name = row.GetVisualDescendants().OfType<TextBlock>().First();

            Assert.Equal(((ISolidColorBrush)white!).Color, ((ISolidColorBrush)name.Foreground!).Color);
        }

        window.Close();
    }

    private static (Window Window, PanelView Panel) CommanderPanel(params string[] events)
    {
        var state = JournalScript.Timed(
            DateTimeOffset.UtcNow, Fid, "Jameson", [.. events.Select(json => (TimeSpan.Zero, json))]);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(Checklists());
        panel.EnableStatistics(() => state, Ledger());

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    [AvaloniaFact]
    public void TheChecklistHasNoActivitiesButton()
    {
        using var look = AppLook.Put();
        var (window, panel) = CommanderPanel();

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            found => found.Content as string == "Activities");

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheStatisticsSidebarListsActivitiesAndDrawsThePage(bool statisticsKnown)
    {
        using var look = AppLook.Put();
        var (window, panel) = CommanderPanel(
            statisticsKnown ? ["""{"event":"Statistics","Bank_Account":{"Current_Wealth":1000}}"""] : []);

        panel.Nav.SelectRoot(PanelTab.Commander, StatisticsPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<StatisticsPage>().Single();
        var sidebar = page.GetVisualDescendants().OfType<Sidebar>().Single();

        Assert.Contains(
            sidebar.Items,
            row => row.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "ACTIVITIES"));

        page.Open(StatisticsPage.ActivitiesKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ActivityCatalogue.All.Count,
            panel.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains(ListRow.Class)));

        Save(window, $"activities-in-statistics-{(statisticsKnown ? "known" : "unknown")}.png");
        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAGameSectionAfterActivitiesLeavesTheActivitiesPage()
    {
        using var look = AppLook.Put();
        var (window, panel) = CommanderPanel("""{"event":"Statistics","Bank_Account":{"Current_Wealth":1000},"Combat":{"Bounties_Claimed":3}}""");

        panel.Nav.SelectRoot(PanelTab.Commander, StatisticsPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        panel.GetVisualDescendants().OfType<StatisticsPage>().Single(found => found.IsEffectivelyVisible).Open(StatisticsPage.ActivitiesKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StatisticsPage.ActivitiesKey, panel.Nav.DestinationKey);

        panel.GetVisualDescendants().OfType<StatisticsPage>().Single(found => found.IsEffectivelyVisible).Open("Combat");
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<StatisticsPage>().Single(found => found.IsEffectivelyVisible);

        Assert.Equal(StatisticsPage.RootKey, panel.Nav.DestinationKey);
        Assert.Equal("Combat", page.Section);
        Assert.Equal(0, panel.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains(ListRow.Class)));

        window.Close();
    }
}
