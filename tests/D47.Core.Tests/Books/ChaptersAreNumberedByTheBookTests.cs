using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class ChaptersAreNumberedByTheBookTests
{
    private const string Folder = @"C:\d47\data\books";

    private static readonly BookSource Source = BookSource.FromGutenberg(4748);

    [Theory]
    [InlineData(0, true, null)]
    [InlineData(1, true, 1)]
    [InlineData(2, true, 2)]
    [InlineData(0, false, 1)]
    [InlineData(2, false, 3)]
    public void TheFirstChapterThatStartsAtAHeadingIsChapterOne(int index, bool frontMatter, int? expected) =>
        Assert.Equal(expected, ChapterNumbering.Number(index, frontMatter));

    [Fact]
    public void ThePlaceCountsNumberedChaptersOnlyAndFrontMatterHasNone()
    {
        Assert.Equal("CH 1 OF 135", ChapterNumbering.Place(1, 136, frontMatter: true));
        Assert.Null(ChapterNumbering.Place(0, 136, frontMatter: true));
        Assert.Equal("CH 1 OF 3", ChapterNumbering.Place(0, 3, frontMatter: false));
    }

    [Theory]
    [InlineData(2, "CHAPTER 2. The Carpet-Bag.", "Chapter 2. The Carpet-Bag.")]
    [InlineData(1, "CHAPTER 1. Loomings.", "Chapter 1. Loomings.")]
    [InlineData(2, "The Falling Star", "Chapter 2: The Falling Star.")]
    [InlineData(2, "Chaptered Lives", "Chapter 2: Chaptered Lives.")]
    public void ATitleThatBeginsWithChapterIsSpokenAlone(int number, string title, string expected) =>
        Assert.Equal(expected, ChapterNumbering.Spoken(number, title));

    [Fact]
    public void AnOldCountOnACompleteDownloadIsCorrectedWhenTheBookIsOpened()
    {
        var files = new MemoryFileSystem();
        var shelf = new BookShelf(files, Folder, NullLogger.Instance);
        shelf.AddDownload(Source, "The Long Chase", "A. Pursuer");

        var text = "Title: The Long Chase\r\n\r\n*** START OF THE PROJECT GUTENBERG EBOOK ***\r\n\r\n"
            + "CHAPTER 1. One.\r\n\r\nCHAPTER 2. Two.\r\n\r\nCHAPTER 3. Three.\r\n\r\nPreface.\r\n\r\n"
            + "CHAPTER 1. One.\r\n\r\nA tale.\r\n\r\nCHAPTER 2. Two.\r\n\r\nA song.\r\n\r\nCHAPTER 3. Three.\r\n\r\nAn end.\r\n\r\n";

        shelf.Arrived(Source, text, false, new BookDownload(true, text.Length, null), 5);
        shelf.MoveTo(Source, new BookPlace(2, 0));

        var book = shelf.Open(Source)!;
        var entry = shelf.Find(Source)!;

        Assert.Equal(4, book.Chapters.Count);
        Assert.Equal(4, entry.Chapters);
        Assert.True(entry.FrontMatter);
        Assert.Equal(3, ChapterNumbering.Count(entry.Chapters, entry.FrontMatter));
        Assert.Equal(new BookPlace(2, 0), entry.Place);
        Assert.True(new BookShelf(files, Folder, NullLogger.Instance).Find(Source)!.FrontMatter);
    }
}
