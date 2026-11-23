using System.Text;
using D47.Core.Books;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class APlainTextBookIsReadIntoChaptersTests
{
    private const string Gutenberg = """
        The Project Gutenberg eBook of The Night Watch

        This ebook is for the use of anyone anywhere in the United States and
        most other parts of the world at no cost.

        Title: The Night Watch

        Author: H. G. Wellstone

        Release date: January 1, 1900 [eBook #9001]

        *** START OF THE PROJECT GUTENBERG EBOOK THE NIGHT WATCH ***




        THE NIGHT WATCH

        by H. G. Wellstone


        CONTENTS

        I. The Lamp
        II. The Shore


        I.
        THE LAMP.


        The lamp was lit at dusk, as it had been
        every night for forty years.

        Mr. Hale climbed the stair slowly.


        CHAPTER II.

        THE SHORE.

        Nothing moved on the shore. The tide was out.

        *** END OF THE PROJECT GUTENBERG EBOOK THE NIGHT WATCH ***

        Updated editions will replace the previous one.
        """;

    private static BookRead Read(string text, BookSource? source = null) =>
        TextBookReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(text)), source ?? BookSource.FromGutenberg(9001));

    private static string[] Flat(Book book) =>
        [book.Title, book.Author, .. book.Chapters.SelectMany(chapter => (string[])["# " + chapter.Title, .. chapter.Paragraphs.Select(p => p.Text)])];

    [Fact]
    public void TheGutenbergHeaderAndFooterAreDroppedAndRomanNumeralsStartChapters()
    {
        var read = Read(Gutenberg);
        var book = read.Book!;

        Assert.Null(read.Problem);
        Assert.Equal("The Night Watch", book.Title);
        Assert.Equal("H. G. Wellstone", book.Author);
        Assert.Equal(["The Night Watch", "I. THE LAMP.", "CHAPTER II. THE SHORE."], book.Chapters.Select(chapter => chapter.Title));
        Assert.Equal(
            ["The lamp was lit at dusk, as it had been every night for forty years.", "Mr. Hale climbed the stair slowly."],
            book.Chapters[1].Paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal("Nothing moved on the shore. The tide was out.", Assert.Single(book.Chapters[2].Paragraphs).Text);
        Assert.DoesNotContain(Flat(book), line => line.Contains("Project Gutenberg", StringComparison.Ordinal) || line.Contains("Updated editions", StringComparison.Ordinal));
    }

    [Fact]
    public void ATextWithNoHeadingsIsOneChapterNamedForTheFile()
    {
        var read = Read("First paragraph,\nwrapped.\n\nSecond paragraph.\n", BookSource.FromFile(@"C:\Books\Short Notes.txt"));
        var chapter = Assert.Single(read.Book!.Chapters);

        Assert.Equal("Short Notes", read.Book.Title);
        Assert.Equal("", read.Book.Author);
        Assert.Equal("Short Notes", chapter.Title);
        Assert.Equal(["First paragraph, wrapped.", "Second paragraph."], chapter.Paragraphs.Select(paragraph => paragraph.Text));
    }

    [Fact]
    public void AWrappedParagraphEndingInARomanNumeralIsNotAHeading()
    {
        var read = Read("He was crowned as Henry\nVIII.\n\nThe end.\n");

        Assert.Equal(["He was crowned as Henry VIII.", "The end."], Assert.Single(read.Book!.Chapters).Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void ATextWithNoParagraphsGivesAReason()
    {
        var read = Read("\n\n   \n");

        Assert.Null(read.Book);
        Assert.NotNull(read.Problem);
    }

    [Fact]
    public void FeedingOneLineAtATimeGivesTheSameBookAsReadingItWhole()
    {
        var whole = Read(Gutenberg).Book!;

        var parser = new TextBookParser();
        var pieces = new List<BookTextPiece>();

        foreach (var line in Gutenberg.Split('\n'))
        {
            pieces.AddRange(parser.Feed(line));
        }

        Assert.NotEmpty(pieces);
        pieces.AddRange(parser.Finish());

        var fed = TextBookReader.Assemble(parser, pieces, BookSource.FromGutenberg(9001)).Book!;

        Assert.Equal(Flat(whole), Flat(fed));
        Assert.Equal(whole.Source, fed.Source);
    }

    [Fact]
    public void AGutenbergParagraphArrivesOnceTheBlankLineAfterItIsFed()
    {
        var parser = new TextBookParser();

        Assert.Empty(parser.Feed("*** START OF THE PROJECT GUTENBERG EBOOK X ***"));
        Assert.Empty(parser.Feed("CHAPTER 1"));
        Assert.Empty(parser.Feed(""));
        Assert.Empty(parser.Feed("It began."));

        Assert.Equal(
            [new BookTextPiece(BookTextPieceKind.Chapter, "CHAPTER 1"), new BookTextPiece(BookTextPieceKind.Paragraph, "It began.")],
            parser.Feed(""));
    }

    private const string WithContents = """
        Title: The Long Chase

        *** START OF THE PROJECT GUTENBERG EBOOK THE LONG CHASE ***

        THE LONG CHASE

        by A. Pursuer


        CONTENTS

        CHAPTER 1. Departure.

        CHAPTER 2. The Harbour.

        CHAPTER 3. The Chase.

        AFTERCONTENTS

        ETYMOLOGY.

        The word is old.

        EXTRACTS.

        Many have written of it.


        CHAPTER 1. Departure.

        They left at dawn.


        CHAPTER 2. The Harbour.

        The harbour was quiet.

        CHAPTER 3. The Chase.

        The chase began.

        *** END OF THE PROJECT GUTENBERG EBOOK THE LONG CHASE ***
        """;

    [Theory]
    [InlineData("Epilogue")]
    [InlineData("A plain line.")]
    public void AContentsListIsNoChapterAndFrontMatterIsOneChapterNamedForTheBook(string afterContents)
    {
        var book = Read(WithContents.Replace("AFTERCONTENTS", afterContents, StringComparison.Ordinal)).Book!;

        Assert.Equal(
            ["The Long Chase", "CHAPTER 1. Departure.", "CHAPTER 2. The Harbour.", "CHAPTER 3. The Chase."],
            book.Chapters.Select(chapter => chapter.Title));
        Assert.True(book.FrontMatter);
        Assert.Equal(3, ChapterNumbering.Count(book.Chapters.Count, book.FrontMatter));
        Assert.Contains("The word is old.", book.Chapters[0].Paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal("They left at dawn.", Assert.Single(book.Chapters[1].Paragraphs).Text);
    }

    [Fact]
    public void ABookThatOpensAtAHeadingHasNoFrontMatter()
    {
        var book = Read("*** START OF THE PROJECT GUTENBERG EBOOK X ***\n\nCHAPTER I.\n\nOne.\n\nCHAPTER II.\n\nTwo.\n").Book!;

        Assert.False(book.FrontMatter);
        Assert.Equal(2, ChapterNumbering.Count(book.Chapters.Count, book.FrontMatter));
    }
}
