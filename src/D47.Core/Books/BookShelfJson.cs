using System.Text.Json;
using D47.Core.Storage;

namespace D47.Core.Books;

/// <summary>What <see cref="BookShelfJson.FromJson"/> read. <see cref="Problem"/> is set when the text did not parse.</summary>
public sealed record BookShelfRead(IReadOnlyList<ShelfBook> Books, string? Problem);

/// <summary>The text of <c>shelf.json</c>.</summary>
public static class BookShelfJson
{
    public static string ToJson(IEnumerable<ShelfBook> books) =>
        JsonSerializer.Serialize(new Document { Books = [.. books.Select(Row.From)] }, StoreJson.OmittingNulls);

    /// <summary>Never throws; text that does not parse gives an empty shelf and a reason. Rows that name no source are skipped.</summary>
    public static BookShelfRead FromJson(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<Document>(json ?? "", StoreJson.OmittingNulls)
                ?? throw new JsonException("The file is empty.");
            var books = new List<ShelfBook>();

            foreach (var row in document.Books ?? [])
            {
                if (row.Source() is not { } source || books.Any(book => book.Source == source))
                {
                    continue;
                }

                books.Add(new ShelfBook(
                    source,
                    string.IsNullOrWhiteSpace(row.Title) ? source.DefaultTitle : row.Title,
                    row.Author ?? "",
                    Math.Max(1, row.Chapters),
                    new BookPlace(Math.Clamp(row.Chapter, 0, Math.Max(0, row.Chapters - 1)), Math.Max(0, row.Paragraph)),
                    row.Finished)
                {
                    Download = row.Download(source),
                });
            }

            return new BookShelfRead(books, null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return new BookShelfRead([], "The book shelf could not be read, so it starts empty.");
        }
    }

    private sealed class Document
    {
        public List<Row>? Books { get; set; }
    }

    private sealed class Row
    {
        public int? Gutenberg { get; set; }

        public string? File { get; set; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public int Chapters { get; set; }

        public int Chapter { get; set; }

        public int Paragraph { get; set; }

        public bool Finished { get; set; }

        /// <summary>Set for a downloaded book: whether its text is whole.</summary>
        public bool? Complete { get; set; }

        public long? Written { get; set; }

        public string? ETag { get; set; }

        public static Row From(ShelfBook book) => new()
        {
            Gutenberg = book.Source.Gutenberg,
            File = book.Source.File,
            Title = book.Title,
            Author = book.Author,
            Chapters = book.Chapters,
            Chapter = book.Place.Chapter,
            Paragraph = book.Place.Paragraph,
            Finished = book.Finished,
            Complete = book.Download?.Complete,
            Written = book.Download?.Written,
            ETag = book.Download?.ETag,
        };

        public BookDownload? Download(BookSource source) =>
            Complete is { } complete && source.Gutenberg is not null
                ? new BookDownload(complete, Math.Max(0, Written ?? 0), string.IsNullOrWhiteSpace(ETag) ? null : ETag)
                : null;

        public BookSource? Source() =>
            Gutenberg is { } number && number > 0 ? BookSource.FromGutenberg(number)
            : !string.IsNullOrWhiteSpace(File) ? BookSource.FromFile(File)
            : null;
    }
}

/// <summary>The text of one book's file on the shelf: its title, author and chapters of paragraphs.</summary>
public static class BookJson
{
    public static string ToJson(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        return JsonSerializer.Serialize(
            new Document
            {
                Title = book.Title,
                Author = book.Author,
                Chapters = [.. book.Chapters.Select(chapter => new Chapter
                {
                    Title = chapter.Title,
                    Paragraphs = [.. chapter.Paragraphs.Select(paragraph => paragraph.Text)],
                })],
            },
            StoreJson.OmittingNulls);
    }

    /// <summary>Throws <see cref="JsonException"/> when the text does not parse or holds no paragraphs.</summary>
    public static Book FromJson(string json, BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var document = JsonSerializer.Deserialize<Document>(json ?? "", StoreJson.OmittingNulls)
            ?? throw new JsonException("The file is empty.");

        var chapters = (document.Chapters ?? [])
            .Select(chapter => new BookChapter(
                chapter.Title ?? "",
                [.. (chapter.Paragraphs ?? []).Select(text => new BookParagraph(text ?? ""))]))
            .ToList();

        if (chapters.All(chapter => chapter.Paragraphs.Count == 0))
        {
            throw new JsonException("The book has no paragraphs.");
        }

        return new Book(
            string.IsNullOrWhiteSpace(document.Title) ? source.DefaultTitle : document.Title,
            document.Author ?? "",
            source,
            chapters);
    }

    private sealed class Document
    {
        public string? Title { get; set; }

        public string? Author { get; set; }

        public List<Chapter>? Chapters { get; set; }
    }

    private sealed class Chapter
    {
        public string? Title { get; set; }

        public List<string>? Paragraphs { get; set; }
    }
}
