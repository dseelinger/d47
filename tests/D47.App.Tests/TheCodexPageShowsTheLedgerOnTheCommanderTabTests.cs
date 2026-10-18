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
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › Codex as drawn: the regions table, a region's page, the history states and the period they share (#1290).</summary>
public class TheCodexPageShowsTheLedgerOnTheCommanderTabTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static string Codex(string time, long id, int region, string sub, string system, bool isNew) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"$Codex_SubCategory_X;\",\"SubCategory_Localised\":\"" + sub + "\",\"Region\":\"$Codex_RegionName_" + region
        + ";\",\"System\":\"" + system + "\",\"SystemAddress\":1,\"BodyID\":5,\"Latitude\":12.4512,\"Longitude\":-108.2231"
        + (isNew ? ",\"IsNewEntry\":true" : string.Empty) + "}";

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        Codex("2026-10-02T10:00:00Z", 101, 18, "Organic structures", "Sol", true),
        "{\"timestamp\":\"2026-10-03T10:00:00Z\",\"event\":\"SellExplorationData\"}",
        Codex("2026-10-04T10:00:00Z", 102, 18, "Stars", "Alpha", true),
        Codex("2026-10-05T10:00:00Z", 101, 18, "Organic structures", "Sol", false),
        Codex("2026-10-06T10:00:00Z", 104, 35, "Geology", "Beta", true),
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

    private static CodexPage Root(CommanderGameState state, HistoryState history, CodexChoice? choice = null, PanelNavigator? nav = null, bool headset = false) =>
        new(() => state, () => history, () => Now, choice ?? new CodexChoice(), nav ?? new PanelNavigator(), headset);

    private static CodexRegionPage Region(CommanderGameState state, int? id = 18, CodexChoice? choice = null, HistoryState history = HistoryState.Done, bool headset = false) =>
        new(() => state, () => history, () => Now, choice ?? new CodexChoice(), id, headset);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static string[] Cells(Control root, string name) =>
        [.. In<TextBlock>(In<Control>(root).First(control => control.Name == name)).Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheRootDrawsThePeriodTheStripAndOneRowPerRegion()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done));

        Assert.Equal(CodexPage.SummaryText, page.Summary);
        Assert.Single(In<Segment>(page));
        Assert.Equal(["ENTRIES", "3", "codex scans"], Cells(page, "CodexStrip.Entries"));
        Assert.Equal(["NEW", "2", "new to your codex"], Cells(page, "CodexStrip.New"));
        Assert.Equal(["LOGGED", "3", "distinct entries"], Cells(page, "CodexStrip.Logged"));
        Assert.Equal(["REGIONS", "2", "with entries"], Cells(page, "CodexStrip.Regions"));
        Assert.True(Shows(page, "Inner Orion Spur"));
        Assert.True(Shows(page, "Organic structures 1 · Stars 1"));
        Assert.True(Shows(page, "Since you sold exploration data on 3 October at 10:00 UTC."));
        Assert.True(Shows(page, "2 REGIONS"));
        Assert.True(page.Filters);
        Assert.Empty(In<Notice>(page));
        Assert.All(
            In<Button>(page).Where(button => button.Name?.StartsWith("CodexRegion.", StringComparison.Ordinal) == true),
            button => Assert.True(button.Bounds.Height >= 44));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-by-region.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void PressingARegionDrillsToItsPage()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);

        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done, nav: nav));

        In<Button>(page).First(button => button.Name == "CodexRegion.18").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("codex-region-18", nav.Trail[^1].Key);
        Assert.Equal("Inner Orion Spur", nav.Trail[^1].Word);
        Assert.True(CodexPage.IsRegion(nav.Trail[^1].Key));
        Assert.Equal(18, CodexPage.RegionOf(nav.Trail[^1].Key));
        Assert.Null(CodexPage.RegionOf(CodexPage.RegionCrumb(null).Key));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRegionPageDrawsGroupsRowsAndStatus()
    {
        var (window, page) = Open(() => Region(State(Lines)));

        Assert.Equal(CodexPage.SummaryText, page.Summary);
        Assert.True(Shows(page, "Biological and Geological · Organic structures".ToUpperInvariant()) || Shows(page, "Biological and Geological · Organic structures"));
        Assert.True(Shows(page, "NEW"));
        Assert.True(Shows(page, "2026-10-04 10:00"));
        Assert.True(Shows(page, "ALREADY LOGGED"));
        Assert.True(Shows(page, "2026-10-05 10:00"));
        Assert.True(Shows(page, "2 ENTRIES"));
        Assert.Equal(["SUBCATEGORIES", "2", "with entries"], Cells(page, "CodexStrip.Subcategories"));
        Assert.True(page.Filters);

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-region.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAHeadingSortsTheRowsWithinEachGroupAndTheGroupsKeepTheirOrder()
    {
        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };
        var (window, page) = Open(() => Region(State(Lines), 18, choice));

        IReadOnlyList<object> Items() => (IReadOnlyList<object>)In<ItemsControl>(page).First(list => list.Name == "CodexRows").ItemsSource!;

        string[] Shape() => [.. Items().Select(item => item is CodexGroup group ? group.Label : ((CodexEntryRow)item).At.ToString("MM-dd", CultureInfo.InvariantCulture))];

        Assert.Equal(["Biological and Geological · Organic structures", "10-05", "10-02", "Biological and Geological · Stars", "10-04"], Shape());

        void Press(string name)
        {
            In<Button>(page).First(button => button.Name == name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        Press("CodexEntryHead.Time");
        Press("CodexEntryHead.Time");

        Assert.Equal(["Biological and Geological · Organic structures", "10-02", "10-05", "Biological and Geological · Stars", "10-04"], Shape());

        window.Close();
    }

    [AvaloniaFact]
    public void ThePeriodChosenOnOnePageCarriesToTheOther()
    {
        var choice = new CodexChoice();
        var state = State(Lines);
        var (window, panel) = Open(() => new StackPanel
        {
            Children = { Root(state, HistoryState.Done, choice), Region(state, 18, choice) },
        });
        var root = panel.Children.OfType<CodexPage>().Single();
        var region = panel.Children.OfType<CodexRegionPage>().Single();

        In<RadioButton>(In<Segment>(region).First()).ElementAt(2).IsChecked = true;
        Assert.Equal(2, choice.Period);

        Assert.True(root.Tick());
        Assert.Equal(2, In<Segment>(root).First().SelectedIndex);
        Assert.True(Shows(root, "Everything on record, since 2026-10-01."));

        window.Close();
    }

    [AvaloniaFact]
    public void RunningHistoryShowsOnlyItsLineWithNoControlsAndNoSearch()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Running));

        Assert.True(Shows(page, CodexModel.RunningText));
        Assert.Empty(In<Segment>(page));
        Assert.Empty(In<Button>(page));
        Assert.False(page.Filters);

        var region = Region(State(Lines), 18, null, HistoryState.Running);
        window.Content = region;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(region, CodexModel.RunningText));
        Assert.False(region.Filters);

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedWalkShowsTheWarningAboveTheLiveRecord()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Failed));

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(CodexModel.FailedLabel, notice.Label);
        Assert.Contains("at 09:00 UTC today", notice.Text);
        Assert.True(Shows(page, "Inner Orion Spur"));
        Assert.Contains(In<TextBlock>(page).SelectMany(block => block.Inlines ?? []).OfType<Avalonia.Controls.Documents.Run>(), run => run.Text == "2026-10-01 09:00");

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchNarrowsTheCountAndWithNoMatchSaysSo()
    {
        var (window, page) = Open(() => Root(State(Lines), HistoryState.Done));

        page.Filter("beta");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "1 OF 2 REGIONS"));

        page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, CodexModel.NoRegionMatchText));
        Assert.True(page.Filters);

        window.Close();
    }

    [AvaloniaFact]
    public void ANothingPeriodSaysSoWithNoStripAndNoSortHeader()
    {
        var (window, page) = Open(() => Root(State(Lines[0], Lines[1], Lines[2]), HistoryState.Done));

        Assert.True(Shows(page, "No codex entries since you sold exploration data on 3 October."));
        Assert.DoesNotContain(In<Control>(page), control => control.Name?.StartsWith("CodexStrip.", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(In<Button>(page), button => button.Name?.StartsWith("CodexHead", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRegionListIsVirtualized()
    {
        var lines = new List<string> { Lines[0] };
        var at = DateTimeOffset.Parse("2026-10-06T00:00:00Z", CultureInfo.InvariantCulture);

        for (var i = 0; i < 3000; i++)
        {
            lines.Add(Codex(at.AddSeconds(i).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture), 1000 + i, 18, "Stars", "Sol", false));
        }

        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };
        var (window, page) = Open(() => Region(State([.. lines]), 18, choice));

        var drawn = In<Border>(In<ItemsControl>(page).First(list => list.Name == "CodexRows")).Count(border => border.MinHeight >= 44);

        Assert.InRange(drawn, 1, 100);
        Assert.True(Shows(page, "3,000 ENTRIES"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensCodexUnderLedgersWithItsRegionBelowIt()
    {
        using var look = AppLook.Put();

        var root = TestSurface.MemoryFolder("d47-codex-page-tests");
        var checklists = new D47.Core.Checklists.ChecklistService(
            new D47.Core.Checklists.ChecklistStore(Path.Combine(root, "checklist.json"), new D47.Core.Storage.MemoryFileSystem(), NullLogger<D47.Core.Checklists.ChecklistStore>.Instance),
            new D47.Core.Checklists.ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new D47.Core.Storage.MemoryFileSystem(),
                NullLogger<D47.Core.Checklists.ChecklistProposalStore>.Instance),
            () => null);

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
        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CodexPage>(view));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-big-panel.png");
        }

        In<Button>(view).First(button => button.Name == "CodexRegion.18").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex", "Inner Orion Spur"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CodexRegionPage>(view));
        Assert.Empty(In<CodexPage>(view));

        window.Close();
    }
}
