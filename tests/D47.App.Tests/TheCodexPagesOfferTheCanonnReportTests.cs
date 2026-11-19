using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A REPORT TO CANONN tile sits on the entry band and the Possible firsts row, and calls the protected tool (#2597).</summary>
[Trait("Category", "Integration")]
public class TheCodexPagesOfferTheCanonnReportTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T08:30:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse("# Table date: 2026-10-19\n101\tEntry 101\tBiology\tOrganic\t4:12\n");

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        "{\"timestamp\":\"2026-10-02T09:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":1,\"StarPos\":[0.0,0.0,0.0]}",
        "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name\":\"$Codex_Ent_101;\",\"Name_Localised\":\"Entry 101\","
        + "\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\","
        + "\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,"
        + "\"Latitude\":12.4512,\"Longitude\":-108.2231,\"IsNewEntry\":true}",
    ];

    private sealed class Sender(bool reachable) : IRegionFirstReport
    {
        public Task<bool> SendAsync(RegionFirstReport report, CancellationToken cancellationToken) => Task.FromResult(reachable);
    }

    private sealed class Desk
    {
        public Desk(bool reachable)
        {
            State = JournalScript.Commander("F735466", "John Deparagon", Lines);
            Book = new RegionFirstBook("region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
            Book.Observe("F735466", CodexLedger.None.With(State.CodexLedger), Table);
            Reporting = new RegionFirstReporting(() => State, Book, new Sender(reachable), "test", () => Now);
            Press = new RegionFirstPress(
                async (name, arguments, cancellation) =>
                {
                    Calls.Add((name, arguments.Int32("entry_id"), arguments.Int32("region_id")));

                    return await Reporting.ReportAsync(arguments, cancellation);
                },
                Book,
                () => State);
        }

        public CommanderGameState State { get; }

        public RegionFirstBook Book { get; }

        public RegionFirstReporting Reporting { get; }

        public RegionFirstPress Press { get; }

        public List<(string Tool, int? Entry, int? Region)> Calls { get; } = [];

        public RegionFirstRecord Record => Book.Find("F735466", 101, 18)!;
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button? Tile(Control root) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "CodexReport");

    private static async Task Press(RegionFirstPress press, RegionFirstRecord record, Control page)
    {
        await press.PressAsync(record);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        (page as CodexEntryPage)?.Tick();
        (page as CodexPossibleFirstsPage)?.Tick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AnUnreportedRecordShowsTheTileOnTheBandAndTheRow()
    {
        var desk = new Desk(reachable: true);

        var (entry, entryPage) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book, press: desk.Press));
        Assert.Equal("Report to Canonn", Tile(entryPage)?.Content);
        Assert.Equal(D47.App.Theming.TypeScale.MinimumTarget, Tile(entryPage)!.MinHeight);
        entry.Close();

        var (list, listPage) = PageHost.Open(() => new CodexPossibleFirstsPage(() => desk.State, desk.Book, press: desk.Press, headset: true));
        Assert.Equal("Report to Canonn", Tile(listPage)?.Content);
        Assert.Contains("NOT REPORTED", Texts(listPage));
        list.Close();
    }

    [AvaloniaFact]
    public void NoTileIsOfferedWithoutAPressOrOnAReportedRecord()
    {
        var desk = new Desk(reachable: true);

        var (bare, barePage) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book));
        Assert.Null(Tile(barePage));
        bare.Close();

        desk.Book.MarkReported("F735466", 101, 18, Now);

        var (entry, entryPage) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book, press: desk.Press));
        Assert.Null(Tile(entryPage));
        entry.Close();

        var (list, listPage) = PageHost.Open(() => new CodexPossibleFirstsPage(() => desk.State, desk.Book, press: desk.Press));
        Assert.Null(Tile(listPage));
        Assert.Contains(listPage.GetVisualDescendants().OfType<TextBlock>(), block => block.Inlines?.Any(inline => (inline as Avalonia.Controls.Documents.Run)?.Text == "✓ REPORTED ") == true);
        list.Close();
    }

    [AvaloniaFact]
    public async Task APressCallsTheToolWithTheRecordAndReadsSentFromTheStore()
    {
        var desk = new Desk(reachable: true);
        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book, press: desk.Press));

        await Press(desk.Press, desk.Record, page);

        Assert.Equal([(RegionFirstReporting.Tool, (int?)101, (int?)18)], desk.Calls);
        Assert.NotNull(desk.Record.Reported);
        Assert.Null(Tile(page));
        Assert.Contains(RegionFirstPress.SentText, Texts(page));
        Assert.Contains(Texts(page), text => text.Contains("reported 2026-10-20 08:30 UTC", StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaFact]
    public async Task AFailedPressDrawsTheNoticeAndRetryPressesAgain()
    {
        var desk = new Desk(reachable: false);
        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book, press: desk.Press));

        await Press(desk.Press, desk.Record, page);

        Assert.Null(desk.Record.Reported);
        Assert.Contains("REPORT NOT SENT", Texts(page));
        Assert.Contains(RegionFirstReporting.Unreachable, Texts(page));
        Assert.Equal("Retry", Tile(page)?.Content);

        window.Close();

        var again = new Desk(reachable: true);
        var (retry, retryPage) = PageHost.Open(() => new CodexPossibleFirstsPage(() => again.State, again.Book, press: again.Press));
        await Press(again.Press, again.Record, retryPage);
        Assert.NotNull(again.Record.Reported);
        Assert.Null(Tile(retryPage));
        retry.Close();
    }

    [AvaloniaFact]
    public async Task ARowShowsAnInlineNoticeBesideTheRetryTile()
    {
        var desk = new Desk(reachable: false);
        var (window, page) = PageHost.Open(() => new CodexPossibleFirstsPage(() => desk.State, desk.Book, press: desk.Press));

        await Press(desk.Press, desk.Record, page);

        Assert.Contains("REPORT NOT SENT", Texts(page));
        Assert.Equal("Retry", Tile(page)?.Content);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheTileIsDisabledWhileThePressRuns()
    {
        var desk = new Desk(reachable: true);
        var gate = new TaskCompletionSource();
        var press = new RegionFirstPress(
            async (name, arguments, cancellation) =>
            {
                await gate.Task;

                return await desk.Reporting.ReportAsync(arguments, cancellation);
            },
            desk.Book,
            () => desk.State);
        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => desk.State, 101, 18, "Entry 101", null, regionFirsts: desk.Book, press: press));

        var running = press.PressAsync(desk.Record);
        page.Tick();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(Tile(page)!.IsEnabled);

        gate.SetResult();
        await running;
        page.Tick();
        Assert.Null(Tile(page));
        window.Close();
    }
}
