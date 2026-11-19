using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A codex entry with a region-first record shows the band and reads its kind where NEW would be (#2595).</summary>
[Trait("Category", "Integration")]
public class APossibleFirstIsMarkedOnTheCodexPagesTests
{
    private const string Organic = "$Codex_SubCategory_Organic_Structures;";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-07T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly CodexTable Table = CodexTable.Parse("# Table date: 2026-10-19\n101\tEntry 101\tBiology\tOrganic\t4:12\n");

    private static string Codex(string time, long id, bool isNew) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"" + Organic + "\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_18;\""
        + ",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5,\"Latitude\":12.4512,\"Longitude\":-108.2231"
        + (isNew ? ",\"IsNewEntry\":true" : string.Empty) + "}";

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-01T09:00:00Z\",\"event\":\"LoadGame\"}",
        Codex("2026-10-02T10:00:00Z", 101, true),
        Codex("2026-10-04T10:00:00Z", 103, true),
        Codex("2026-10-05T10:00:00Z", 104, true),
    ];

    private static CommanderGameState State() => JournalScript.Commander("F735466", "John Deparagon", Lines);

    private static RegionFirstBook Book(CommanderGameState state)
    {
        var book = new RegionFirstBook("region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
        book.Observe("F735466", CodexLedger.None.With(state.CodexLedger), Table);

        return book;
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void ARegionFirstShowsTheBandWithItsSentenceAndTableDate()
    {
        var state = State();
        var book = Book(state);

        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => state, 101, 18, "Entry 101", null, regionFirsts: book));
        var texts = Texts(page);

        Assert.NotNull(page.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "CodexRegionFirst"));
        Assert.Contains("POSSIBLE REGION FIRST", texts);
        Assert.Contains("You may be the first Commander to log Entry 101 in " + CodexModel.RegionName(18) + ".", texts);
        Assert.Contains("table 2026-10-19 · not reported", texts);
        Assert.DoesNotContain("NEW", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void AGalaxyFirstSaysAnywhereInTheGalaxy()
    {
        var state = State();
        var book = Book(state);

        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => state, 103, 18, "Entry 103", null, regionFirsts: book));
        var texts = Texts(page);

        Assert.Contains("POSSIBLE FIRST IN THE GALAXY", texts);
        Assert.Contains("You may be the first Commander to log Entry 103 anywhere in the galaxy.", texts);

        window.Close();
    }

    [AvaloniaFact]
    public void AReportedRecordSaysWhenAndAnEntryWithNoRecordShowsNoBand()
    {
        var state = State();
        var book = Book(state);
        book.MarkReported("F735466", 101, 18, DateTimeOffset.Parse("2026-10-20T08:30:00Z", CultureInfo.InvariantCulture));

        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => state, 101, 18, "Entry 101", null, regionFirsts: book));
        Assert.Contains("table 2026-10-19 · reported 2026-10-20 08:30 UTC", Texts(page));
        window.Close();

        var none = new RegionFirstBook("none.json", new MemoryFileSystem(), NullLogger.Instance, work => work());
        var (bare, barePage) = PageHost.Open(() => new CodexEntryPage(() => state, 101, 18, "Entry 101", null, regionFirsts: none));

        Assert.Null(barePage.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "CodexRegionFirst"));
        Assert.Contains("NEW", Texts(barePage));
        bare.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetBigPanelDrawsTheBandToo()
    {
        var state = State();
        var book = Book(state);

        var (window, page) = PageHost.Open(() => new CodexEntryPage(() => state, 101, 18, "Entry 101", null, headset: true, regionFirsts: book));

        Assert.Contains("POSSIBLE REGION FIRST", Texts(page));
        window.Close();
    }

    [AvaloniaFact]
    public void TheRegionPageStatusCellReadsTheKindInPlaceOfNew()
    {
        var state = State();
        var book = Book(state);
        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };

        var (window, page) = PageHost.Open(() => new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, choice, 18, regionFirsts: book));
        var texts = Texts(page);

        Assert.Contains("POSSIBLE REGION FIRST", texts);
        Assert.Contains("POSSIBLE FIRST IN THE GALAXY", texts);
        Assert.Single(texts, "NEW"); // the summary strip's label; no row reads NEW

        window.Close();
    }
}
