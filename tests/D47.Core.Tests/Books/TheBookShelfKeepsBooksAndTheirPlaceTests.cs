using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class TheBookShelfKeepsBooksAndTheirPlaceTests
{
    private const string Folder = @"C:\d47\data\books";

    private static readonly Book Night = new(
        "The Night Watch",
        "H. G. Wellstone",
        BookSource.FromGutenberg(9001),
        [
            new BookChapter("I. The Lamp", [new BookParagraph("The lamp was lit."), new BookParagraph("Mr. Hale climbed.")]),
            new BookChapter("II. The Shore", [new BookParagraph("The tide was out.")]),
        ]);

    private static BookShelf Shelf(MemoryFileSystem files) => new(files, Folder, NullLogger.Instance);

    [Fact]
    public void AnAddedBookIsOnTheShelfAtItsStartAndItsTextOpens()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);

        var entry = shelf.Add(Night)!;
        var opened = shelf.Open(Night.Source)!;

        Assert.Equal(new ShelfBook(Night.Source, "The Night Watch", "H. G. Wellstone", 2, new BookPlace(0, 0), false), entry);
        Assert.Equal([entry], shelf.Books);
        Assert.Equal(["I. The Lamp", "II. The Shore"], opened.Chapters.Select(chapter => chapter.Title));
        Assert.Equal(["The lamp was lit.", "Mr. Hale climbed."], opened.Chapters[0].Paragraphs.Select(p => p.Text));
        Assert.NotNull(files.ReadText(Path.Combine(Folder, "gutenberg-9001.json")));
    }

    [Fact]
    public void APlaceIsKeptWithinTheBooksChapters()
    {
        var shelf = Shelf(new MemoryFileSystem());
        shelf.Add(Night);

        Assert.Equal(new BookPlace(1, 0), shelf.MoveTo(Night.Source, new BookPlace(7, -3))!.Place);
        Assert.True(shelf.MoveTo(Night.Source, new BookPlace(1, 0), finished: true)!.Finished);
        Assert.Null(shelf.MoveTo(BookSource.FromGutenberg(1), new BookPlace(0, 0)));
    }

    [Fact]
    public void TakingABookOffRemovesItsText()
    {
        var files = new MemoryFileSystem();
        var shelf = Shelf(files);
        shelf.Add(Night);

        Assert.True(shelf.TakeOff(Night.Source));
        Assert.False(shelf.TakeOff(Night.Source));
        Assert.Empty(shelf.Books);
        Assert.Null(shelf.Open(Night.Source));
        Assert.Null(files.ReadText(Path.Combine(Folder, "gutenberg-9001.json")));
    }

    [Fact]
    public void AShelfFileThatDoesNotParseGivesAnEmptyShelf()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Path.Combine(Folder, BookShelf.ShelfFileName), "{ not json");

        Assert.Empty(Shelf(files).Books);
    }

    [Fact]
    public void ABookFromAFileNamedShelfDoesNotTakeTheShelfFilesName()
    {
        var source = BookSource.FromFile(@"D:\Downloads\shelf.txt");

        Assert.StartsWith("file-shelf-", source.Id, StringComparison.Ordinal);
        Assert.NotEqual(source.Id, BookSource.FromFile("Shelf.txt").Id);
    }
}
