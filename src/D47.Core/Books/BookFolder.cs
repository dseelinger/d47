using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Books;

/// <summary>A .txt or .epub in the books folder that is not on the shelf and has text to read.</summary>
public sealed record BookFile(string FileName, Book Book)
{
    public BookSource Source => Book.Source;

    /// <summary>The title the file gives, or null when the title is only the file name.</summary>
    public string? FileTitle => string.Equals(Book.Title, Book.Source.DefaultTitle, StringComparison.Ordinal) ? null : Book.Title;

    public int Chapters => Book.Chapters.Count;
}

/// <summary>A file in the books folder that cannot go on the shelf, and what to tell the Commander.</summary>
public sealed record BookFileRefusal(string FileName, string Label, string Text);

/// <summary>What the books folder holds that is not yet on the shelf.</summary>
public sealed record BookFolderListing(IReadOnlyList<BookFile> Books, IReadOnlyList<BookFileRefusal> Refused)
{
    public static BookFolderListing Empty { get; } = new([], []);
}

/// <summary>
/// The .txt and .epub files in the books folder beside the shelf, other than a downloaded book's text. A file is read
/// again only when its size or write time changes. Reads the disk on the caller's thread, so it is not called from a
/// tick.
/// </summary>
public sealed class BookFolder
{
    public const string TextExtension = ".txt";

    public const string EpubExtension = ".epub";

    public const string ProtectedText = "It may be protected by DRM. D47 reads only unprotected .epub files.";

    public const string EmptyText = "There is no text in it to read.";

    private readonly IFileSystem _files;

    private readonly BookShelf _shelf;

    private readonly ILogger _logger;

    private readonly Lock _gate = new();

    private readonly Dictionary<string, (FileState State, BookRead Read)> _read = new(StringComparer.OrdinalIgnoreCase);

    public BookFolder(IFileSystem files, string folder, BookShelf shelf, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(logger);

        _files = files;
        Folder = folder;
        _shelf = shelf;
        _logger = logger;
    }

    public string Folder { get; }

    /// <summary>Creates the folder when it is missing.</summary>
    public void Ensure() => _files.CreateFolder(Folder);

    public static bool IsBookFile(string path) =>
        Path.GetExtension(path) is { } extension
        && (extension.Equals(TextExtension, StringComparison.OrdinalIgnoreCase) || extension.Equals(EpubExtension, StringComparison.OrdinalIgnoreCase));

    /// <summary>The readable files not on the shelf, and the files that cannot be read, each in file-name order.</summary>
    public BookFolderListing List()
    {
        var books = new List<BookFile>();
        var refused = new List<BookFileRefusal>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        lock (_gate)
        {
            foreach (var path in _files.Enumerate(Folder, "*"))
            {
                if (!IsBookFile(path))
                {
                    continue;
                }

                var name = Path.GetFileName(path);
                seen.Add(path);

                if (_shelf.Find(BookSource.FromFile(name)) is not null || _shelf.IsDownloadText(name))
                {
                    continue;
                }

                var read = ReadFile(path, name);

                if (read.Book is { } book)
                {
                    books.Add(new BookFile(name, book));
                }
                else
                {
                    refused.Add(Refusal(name, read.Problem));
                }
            }

            foreach (var gone in _read.Keys.Where(key => !seen.Contains(key)).ToList())
            {
                _read.Remove(gone);
            }
        }

        return new BookFolderListing(books, refused);
    }

    /// <summary>
    /// Copies the file into the folder under its own name and returns the name; null, after logging, when it is not a
    /// .txt or .epub or cannot be copied. A file already in the folder is left where it is.
    /// </summary>
    public string? CopyIn(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!IsBookFile(path))
        {
            return null;
        }

        var name = Path.GetFileName(path);
        var target = Path.Combine(Folder, name);

        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        try
        {
            _files.Copy(path, target);
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The book at {Path} could not be copied to {Folder}", path, Folder);
            return null;
        }
    }

    /// <summary>Puts the file on the shelf at its start; null when it cannot be read, or the shelf cannot be written.</summary>
    public ShelfBook? AddToShelf(BookFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _shelf.Add(file.Book);
    }

    private BookRead ReadFile(string path, string name)
    {
        var state = _files.Stat(path);

        if (state is { } now && _read.TryGetValue(path, out var kept) && kept.State == now)
        {
            return kept.Read;
        }

        BookRead read;

        try
        {
            using var stream = _files.OpenRead(path);

            if (stream is null)
            {
                return BookRead.Refused(EmptyText);
            }

            var source = BookSource.FromFile(name);

            read = Path.GetExtension(name).Equals(EpubExtension, StringComparison.OrdinalIgnoreCase)
                ? EpubBookReader.Read(stream, source)
                : TextBookReader.Read(stream, source);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The book at {Path} could not be read", path);
            return BookRead.Refused($"The file could not be read: {ex.Message}");
        }

        if (state is { } stamped)
        {
            _read[path] = (stamped, read);
        }

        return read;
    }

    private static BookFileRefusal Refusal(string name, string? problem)
    {
        if (problem?.Contains("protected", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new BookFileRefusal(name, $"{name} has no text", ProtectedText);
        }

        if (problem is null || problem.Contains("no paragraphs", StringComparison.OrdinalIgnoreCase) || problem.Contains("no text", StringComparison.OrdinalIgnoreCase))
        {
            return new BookFileRefusal(name, $"{name} has no text", EmptyText);
        }

        return new BookFileRefusal(name, $"{name} cannot be read", problem);
    }
}
