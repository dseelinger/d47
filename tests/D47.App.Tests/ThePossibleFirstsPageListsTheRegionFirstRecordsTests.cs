using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Codex shows a Possible firsts row that opens a page of every region-first record (#2596).</summary>
[Trait("Category", "Integration")]
public class ThePossibleFirstsPageListsTheRegionFirstRecordsTests
{
    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse("# Table date: 2026-10-19\n101\tEntry 101\tBiology\tOrganic\t4:12\n");

    private static string Logged(string time, long id) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"" + Organic + "\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_18;\""
        + ",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"Latitude\":12.4512,\"Longitude\":-108.2231,\"IsNewEntry\":true}";

    private static readonly string[] ThreeFinds =
    [
        Logged("2026-10-02T10:00:00Z", 101),
        Logged("2026-10-04T10:00:00Z", 103),
        Logged("2026-10-05T10:00:00Z", 104),
    ];

    private static CommanderGameState State(params string[] codex) =>
        JournalScript.Commander("F735466", "John Deparagon", ["{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}", .. codex]);

    private static RegionFirstBook Book(CommanderGameState state, CodexTable? table = null)
    {
        var book = new RegionFirstBook("region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
        book.Observe("F735466", CodexLedger.None.With(state.CodexLedger), table ?? Table);

        return book;
    }

    private static PanelNavigator Nav()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, LedgersPage.Root);
        nav.Select(PanelTab.Commander);

        return nav;
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Search(CodexPossibleFirstsPage page, string query)
    {
        page.Filter(query);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Rows(Control page) =>
        [.. page.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Name?.StartsWith("CodexPossibleFirst.", StringComparison.Ordinal) == true)
            .Select(border => border.Name!)];

    private static string Runs(TextBlock block) =>
        string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? []).Trim();

    private static CodexPage Codex(CommanderGameState state, RegionFirstBook? book, PanelNavigator nav) =>
        new(() => state, () => HistoryState.Done, () => Now, new CodexChoice { Period = (int)CodexPeriod.Everything }, nav, regionFirsts: book);

    [AvaloniaFact]
    public void CodexShowsTheRowWithTheThreeCountsAndOpensThePage()
    {
        var state = State(ThreeFinds);
        var book = Book(state);
        var nav = Nav();

        var (window, page) = PageHost.Open(() => Codex(state, book, nav));
        var texts = Texts(page);

        Assert.Contains("POSSIBLE FIRSTS", texts.Select(text => text.ToUpperInvariant()));
        Assert.Contains("1 region · 2 galaxy · 3 not reported", texts);
        Assert.Contains("Entries you logged that the codex table did not list for their region, kept as they were judged. Whole record, not this trip.", texts);

        Press(Named(page, "CodexPossibleFirsts"));

        Assert.Equal(CodexPage.PossibleFirstsKey, nav.Trail[^1].Key);
        Assert.Equal("Possible firsts", nav.Trail[^1].Word);
        window.Close();
    }

    [AvaloniaFact]
    public void CodexShowsNoRowWhenThereIsNoRecord()
    {
        var state = State(ThreeFinds);
        var none = new RegionFirstBook("none.json", new MemoryFileSystem(), NullLogger.Instance, work => work());

        var (window, page) = PageHost.Open(() => Codex(state, none, Nav()));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == "CodexPossibleFirsts");
        window.Close();

        var (bare, barePage) = PageHost.Open(() => Codex(state, null, Nav()));
        Assert.DoesNotContain(barePage.GetVisualDescendants().OfType<Button>(), button => button.Name == "CodexPossibleFirsts");
        bare.Close();
    }

    [AvaloniaFact]
    public void ThePageListsEveryRecordNewestFirstWithItsColumns()
    {
        var state = State(ThreeFinds);
        var book = Book(state);

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => state, book, Nav()));
        var texts = Texts(page);

        Assert.Equal(["CodexPossibleFirst.104.18", "CodexPossibleFirst.103.18", "CodexPossibleFirst.101.18"], Rows(page));
        Assert.Contains("Each was judged once, against the codex table D47 had when it was recorded. The table may list some of them by now. 3 possible firsts · 3 not reported.", texts);
        Assert.Contains("POSSIBLE FIRST IN THE GALAXY", texts);
        Assert.Contains("POSSIBLE REGION FIRST", texts);
        Assert.Contains("NOT REPORTED", texts);
        Assert.Contains(CodexModel.RegionName(18), texts);
        Assert.Contains("2026-10-05 10:00", texts);
        window.Close();
    }

    [AvaloniaFact]
    public void ReportSortsTheNotReportedFirstAndAReportedOneShowsItsDate()
    {
        var state = State(ThreeFinds);
        var book = Book(state);
        book.MarkReported("F735466", 104, 18, DateTimeOffset.Parse("2026-10-20T08:30:00Z", CultureInfo.InvariantCulture));

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => state, book, Nav()));

        Assert.Equal("CodexPossibleFirst.104.18", Rows(page)[0]);
        Assert.Contains("✓ REPORTED 2026-10-20", page.GetVisualDescendants().OfType<TextBlock>().Select(Runs));

        Press(Named(page, "CodexPossibleFirstsHead.Report"));

        Assert.Equal("CodexPossibleFirst.104.18", Rows(page)[^1]);
        window.Close();
    }

    [AvaloniaFact]
    public void ThePressOnARowOpensTheEntryInItsRegion()
    {
        var state = State(ThreeFinds);
        var book = Book(state);
        var nav = Nav();

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => state, book, nav));
        Press(Named(page, "CodexPossibleFirst.Open.103.18"));

        Assert.Equal(CodexPage.EntryCrumb(18, "103", "Entry 103").Key, nav.Trail[^1].Key);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSearchKeepsTheRecordsThatMatch()
    {
        var state = State(ThreeFinds);
        var book = Book(state);

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => state, book, Nav()));

        Search(page, "Entry 103");
        Assert.Equal(["CodexPossibleFirst.103.18"], Rows(page));

        Search(page, "nothing like it");
        Assert.Empty(Rows(page));

        Search(page, "sol");
        Assert.Equal(3, Rows(page).Count);

        Search(page, CodexModel.RegionName(18));
        Assert.Equal(3, Rows(page).Count);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPagesTheTableAt25Rows()
    {
        var many = State([.. Enumerable.Range(1, 30).Select(day => Logged($"2026-10-{day:00}T10:00:00Z", 200 + day))]);
        var book = Book(many, CodexTable.Parse("# Table date: 2026-10-19\n"));

        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => many, book, Nav(), headset: true));

        Assert.Equal(25, Rows(page).Count);
        Press(Named(page, CodexPossibleFirstsPage.NextName));
        Assert.Equal(5, Rows(page).Count);
        window.Close();
    }
}
