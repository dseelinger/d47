using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Books;

/// <summary>The shelf and a book's place are read back from <c>data\books\</c> by the next run.</summary>
[Trait("Category", "Integration")]
public sealed class ABooksPlaceSurvivesARestartTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-book-shelf-" + Guid.NewGuid().ToString("N"), "books");

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_folder)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ThePlaceIsTheSameAfterTheShelfIsReloaded()
    {
        var source = BookSource.FromFile("The Night Watch.epub");
        var book = new Book(
            "The Night Watch",
            "H. G. Wellstone",
            source,
            [
                new BookChapter("One", [new BookParagraph("The lamp was lit.")]),
                new BookChapter("Two", [new BookParagraph("The tide was out."), new BookParagraph("Night fell.")]),
            ]);

        var first = new BookShelf(new DiskFileSystem(), _folder, NullLogger.Instance);
        first.Add(book);
        first.MoveTo(source, new BookPlace(1, 1));

        var second = new BookShelf(new DiskFileSystem(), _folder, NullLogger.Instance);

        Assert.Equal(new BookPlace(1, 1), second.Find(source)!.Place);
        Assert.Equal("Night fell.", second.Open(source)!.Chapters[1].Paragraphs[1].Text);
        Assert.True(File.Exists(Path.Combine(_folder, BookShelf.ShelfFileName)));
    }
}
