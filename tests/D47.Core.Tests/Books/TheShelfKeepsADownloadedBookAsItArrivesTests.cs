using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class TheShelfKeepsADownloadedBookAsItArrivesTests
{
    private const string Folder = @"C:\d47\data\books";

    private const string Header =
        "Title: The Night Watch\r\nAuthor: H. G. Wellstone\r\n\r\n*** START OF THE PROJECT GUTENBERG EBOOK ***\r\n\r\n";

    private static readonly BookSource Source = BookSource.FromGutenberg(4747);

    private static readonly string TextFile = Path.Combine(Folder, "pg4747.txt");

    private static BookShelf Shelf(MemoryFileSystem files) => new(files, Folder, NullLogger.Instance);

    [Fact]
    public void ADownloadStartsWithNoTextUnderTheCataloguesTitle()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);

        var entry = shelf.AddDownload(Source, "The Night Watch", "Wellstone, H. G.")!;

        Assert.Equal(new BookDownload(false, 0, null), entry.Download);
        Assert.Equal("", files.ReadText(TextFile));
        Assert.Null(shelf.Open(Source));
    }

    [Fact]
    public void OnlyParagraphsEndedByABlankLineOpenUntilTheTextIsWhole()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);
        shelf.AddDownload(Source, "The Night Watch", "Wellstone, H. G.");

        shelf.Arrived(Source, Header + "The lamp was lit.\r\n\r\nNight fell\r\n", false, new BookDownload(false, 100, "\"v1\""), 1);

        var partial = shelf.Open(Source)!;
        Assert.Equal("The Night Watch", partial.Title);
        Assert.Equal("Wellstone, H. G.", partial.Author);
        Assert.Equal(["The lamp was lit."], partial.Chapters.SelectMany(c => c.Paragraphs).Select(p => p.Text));

        shelf.Arrived(Source, "over the quay.", false, new BookDownload(true, 114, "\"v1\""), 1);

        Assert.Equal(
            ["The lamp was lit.", "Night fell over the quay."],
            shelf.Open(Source)!.Chapters.SelectMany(c => c.Paragraphs).Select(p => p.Text));
    }

    [Fact]
    public void TheDownloadAndThePlaceSurviveARestart()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);
        shelf.AddDownload(Source, "The Night Watch", "Wellstone, H. G.");
        shelf.Arrived(Source, Header + "CHAPTER I\r\n\r\nOne.\r\n\r\nCHAPTER II\r\n\r\nTwo.\r\n\r\n", false, new BookDownload(false, 90, "\"v1\""), 2);
        shelf.MoveTo(Source, new BookPlace(1, 0));

        var again = Shelf(files).Find(Source)!;

        Assert.Equal(new BookDownload(false, 90, "\"v1\""), again.Download);
        Assert.Equal(new BookPlace(1, 0), again.Place);
        Assert.Equal(2, again.Chapters);
    }

    [Fact]
    public void ReplacedTextThatIsNotWholeKeepsThePlaceAndTheChapterCount()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);
        shelf.AddDownload(Source, "The Night Watch", "Wellstone, H. G.");
        shelf.Arrived(Source, Header + "CHAPTER I\r\n\r\nOne.\r\n\r\nCHAPTER II\r\n\r\nTwo.\r\n\r\n", false, new BookDownload(false, 90, "\"v1\""), 2);
        shelf.MoveTo(Source, new BookPlace(1, 0));

        var replaced = shelf.Arrived(Source, Header, true, new BookDownload(false, 40, "\"v2\""), 0)!;

        Assert.Equal(Header, files.ReadText(TextFile));
        Assert.Equal(new BookPlace(1, 0), replaced.Place);
        Assert.Equal(2, replaced.Chapters);
        Assert.Equal(new BookPlace(1, 0), Shelf(files).Find(Source)!.Place);
    }

    [Fact]
    public void ADownloadsTextIsNotListedAsAFileAndGoesWhenTheBookIsTakenOff()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);
        var folder = new BookFolder(files, Folder, shelf, NullLogger.Instance);
        shelf.AddDownload(Source, "The Night Watch", "Wellstone, H. G.");
        shelf.Arrived(Source, Header + "The lamp was lit.\r\n\r\n", false, new BookDownload(false, 80, "\"v1\""), 1);

        Assert.Empty(folder.List().Books);

        Assert.True(shelf.TakeOff(Source));
        Assert.Null(files.ReadText(TextFile));
        Assert.Null(shelf.Arrived(Source, "More.\r\n", false, new BookDownload(false, 87, "\"v1\""), 1));
    }

    [Fact]
    public void ABookReadFromAFileTakesNoArrivingText()
    {
        var shelf = Shelf(new MemoryFileSystem());
        var file = BookSource.FromFile("Night.txt");
        shelf.Add(new Book("Night", "", file, [new BookChapter("Night", [new BookParagraph("One.")])]));

        Assert.Null(shelf.Arrived(file, "More.\r\n", false, new BookDownload(false, 7, null), 1));
        Assert.Throws<ArgumentException>(() => shelf.AddDownload(file, "Night", ""));
    }
}
