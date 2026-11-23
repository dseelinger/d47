using System.Security.Cryptography;
using System.Text;

namespace D47.Core.Books;

/// <summary>A book read from a text or EPUB file: its chapters in order, never none.</summary>
public sealed record Book(string Title, string Author, BookSource Source, IReadOnlyList<BookChapter> Chapters)
{
    /// <summary>The first chapter is text before the book's first heading and has no number.</summary>
    public bool FrontMatter { get; init; }
}

/// <summary>Chapters are numbered by the book: the first one that starts at a heading is 1, and front matter has no number.</summary>
public static class ChapterNumbering
{
    public static int Count(int chapters, bool frontMatter) => Math.Max(0, chapters - (frontMatter ? 1 : 0));

    /// <summary>The number of the chapter at <paramref name="index"/>; null for front matter.</summary>
    public static int? Number(int index, bool frontMatter) =>
        frontMatter ? (index <= 0 ? null : index) : index + 1;

    /// <summary><c>CH 3 OF 135</c>; null for front matter.</summary>
    public static string? Place(int index, int chapters, bool frontMatter) =>
        Number(index, frontMatter) is { } number ? $"CH {number} OF {Count(chapters, frontMatter)}" : null;

    /// <summary>The title as it is spoken: a title that begins with "Chapter" alone, any other after "Chapter n: ".</summary>
    public static string Spoken(int? number, string title)
    {
        var text = title.Trim();

        if (text.StartsWith("chapter", StringComparison.OrdinalIgnoreCase) && (text.Length == 7 || !char.IsLetter(text[7])))
        {
            text = "Chapter" + text[7..];
        }
        else if (number is { } n)
        {
            text = $"Chapter {n}: {text}";
        }

        return text.EndsWith('.') ? text : text + ".";
    }
}

public sealed record BookChapter(string Title, IReadOnlyList<BookParagraph> Paragraphs);

/// <summary>One paragraph; a resume starts at the start of one.</summary>
public sealed record BookParagraph(string Text)
{
    /// <summary>The paragraph split for speaking.</summary>
    public IReadOnlyList<string> Sentences => BookSentences.Split(Text);
}

/// <summary>What a reader made of a file. <see cref="Problem"/> is set, and <see cref="Book"/> null, when the text could not be read.</summary>
public sealed record BookRead(Book? Book, string? Problem)
{
    public static BookRead Read(Book book) => new(book, null);

    public static BookRead Refused(string problem) => new(null, problem);
}

/// <summary>Where a book came from: a Project Gutenberg number or a file name.</summary>
public sealed record BookSource
{
    private const int MostSlugCharacters = 40;

    private BookSource(int? gutenberg, string? file)
    {
        Gutenberg = gutenberg;
        File = file;
    }

    public int? Gutenberg { get; }

    /// <summary>The file name alone, without its folder.</summary>
    public string? File { get; }

    public static BookSource FromGutenberg(int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return new BookSource(number, null);
    }

    public static BookSource FromFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var name = Path.GetFileName(fileName.Trim());
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(fileName));

        return new BookSource(null, name);
    }

    /// <summary>The title used when the text names none.</summary>
    public string DefaultTitle =>
        Gutenberg is { } number ? $"Project Gutenberg #{number}" : Path.GetFileNameWithoutExtension(File!);

    /// <summary>A file-name-safe id, unique per source: <c>gutenberg-36</c>, or <c>file-</c>, a slug and a hash of the name.</summary>
    public string Id => Gutenberg is { } number ? $"gutenberg-{number}" : $"file-{Slug(File!)}-{Hash(File!)}";

    private static string Slug(string name)
    {
        var slug = new StringBuilder();

        foreach (var c in Path.GetFileNameWithoutExtension(name).ToLowerInvariant())
        {
            if (slug.Length == MostSlugCharacters)
            {
                break;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var trimmed = slug.ToString().TrimEnd('-');

        return trimmed.Length == 0 ? "book" : trimmed;
    }

    private static string Hash(string name) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
}
