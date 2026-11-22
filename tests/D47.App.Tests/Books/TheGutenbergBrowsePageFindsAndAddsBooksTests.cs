using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>Media › Books › Add a book › Project Gutenberg browses the catalogue on this PC and adds a book from it (#2682).</summary>
public sealed class TheGutenbergBrowsePageFindsAndAddsBooksTests
{
    private const string Folder = @"C:\d47\data\books";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly string[] Rows =
    [
        "84\tFrankenstein; Or, The Modern Prometheus\tShelley, Mary Wollstonecraft\ten\tScience fiction; Gothic fiction\t90000\t1993-10-01",
        "1342\tPride and Prejudice\tAusten, Jane\ten\t\t80000\t1998-06-01",
        "1661\tThe Adventures of Sherlock Holmes\tDoyle, Arthur Conan\ten\tMystery; Adventure\t70000\t1999-03-01",
        "36\tThe War of the Worlds\tWells, H. G.\ten\tScience fiction\t60000\t2004-10-03",
        "35\tThe Time Machine\tWells, H. G.\ten\tScience fiction\t50000\t2004-10-02",
        $"{GutenbergText.Number}\tThe Night Watch\tWellstone, H. G.\ten\tHorror\t40000\t2026-01-01",
        "2000\tDon Quijote\tCervantes Saavedra, Miguel de\tes\tAdventure\t10000\t1999-12-01",
    ];

    private readonly FakeGutenbergMirror _mirror = new();

    private int _fetches;

    private static byte[] Catalogue()
    {
        using var buffer = new MemoryStream();

        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(GutenbergCatalogue.Header + "\n" + string.Join("\n", Rows) + "\n"));
        }

        return buffer.ToArray();
    }

    private (Window Window, PanelView Panel, BookRig Rig) Open(bool headset = false, bool reachable = true)
    {
        var rig = BookRig.OnShelf(new MemoryFileSystem(), Folder, mirror: _mirror);
        var store = new GutenbergCatalogueStore(
            Path.Combine(Folder, "gutenberg"),
            rig.Files,
            () => new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero),
            NullLogger.Instance,
            _ =>
            {
                _fetches++;
                return Task.FromResult(reachable ? Catalogue() : null);
            });
        var host = new BooksHost(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => true,
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            rig.BookFiles,
            store,
            rig.Downloads);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(books: host);

        var window = new Window { Content = panel, Width = headset ? 1024 : 1280, Height = headset ? 640 : 1000 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel, rig);
    }

    private static BookAddPage? Page(Control root) => root.GetVisualDescendants().OfType<BookAddPage>().FirstOrDefault();

    /// <summary>Opens Add a book and waits until it shows the catalogue's count or the Notice.</summary>
    private static async Task OpenAddPage(PanelView panel)
    {
        Click(Find<Button>(panel, BooksPage.AddName));
        await Until(() => Count(panel) is not null || Find<Notice>(panel, BookAddPage.UnreachableName).IsVisible);
    }

    /// <summary>Runs the UI's jobs, letting the page's searches finish off the UI thread, until <paramref name="done"/>.</summary>
    private static async Task Until(Func<bool> done)
    {
        var stop = DateTime.UtcNow + Patience;

        while (true)
        {
            Dispatcher.UIThread.RunJobs();

            if (done())
            {
                return;
            }

            Assert.True(DateTime.UtcNow < stop, "The page did not reach the expected state.");
            await Task.Delay(10);
        }
    }

    private static T Find<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Button> BookRows(Control root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Where(button => button.Name == BookAddPage.GutenbergRowName && button.IsEffectivelyVisible)];

    private static List<int> Listed(Control root) => [.. BookRows(root).Select(row => (int)row.Tag!)];

    private static Button Row(Control root, int id) => BookRows(root).First(row => (int)row.Tag! == id);

    private static List<string> Drawn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    /// <summary>The "N of M books" line.</summary>
    private static string? Count(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text)) : null)
            .FirstOrDefault(text => text is not null && text.EndsWith(" books", StringComparison.Ordinal));

    private static string Token(Control root, string kind) =>
        Find<Button>(root, "GutenbergToken." + kind).GetVisualDescendants().OfType<TextBlock>().First().Text!;

    private static Button ClearFilters(Control root) => Find<Button>(root, GutenbergFilters.ClearName);

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheListShowsEnglishBooksByPopularityWithABookOnTheShelfMarked(bool headset)
    {
        var (window, panel, rig) = Open(headset);
        rig.Shelf.AddDownload(BookSource.FromGutenberg(36), "The War of the Worlds", "Wells, H. G.");

        await OpenAddPage(panel);

        Assert.Equal(0, Find<Segment>(panel, BookAddPage.SourceName).SelectedIndex);
        Assert.Equal([84, 1342, 1661, 36, 35, GutenbergText.Number], Listed(panel));
        Assert.Equal("6 of 6 books", Count(panel));
        Assert.Equal("POPULAR", Token(panel, "Order"));
        Assert.Equal("ANY SUBJECT", Token(panel, "Subject"));
        Assert.Equal("ENGLISH", Token(panel, "Language"));
        Assert.False(ClearFilters(panel).IsVisible);
        Assert.Contains(TokenFilterBar<GutenbergFilters.Kind>.TokenHint, Drawn(panel));
        Assert.Contains("#84", Drawn(panel));

        if (!headset)
        {
            Assert.Contains("SHELLEY, MARY WOLLSTONECRAFT · SCIENCE FICTION, GOTHIC FICTION", Drawn(panel));
        }

        var shelved = Row(panel, 36);
        Assert.False(shelved.IsEnabled);
        Assert.Contains(BookAddPage.OnYourShelf, shelved.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        window.SaveFrame(headset ? "gutenberg-browse-headset.png" : "gutenberg-browse-desktop.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task TextSubjectAndLanguageNarrowTheListAndClearFiltersRestoresTheDefaults()
    {
        var (window, panel, _) = Open();
        await OpenAddPage(panel);

        Find<TextBox>(panel, GutenbergFilters.TextName).Text = "wells";
        await Until(() => Count(panel) == "3 of 3 books");

        Assert.Equal([36, 35, GutenbergText.Number], Listed(panel));
        Assert.Equal("3 of 3 books", Count(panel));
        Assert.True(ClearFilters(panel).IsVisible);

        Find<TextBox>(panel, GutenbergFilters.TextName).Text = string.Empty;
        Click(Find<Button>(panel, "GutenbergToken.Subject"));
        Assert.Contains(GutenbergFilters.SubjectHint, Drawn(panel));

        var mystery = panel.GetVisualDescendants().OfType<CheckBox>().First(box => box.Name == GutenbergFilters.SubjectName && (string)box.Tag! == "Mystery");
        mystery.IsChecked = true;
        await Until(() => Count(panel) == "1 of 1 books");

        Assert.Equal([1661], Listed(panel));
        Assert.Equal("1 of 1 books", Count(panel));
        Assert.Equal("MYSTERY", Token(panel, "Subject"));

        Click(Find<Button>(panel, "GutenbergToken.Language"));
        var languages = panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name == GutenbergFilters.LanguageName).Select(button => (string)button.Tag!).ToList();
        Assert.Equal(["en", "es"], languages);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == GutenbergFilters.LanguageName && (string)button.Tag! == "es"));
        await Until(() => Count(panel) == "0 of 0 books");

        Assert.Empty(Listed(panel));
        Assert.Equal("0 of 0 books", Count(panel));
        Assert.Equal("SPANISH", Token(panel, "Language"));

        Click(ClearFilters(panel));
        await Until(() => Count(panel) == "6 of 6 books");

        Assert.Equal([84, 1342, 1661, 36, 35, GutenbergText.Number], Listed(panel));
        Assert.Equal("6 of 6 books", Count(panel));
        Assert.Equal(string.Empty, Find<TextBox>(panel, GutenbergFilters.TextName).Text);
        Assert.Equal("ANY SUBJECT", Token(panel, "Subject"));
        Assert.Equal("ENGLISH", Token(panel, "Language"));
        Assert.False(ClearFilters(panel).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheCatalogueIsAskedForOnlyOnceProjectGutenbergIsShown()
    {
        var (window, panel, rig) = Open();

        Assert.Equal(0, _fetches);

        rig.Files.WriteText(Path.Combine(Folder, "the-raven.txt"), "Once upon a midnight dreary.");
        await OpenAddPage(panel);

        Assert.Equal(1, _fetches);

        Find<Segment>(panel, BookAddPage.SourceName).GetVisualDescendants().OfType<RadioButton>().ElementAt(1).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        await Page(panel)!.RefreshAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(BookRows(panel));
        Assert.Contains("THE-RAVEN.TXT", Drawn(panel));
        Assert.DoesNotContain(TokenFilterBar<GutenbergFilters.Kind>.TokenHint, Drawn(panel));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACatalogueThatCannotBeFetchedShowsTheNotice(bool headset)
    {
        var (window, panel, _) = Open(headset, reachable: false);

        await OpenAddPage(panel);

        var notice = Find<Notice>(panel, BookAddPage.UnreachableName);
        Assert.True(notice.IsVisible);
        Assert.Equal(BookAddPage.UnreachableLabel, notice.Label);
        Assert.Equal(BookAddPage.UnreachableText, notice.Text);
        Assert.Empty(BookRows(panel));

        window.SaveFrame(headset ? "gutenberg-unreachable-headset.png" : "gutenberg-unreachable-desktop.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task AddAndReadFetchesTheFirstLinesThenBooksOpensWithTheBookReadingFromChapterOne()
    {
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Whole(new ByteArrayContent(GutenbergText.Bytes(GutenbergText.Text)), "\"v1\""));
        var (window, panel, rig) = Open();
        await OpenAddPage(panel);
        var add = Find<Button>(panel, BookAddPage.AddName);

        Assert.False(add.IsEnabled);

        Click(Row(panel, GutenbergText.Number));
        Assert.True(add.IsEnabled);

        Click(add);
        rig.Settle();
        await Until(() => Page(panel) is null);
        rig.Settle();

        Assert.NotNull(rig.Shelf.Find(GutenbergText.Source));
        Assert.Equal(GutenbergText.Source, rig.Player.Now?.Book.Source);
        Assert.Equal(0, rig.Player.Now!.Place.Chapter);
        Assert.Null(Page(panel));
        Assert.Equal(MediaFurnishing.BooksRoot, panel.Nav.Trail[^1].Key);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AddAndReadWhileAStoryPlaysPutsTheBookOnTheShelfWithoutReadingIt()
    {
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Whole(new ByteArrayContent(GutenbergText.Bytes(GutenbergText.Text)), "\"v1\""));
        var (window, panel, rig) = Open();
        rig.PlayStory();
        await OpenAddPage(panel);
        var page = Page(panel)!;

        Click(Row(panel, GutenbergText.Number));
        Click(Find<Button>(panel, BookAddPage.AddName));
        rig.Settle();
        await Until(() => Drawn(panel).Contains("The Night Watch is on your shelf. Press it there to read it."));

        Assert.NotNull(rig.Shelf.Find(GutenbergText.Source));
        Assert.Null(rig.Player.Now);
        Assert.Same(page, Page(panel));
        Assert.Contains("The Night Watch is on your shelf. Press it there to read it.", Drawn(panel));
        Assert.False(Row(panel, GutenbergText.Number).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ABookThatCannotBeFetchedShowsTheNoticeAndAddsNothing()
    {
        _mirror.Answer = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (window, panel, rig) = Open();
        await OpenAddPage(panel);
        var page = Page(panel)!;

        Click(Row(panel, GutenbergText.Number));
        Click(Find<Button>(panel, BookAddPage.AddName));
        rig.Settle();
        await Until(() => Find<Notice>(panel, BookAddPage.UnreachableName).IsVisible);

        Assert.True(Find<Notice>(panel, BookAddPage.UnreachableName).IsVisible);
        Assert.Null(rig.Shelf.Find(GutenbergText.Source));
        Assert.Empty(rig.Shelf.Books);
        Assert.Null(rig.Player.Now);
        Assert.Same(page, Page(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ABookOnTheShelfCannotBeAddedTwice()
    {
        var (window, panel, rig) = Open();
        rig.Shelf.AddDownload(BookSource.FromGutenberg(36), "The War of the Worlds", "Wells, H. G.");
        await OpenAddPage(panel);
        var add = Find<Button>(panel, BookAddPage.AddName);

        var shelved = Row(panel, 36);
        Assert.False(shelved.IsEnabled);
        Assert.False(add.IsEnabled);

        Click(shelved);
        Click(add);
        rig.Settle();
        Dispatcher.UIThread.RunJobs();

        Assert.Single(rig.Shelf.Books);
        Assert.Empty(_mirror.Requests);

        window.Close();
    }
}
