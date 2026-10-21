using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Discoveries row on a region's Codex page and the page it opens: what the codex table lists that the record lacks.</summary>
public class TheCodexDiscoveriesPageListsWhatIsMissingTests
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

    private static CodexRegionPage Region(CommanderGameState state, int? id = 18, HistoryState history = HistoryState.Done, PanelNavigator? nav = null) =>
        new(() => state, () => history, () => Now, new CodexChoice(), id, false, nav, Table);

    private static CodexDiscoveriesPage Discoveries(CommanderGameState state, int? id = 18, HistoryState history = HistoryState.Done) =>
        new(() => state, () => history, id, Table);

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static string[] Cells(Control root, string name) =>
        [.. In<TextBlock>(In<Control>(root).First(control => control.Name == name)).Select(block => block.Text ?? string.Empty)];

    private static Button Row(Control root) => In<Button>(root).First(button => button.Name == "CodexDiscoveries");

    private static ItemsControl Rows(Control root) => In<ItemsControl>(root).First(list => list.Name == "CodexMissingRows");

    [AvaloniaFact]
    public void TheRegionPageDrawsADiscoveriesRowWhoseFiguresIgnoreThePeriod()
    {
        var (window, page) = Open(() => Region(State(Lines)));

        Assert.True(Shows(page, CodexModel.DiscoveriesRowLine));
        Assert.True(Shows(page, "3 missing · 1 of 4 on record"));
        Assert.True(Shows(Row(page), "›"));
        Assert.True(Row(page).IsEnabled);
        Assert.True(Row(page).Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheRowPutsDiscoveriesUnderTheRegion()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb(CodexPage.PageKey, "Codex"));
        nav.Select(PanelTab.Commander);
        nav.Drill(CodexPage.RegionCrumb(18));

        var (window, page) = Open(() => Region(State(Lines), nav: nav));

        Row(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Discoveries", nav.Trail[^1].Word);
        Assert.Equal("Inner Orion Spur", nav.Trail[^2].Word);
        Assert.True(CodexPage.IsDiscoveries(nav.Trail[^1].Key));
        Assert.False(CodexPage.IsRegion(nav.Trail[^1].Key));
        Assert.False(CodexPage.IsDiscoveries(nav.Trail[^2].Key));
        Assert.Equal(18, CodexPage.DiscoveriesOf(nav.Trail[^1].Key));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowShowsUnderTheEmptyMessageForARegionWithNoEntriesInThePeriod()
    {
        var (window, page) = Open(() => Region(State(Lines), id: 35));

        Assert.True(Shows(page, "1 missing · 0 of 1 on record"));
        Assert.True(Row(page).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowIsDisabledForTheRegionD47CannotName()
    {
        var (window, page) = Open(() => Region(State(Lines), id: null));

        Assert.False(Row(page).IsEnabled);
        Assert.True(Shows(Row(page), CodexModel.DiscoveriesNoRegionLine));
        Assert.False(Shows(Row(page), "›"));
        Assert.DoesNotContain(In<TextBlock>(Row(page)), block => block.Text?.Contains("missing", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void ARunningWalkDrawsOnlyTheRunningLine()
    {
        var (window, page) = Open(() => Region(State(Lines), history: HistoryState.Running));

        Assert.DoesNotContain(In<Button>(page), button => button.Name == "CodexDiscoveries");

        var (other, discoveries) = Open(() => Discoveries(State(Lines), history: HistoryState.Running));

        Assert.True(Shows(discoveries, CodexModel.RunningText));
        Assert.DoesNotContain(In<Border>(discoveries), border => border.Name?.StartsWith("CodexStrip.", StringComparison.Ordinal) == true);

        window.Close();
        other.Close();
    }

    [AvaloniaFact]
    public void ThePageDrawsTheLineGroupHeadStripNoteHeaderAndGroupedRows()
    {
        var (window, page) = Open(() => Discoveries(State(Lines)));

        Assert.True(Shows(page, CodexModel.DiscoveriesLine));
        Assert.True(Shows(page, CodexModel.DiscoveriesGroupLine));
        Assert.True(Shows(page, "Discoveries · Inner Orion Spur"));
        Assert.True(Shows(page, "3 missing"));
        Assert.Equal(["KNOWN", "4", "in the codex table"], Cells(page, "CodexStrip.Known"));
        Assert.Equal(["ON RECORD", "1", "logged at any time"], Cells(page, "CodexStrip.On record"));
        Assert.Equal(["MISSING", "3", "not on your record"], Cells(page, "CodexStrip.Missing"));
        Assert.Equal(["SUBCATEGORIES", "2", "with missing entries"], Cells(page, "CodexStrip.Subcategories"));
        Assert.True(Shows(
            page,
            "An entry logged before 2026-10-01 reads as missing. " + CodexDiscoveries.Coverage
            + " You logged 1 more here that the codex table does not list."));
        Assert.NotNull(In<Control>(page).FirstOrDefault(control => control.Name == "CodexMissingHead.Entry"));
        Assert.NotNull(In<Control>(page).FirstOrDefault(control => control.Name == "CodexMissingHead.Reportedin"));
        Assert.NotNull(In<Control>(page).FirstOrDefault(control => control.Name == "CodexMissingHead.Status"));

        var drawn = In<TextBlock>(Rows(page)).Select(block => block.Text ?? string.Empty).ToList();

        Assert.Equal(
            ["Alpha tree", "Gamma tree", "Delta vent"],
            drawn.Where(text => text is "Alpha tree" or "Beta tree" or "Gamma tree" or "Delta vent"));
        Assert.Contains("2 of 3 missing", drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("1 of 1 missing", drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3, drawn.Count(text => text == "MISSING"));

        var rows = In<Border>(Rows(page)).Where(border => border.MinHeight >= 44 && border.BorderThickness.Bottom == 1).ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Null(row.Background));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("codex-discoveries.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedWalkDrawsTheWarningAndTheTimeOfTheRecordsStart()
    {
        var (window, page) = Open(() => Discoveries(State(Lines), history: HistoryState.Failed));

        Assert.Single(In<Notice>(page));
        Assert.Contains(
            In<TextBlock>(page),
            block => block.Inlines?.OfType<Run>().Any(run => run.Text == "2026-10-01 09:00") == true);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchNarrowsTheRowsAndTheCountAndAMissReadsAsNoMatch()
    {
        var (window, page) = Open(() => Discoveries(State(Lines)));

        page.Filter("delta");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "1 of 3 missing"));
        Assert.True(Shows(Rows(page), "Delta vent"));
        Assert.False(Shows(Rows(page), "Alpha tree"));

        page.Filter("zzz");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, CodexModel.NoMissingMatchText));
        Assert.DoesNotContain(In<Control>(page), control => control.Name?.StartsWith("CodexMissingHead", StringComparison.Ordinal) == true);
        Assert.False(Rows(page).IsVisible && Rows(page).ItemsSource!.Cast<object>().Any());

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensDiscoveriesUnderTheRegion()
    {
        using var look = AppLook.Put();

        var root = TestSurface.MemoryFolder("d47-codex-discoveries-tests");
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
        Assert.True(view.Nav.Drill(CodexPage.RegionCrumb(18)));
        Assert.True(view.Nav.Drill(CodexPage.DiscoveriesCrumb(18)));

        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Codex", "Inner Orion Spur", "Discoveries"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Single(In<CodexDiscoveriesPage>(view));
        Assert.Empty(In<CodexRegionPage>(view));

        window.Close();
    }
}
