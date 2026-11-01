using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Configuration;
using D47.Core.Logbook;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Logbook lists 20 written entries at a time and adds the next 20 as its scroller reaches the end (#1993).</summary>
public sealed class TheLogbookListsMoreEntriesAsItScrollsTests
{
    private const string Folder = "C:/logs";

    private static readonly DateTimeOffset Instant = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static (LogbookBook Book, MemoryFileSystem Files) Book(int entries)
    {
        var files = new MemoryFileSystem();

        for (var n = 1; n <= entries; n++)
        {
            files.WriteText(Path.Combine(Folder, $"log-{n:00}.md"), "x");
        }

        var book = new LogbookBook(
            new LogFolder(Folder, files, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(files, NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => new LogbookSettings(),
            () => [],
            () => Instant,
            () => new LogbookContext(),
            NullLogger<LogbookBook>.Instance);

        return (book, files);
    }

    private static Window Shown(LogbookPage page, double height)
    {
        var window = new Window { Content = page, Width = 720, Height = height };
        window.Show();
        Settle();

        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 6; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ScrollViewer Scroller(Control page) =>
        page.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "ModalBody");

    private static List<string> Names(Control page) =>
        [.. page.GetVisualDescendants().OfType<SelectableTextBlock>().Select(t => t.Text ?? string.Empty).Where(t => t.StartsWith("log-", StringComparison.OrdinalIgnoreCase)).Select(t => t.ToLowerInvariant())];

    private static bool Says(Control page, string text) =>
        page.GetVisualDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(text, StringComparison.Ordinal));

    private static void ScrollToEnd(Control page)
    {
        var scroll = Scroller(page);
        scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
        Settle();
    }

    [AvaloniaFact]
    public void TheListStartsWithTwentyAndScrollingToTheEndAddsTwentyMore()
    {
        var (book, _) = Book(55);
        var page = new LogbookPage(book);
        var window = Shown(page, 720);

        Assert.Equal(20, Names(page).Count);
        Assert.Equal("log-55.md", Names(page)[0]);
        Assert.True(Scroller(page).Extent.Height > Scroller(page).Viewport.Height);

        ScrollToEnd(page);
        Assert.Equal(40, Names(page).Count);

        ScrollToEnd(page);
        Assert.Equal(55, Names(page).Count);
        Assert.False(Says(page, "more in the folder"));

        window.Close();
    }

    [AvaloniaFact]
    public void ARoomyScrollerKeepsFillingWithoutAScroll()
    {
        var (book, _) = Book(55);
        var page = new LogbookPage(book);
        var window = Shown(page, 2200);
        var scroll = Scroller(page);

        Assert.True(Names(page).Count is > 20 and < 55, $"{Names(page).Count} shown");
        Assert.True(scroll.Extent.Height >= scroll.Viewport.Height);
        Assert.False(Says(page, "more in the folder"));

        window.Close();
    }

    [AvaloniaFact]
    public void ARefreshKeepsTheCountAndPutsANewEntryFirst()
    {
        var (book, files) = Book(55);
        var page = new LogbookPage(book);
        var window = Shown(page, 720);

        ScrollToEnd(page);
        Assert.Equal(40, Names(page).Count);

        files.WriteText(Path.Combine(Folder, "log-56.md"), "x");
        book.Disarm();
        Settle();

        var names = Names(page);
        Assert.Equal(41, names.Count);
        Assert.Equal("log-56.md", names[0]);

        window.Close();
    }
}
