using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Books;

/// <summary>Where reading stopped: a chapter index and a paragraph index within it, both from zero.</summary>
public readonly record struct BookPlace(int Chapter, int Paragraph);

/// <summary>One book on the shelf, as <c>shelf.json</c> keeps it.</summary>
public sealed record ShelfBook(BookSource Source, string Title, string Author, int Chapters, BookPlace Place, bool Finished)
{
    /// <summary>For a book whose text is downloaded, how much of it has arrived; null for a book read from a file.</summary>
    public BookDownload? Download { get; init; }
}

/// <summary>
/// A downloaded book's text so far: whether it is whole, the bytes of the source written, and the source's ETag.
/// <see cref="Written"/> counts whole lines only, so it is where a resumed download starts.
/// </summary>
public sealed record BookDownload(bool Complete, long Written, string? ETag);

/// <summary>
/// The books on the shelf, app-wide: <c>shelf.json</c> and each book's paragraphs as <c>&lt;id&gt;.json</c>, in one
/// folder. A downloaded Project Gutenberg book keeps its text as it arrived, as <c>pg&lt;n&gt;.txt</c>, and is parsed
/// when opened. Adding, arriving text, taking off and moving the place are its only writes. Reads and writes the disk on the caller's
/// thread, so it is not called from a tick.
/// </summary>
public sealed class BookShelf
{
    public const string ShelfFileName = "shelf.json";

    private readonly IFileSystem _files;

    private readonly string _folder;

    private readonly ILogger _logger;

    private readonly Lock _gate = new();

    private List<ShelfBook> _books;

    public BookShelf(IFileSystem files, string folder, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(logger);

        _files = files;
        _folder = folder;
        _logger = logger;
        _books = LoadShelf();
    }

    /// <summary>In the order they were added.</summary>
    public IReadOnlyList<ShelfBook> Books
    {
        get
        {
            lock (_gate)
            {
                return [.. _books];
            }
        }
    }

    private string ShelfFile => Path.Combine(_folder, ShelfFileName);

    public ShelfBook? Find(BookSource source)
    {
        lock (_gate)
        {
            return _books.FirstOrDefault(book => book.Source == source);
        }
    }

    /// <summary>The book's text; null, after logging, when it is not on the shelf or its file cannot be read.</summary>
    public Book? Open(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Find(source) is { Download: { } download } entry)
        {
            return OpenDownload(entry, download);
        }

        var path = TextFile(source);
        string? text;

        try
        {
            text = _files.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The book at {Path} could not be read", path);
            return null;
        }

        if (text is null)
        {
            return null;
        }

        try
        {
            return BookJson.FromJson(text, source);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The book at {Path} could not be read", path);
            return null;
        }
    }

    /// <summary>Puts the book on the shelf at its start, in place of any copy from the same source. Null, after logging, when it cannot be written.</summary>
    public ShelfBook? Add(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        var entry = new ShelfBook(book.Source, book.Title, book.Author, book.Chapters.Count, new BookPlace(0, 0), Finished: false);

        lock (_gate)
        {
            if (!TryWrite(TextFile(book.Source), BookJson.ToJson(book)))
            {
                return null;
            }

            var index = _books.FindIndex(existing => existing.Source == book.Source);

            if (index >= 0)
            {
                _books[index] = entry;
            }
            else
            {
                _books.Add(entry);
            }

            SaveShelf();
        }

        return entry;
    }

    /// <summary>The file a downloaded Project Gutenberg book's text is kept in, in the shelf's folder.</summary>
    public static string DownloadFileName(int gutenberg) => $"pg{gutenberg}.txt";

    /// <summary>A downloaded book's text as it arrived; null, after logging when it cannot be read, when there is none.</summary>
    public string? DownloadedText(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Gutenberg is not { } number)
        {
            return null;
        }

        var path = Path.Combine(_folder, DownloadFileName(number));

        try
        {
            return _files.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The book at {Path} could not be read", path);
            return null;
        }
    }

    /// <summary>Whether <paramref name="fileName"/> is the text of a downloaded book on the shelf.</summary>
    public bool IsDownloadText(string fileName)
    {
        lock (_gate)
        {
            return _books.Any(book =>
                book is { Download: not null, Source.Gutenberg: { } number }
                && string.Equals(DownloadFileName(number), fileName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Puts a Project Gutenberg book on the shelf at its start with no text yet, in place of any copy from the same
    /// source. Null, after logging, when it cannot be written.
    /// </summary>
    public ShelfBook? AddDownload(BookSource source, string title, string author)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Gutenberg is not { } number)
        {
            throw new ArgumentException("Only a Project Gutenberg book is downloaded.", nameof(source));
        }

        var entry = new ShelfBook(
            source,
            string.IsNullOrWhiteSpace(title) ? source.DefaultTitle : title.Trim(),
            author?.Trim() ?? "",
            1,
            new BookPlace(0, 0),
            Finished: false)
        {
            Download = new BookDownload(false, 0, null),
        };

        lock (_gate)
        {
            if (!TryWrite(Path.Combine(_folder, DownloadFileName(number)), ""))
            {
                return null;
            }

            var index = _books.FindIndex(existing => existing.Source == source);

            if (index >= 0)
            {
                _books[index] = entry;
            }
            else
            {
                _books.Add(entry);
            }

            SaveShelf();
        }

        return entry;
    }

    /// <summary>
    /// Writes text that arrived for a downloaded book, after the text so far or, when <paramref name="replace"/>, in its
    /// place, and records the download and the chapters so far. The place is kept, and is held within the chapters only
    /// once the text is whole. Null when the book is not a download on the shelf, or, after logging, when the text
    /// cannot be written.
    /// </summary>
    public ShelfBook? Arrived(BookSource source, string text, bool replace, BookDownload download, int chapters)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(download);

        lock (_gate)
        {
            var index = _books.FindIndex(book => book.Source == source);

            if (index < 0 || _books[index] is not { Download: not null, Source.Gutenberg: { } number } book)
            {
                return null;
            }

            var path = Path.Combine(_folder, DownloadFileName(number));

            try
            {
                if (replace)
                {
                    _files.WriteText(path, text);
                }
                else if (text.Length > 0)
                {
                    _files.AppendText(path, text);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not write {Path}", path);
                return null;
            }

            var count = Math.Max(1, chapters);
            var updated = download.Complete
                ? book with
                {
                    Chapters = count,
                    Place = new BookPlace(Math.Min(book.Place.Chapter, count - 1), book.Place.Paragraph),
                    Download = download,
                }
                : book with { Chapters = Math.Max(book.Chapters, count), Download = download };

            _books[index] = updated;
            SaveShelf();

            return updated;
        }
    }

    /// <summary>Takes the book and its text off the shelf; false when it was not there.</summary>
    public bool TakeOff(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_gate)
        {
            if (_books.RemoveAll(book => book.Source == source) == 0)
            {
                return false;
            }

            SaveShelf();

            try
            {
                _files.Delete(TextFile(source));

                if (source.Gutenberg is { } number)
                {
                    _files.Delete(Path.Combine(_folder, DownloadFileName(number)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "The text of {Book} could not be deleted", source.Id);
            }

            return true;
        }
    }

    /// <summary>
    /// Moves the book's place, kept within its chapters, and sets whether it is finished. Null when the book is not on
    /// the shelf.
    /// </summary>
    public ShelfBook? MoveTo(BookSource source, BookPlace place, bool finished = false)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_gate)
        {
            var index = _books.FindIndex(book => book.Source == source);

            if (index < 0)
            {
                return null;
            }

            var book = _books[index];
            var kept = new BookPlace(Math.Clamp(place.Chapter, 0, Math.Max(0, book.Chapters - 1)), Math.Max(0, place.Paragraph));
            var moved = book with { Place = kept, Finished = finished };

            _books[index] = moved;
            SaveShelf();

            return moved;
        }
    }

    private string TextFile(BookSource source) => Path.Combine(_folder, source.Id + ".json");

    /// <summary>
    /// The downloaded text parsed under the shelf's title and author. While the text is not whole, a paragraph not yet
    /// ended by a blank line is left out.
    /// </summary>
    private Book? OpenDownload(ShelfBook entry, BookDownload download)
    {
        if (DownloadedText(entry.Source) is not { } text)
        {
            return null;
        }

        var parser = new TextBookParser();
        var pieces = new List<BookTextPiece>();
        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } line)
        {
            pieces.AddRange(parser.Feed(line));
        }

        if (download.Complete)
        {
            pieces.AddRange(parser.Finish());
        }

        return TextBookReader.Assemble(parser, pieces, entry.Source).Book is { } book
            ? book with { Title = entry.Title, Author = entry.Author }
            : null;
    }

    private void SaveShelf() => TryWrite(ShelfFile, BookShelfJson.ToJson(_books));

    private bool TryWrite(string path, string text)
    {
        try
        {
            _files.WriteText(path, text);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write {Path}", path);
            return false;
        }
    }

    private List<ShelfBook> LoadShelf()
    {
        string? text;

        try
        {
            text = _files.ReadText(ShelfFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The book shelf at {Path} could not be read, so it starts empty", ShelfFile);
            return [];
        }

        if (text is null)
        {
            return [];
        }

        var read = BookShelfJson.FromJson(text);

        if (read.Problem is { } problem)
        {
            _logger.LogWarning("{Problem} {Path}", problem, ShelfFile);
        }

        return [.. read.Books];
    }
}
