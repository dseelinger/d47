using D47.Core.Books;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class TheGutenbergCatalogueIsSearchedLocallyTests
{
    private static readonly string[] Rows =
    [
        "11\tAlice's Adventures in Wonderland\tCarroll, Lewis\ten\tAdventure; Humour\t900\t2008-06-27",
        "84\tFrankenstein; Or, The Modern Prometheus\tShelley, Mary Wollstonecraft\ten\tScience fiction; Gothic fiction; Horror\t800\t1993-10-01",
        "1260\tJane Eyre: An Autobiography\tBrontë, Charlotte\ten\tGothic fiction\t700\t1998-02-01",
        "2000\tDon Quijote\tCervantes Saavedra, Miguel de\tes\tAdventure\t600\t1997-12-01",
        "17489\tLes misérables\tHugo, Victor\tfr\tHistorical fiction\t500\t2006-08-01",
        "9999\tAn undated poem\tNobody\txx\tPoetry\t100\t",
    ];

    private static GutenbergCatalogue Read(params string[] rows) =>
        GutenbergCatalogue.Read(new StringReader(string.Join("\n", [GutenbergCatalogue.Header, .. rows]) + "\n"));

    private static int[] Ids(GutenbergResult result) => [.. result.Books.Select(book => book.Id)];

    [Fact]
    public void ASmallFileIsParsedIntoItsColumns()
    {
        var catalogue = Read(Rows);
        var book = Assert.Single(catalogue.Search(new GutenbergQuery("frankenstein")).Books);

        Assert.Equal(6, catalogue.Count);
        Assert.Equal(84, book.Id);
        Assert.Equal("Shelley, Mary Wollstonecraft", book.Author);
        Assert.Equal("en", book.Language);
        Assert.Equal(["Science fiction", "Gothic fiction", "Horror"], book.Subjects);
        Assert.Equal(800, book.Downloads);
        Assert.Equal(new DateOnly(1993, 10, 1), book.Issued);
    }

    [Fact]
    public void TextMatchesTheTitleOrTheAuthorWithoutRegardToCase()
    {
        var catalogue = Read(Rows);

        Assert.Equal([11], Ids(catalogue.Search(new GutenbergQuery("WONDERLAND"))));
        Assert.Equal([11], Ids(catalogue.Search(new GutenbergQuery("carroll"))));
    }

    [Fact]
    public void BronteFindsBronte()
    {
        var catalogue = Read(Rows);

        Assert.Equal([1260], Ids(catalogue.Search(new GutenbergQuery("Bronte"))));
        Assert.Equal([17489], Ids(catalogue.Search(new GutenbergQuery("miserables"))));
        Assert.Equal([1260], Ids(catalogue.Search(new GutenbergQuery("Brontë"))));
    }

    [Fact]
    public void SubjectsMatchABookWithAnyOfThem()
    {
        var catalogue = Read(Rows);

        Assert.Equal([84, 1260], Ids(catalogue.Search(new GutenbergQuery(Subjects: ["Gothic fiction"]))));
        Assert.Equal([11, 84, 1260, 2000], Ids(catalogue.Search(new GutenbergQuery(Subjects: ["Adventure", "Gothic fiction"]))));
        Assert.Equal(6, catalogue.Search(new GutenbergQuery(Subjects: [])).Matched);
    }

    [Fact]
    public void ALanguageMatchesItsOwnCodeOnly()
    {
        var catalogue = Read(Rows);

        Assert.Equal([2000], Ids(catalogue.Search(new GutenbergQuery(Language: "es"))));
        Assert.Equal([11, 84, 1260], Ids(catalogue.Search(new GutenbergQuery(Language: "EN"))));
    }

    [Fact]
    public void FiltersNarrowTogether()
    {
        var catalogue = Read(Rows);
        var result = catalogue.Search(new GutenbergQuery("jane", ["Gothic fiction"], "en"));

        Assert.Equal([1260], Ids(result));
        Assert.Empty(catalogue.Search(new GutenbergQuery("jane", ["Horror"], "en")).Books);
    }

    [Fact]
    public void PopularOrdersByDownloads()
    {
        var catalogue = Read(Rows);

        Assert.Equal([11, 84, 1260, 2000, 17489, 9999], Ids(catalogue.Search(new GutenbergQuery(Order: GutenbergOrder.Popular))));
    }

    [Fact]
    public void AToZPutsTitlesInAlphabeticalOrder()
    {
        var titles = Read(Rows).Search(new GutenbergQuery(Order: GutenbergOrder.AToZ)).Books.Select(book => book.Title).ToList();

        Assert.Equal(
            [
                "Alice's Adventures in Wonderland",
                "An undated poem",
                "Don Quijote",
                "Frankenstein; Or, The Modern Prometheus",
                "Jane Eyre: An Autobiography",
                "Les misérables",
            ],
            titles);
    }

    [Fact]
    public void NewestPutsTheLatestIssueFirstAndAnUndatedBookLast()
    {
        var ids = Ids(Read(Rows).Search(new GutenbergQuery(Order: GutenbergOrder.Newest)));

        Assert.Equal([11, 17489, 1260, 2000, 84, 9999], ids);
    }

    [Fact]
    public void ASearchReturnsTwoHundredRowsAndTheTrueCount()
    {
        var rows = Enumerable.Range(1, 350)
            .Select(id => $"{id}\tBook {id:000}\tAuthor\ten\tAdventure\t{1000 - id}\t2020-01-01");
        var result = Read([.. rows]).Search(new GutenbergQuery("book"));

        Assert.Equal(350, result.Matched);
        Assert.Equal(200, result.Books.Count);
        Assert.Equal(1, result.Books[0].Id);
    }

    [Fact]
    public void AMalformedRowIsSkippedAndCounted()
    {
        var catalogue = Read(
            "1\tGood\tAuthor\ten\tAdventure\t5\t2020-01-01",
            "two\tBad id\tAuthor\ten\tAdventure\t5\t2020-01-01",
            "3\tShort row\tAuthor",
            "4\tBad date\tAuthor\ten\tAdventure\t5\tyesterday",
            "5\tAlso good\tAuthor\ten\t\t4\t2020-01-02");

        Assert.Equal(2, catalogue.Count);
        Assert.Equal(3, catalogue.Malformed);
        Assert.Empty(catalogue.Search(new GutenbergQuery("also")).Books.Single().Subjects);
    }

    [Fact]
    public void AFileWithoutItsHeaderIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => GutenbergCatalogue.Read(new StringReader("1\tA\tB\ten\t\t1\t2020-01-01\n")));
    }

    [Fact]
    public void LanguagesListTheirBookCountsAndNames()
    {
        var languages = Read(Rows).Languages();

        Assert.Equal(("en", "English", 3), (languages[0].Code, languages[0].Name, languages[0].Books));
        Assert.Contains(languages, language => language is { Code: "fr", Name: "French", Books: 1 });
        Assert.Contains(languages, language => language is { Code: "xx", Name: "XX", Books: 1 });
    }

    [Fact]
    public void AnUnknownLanguageCodeShowsInUpperCase()
    {
        Assert.Equal("QQ", GutenbergLanguages.NameOf("qq"));
        Assert.Equal("English", GutenbergLanguages.NameOf("en"));
    }
}
