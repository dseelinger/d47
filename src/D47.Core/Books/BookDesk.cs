namespace D47.Core.Books;

/// <summary>The book being read or paused, as the voice commands see it.</summary>
public sealed record BookNow(BookSource Source, string Title, int Chapter, string ChapterTitle, bool Paused, bool FrontMatter = false);

/// <summary>
/// What the book voice commands act on: the shelf, and the reader that plays a book aloud. A book does not start or
/// resume while <paramref name="storyPlaying"/> says a story plays.
/// </summary>
public sealed class BookDesk(
    Func<IReadOnlyList<ShelfBook>> shelf,
    Func<BookSource, bool> read,
    Func<BookNow?> now,
    Action pause,
    Action resume,
    Func<bool> nextChapter,
    Func<bool> backAParagraph,
    Func<bool>? storyPlaying = null)
{
    public const string NoBook = "No book is being read.";

    public const string StoryPlaying = "A story is playing.";

    private readonly Lock _gate = new();

    private BookSource? _last;

    /// <summary>A book is being read, or the Commander has paused it.</summary>
    public bool Active => now() is not null;

    public IReadOnlyList<ShelfBook> Shelf() => shelf();

    /// <summary>Reads the shelf book called <paramref name="title"/>; with none, the book last read.</summary>
    public string Read(string? title)
    {
        if (storyPlaying?.Invoke() == true)
        {
            return StoryPlaying;
        }

        var books = shelf();
        ShelfBook? chosen;

        if (string.IsNullOrWhiteSpace(title))
        {
            chosen = LastRead(books);

            if (chosen is null)
            {
                return books.Count == 0 ? "No books are on the shelf." : "Say read, then the title of a book on the shelf.";
            }
        }
        else
        {
            var wanted = title.Trim();
            chosen = books.FirstOrDefault(book => string.Equals(book.Title, wanted, StringComparison.OrdinalIgnoreCase))
                ?? books.FirstOrDefault(book => book.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));

            if (chosen is null)
            {
                return $"No book on the shelf is called {wanted}.";
            }
        }

        if (!read(chosen.Source))
        {
            return $"{chosen.Title} could not be read.";
        }

        lock (_gate)
        {
            _last = chosen.Source;
        }

        return now() is { } reading
            ? ChapterNumbering.Number(reading.Chapter, reading.FrontMatter) is { } number
                ? $"Reading {reading.Title}, chapter {number}."
                : $"Reading {reading.Title}."
            : $"Reading {chosen.Title}.";
    }

    public string Pause()
    {
        if (now() is not { } reading)
        {
            return NoBook;
        }

        if (reading.Paused)
        {
            return "The book is already paused.";
        }

        pause();
        return "Paused.";
    }

    public string Resume()
    {
        if (now() is not { } reading)
        {
            return NoBook;
        }

        if (!reading.Paused)
        {
            return "The book is already playing.";
        }

        if (storyPlaying?.Invoke() == true)
        {
            return StoryPlaying;
        }

        resume();
        return "Resumed.";
    }

    public string NextChapter()
    {
        if (now() is null)
        {
            return NoBook;
        }

        if (!nextChapter())
        {
            return "This is the last chapter.";
        }

        return now() is { } reading
            ? ChapterNumbering.Spoken(ChapterNumbering.Number(reading.Chapter, reading.FrontMatter), reading.ChapterTitle)
            : "Next chapter.";
    }

    public string BackAParagraph() =>
        now() is null ? NoBook : backAParagraph() ? "Back a paragraph." : "That paragraph could not be read.";

    /// <summary>The book being read, else the last one read this session, else the first with a saved place.</summary>
    private ShelfBook? LastRead(IReadOnlyList<ShelfBook> books)
    {
        BookSource? remembered;

        lock (_gate)
        {
            remembered = now()?.Source ?? _last;
        }

        return books.FirstOrDefault(book => book.Source == remembered)
            ?? books.FirstOrDefault(book => !book.Finished && book.Place != new BookPlace(0, 0));
    }
}
