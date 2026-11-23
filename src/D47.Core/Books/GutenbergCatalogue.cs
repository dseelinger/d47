using System.Globalization;
using System.Text;

namespace D47.Core.Books;

public enum GutenbergOrder
{
    /// <summary>Most downloads first.</summary>
    Popular,

    /// <summary>Title, A to Z.</summary>
    AToZ,

    /// <summary>Most recently issued first.</summary>
    Newest,
}

public sealed record GutenbergBook(
    int Id,
    string Title,
    string Author,
    string Language,
    IReadOnlyList<string> Subjects,
    int Downloads,
    DateOnly? Issued);

/// <summary>A search of the catalogue. Empty subjects and null fields match everything.</summary>
public sealed record GutenbergQuery(
    string? Text = null,
    IReadOnlyList<string>? Subjects = null,
    string? Language = null,
    GutenbergOrder Order = GutenbergOrder.Popular);

/// <summary>Every book that matched, in the chosen order, and their number.</summary>
public sealed record GutenbergResult(IReadOnlyList<GutenbergBook> Books, int Matched);

public sealed record GutenbergLanguage(string Code, string Name, int Books);

/// <summary>
/// The weekly Project Gutenberg list, <c>gutenberg.tsv.gz</c>, read into memory and searched on this computer.
/// </summary>
public sealed class GutenbergCatalogue
{
    public const string Header = "id\ttitle\tauthor\tlanguage\tsubjects\tdownloads\tissued";

    /// <summary>The most rows read; a file with more is refused.</summary>
    public const int MaxFileRows = 500_000;

    /// <summary>The subjects <c>tools/book-catalogue.py</c> writes, in the order a page offers them.</summary>
    public static IReadOnlyList<string> Subjects { get; } =
        ["Science fiction", "Adventure", "Mystery", "Horror", "Gothic fiction", "Historical fiction", "Humour", "Poetry"];

    private readonly List<Entry> _entries = [];

    private GutenbergCatalogue()
    {
    }

    /// <summary>The rows skipped because a column was missing or could not be read.</summary>
    public int Malformed { get; private set; }

    public int Count => _entries.Count;

    /// <summary>Throws <see cref="InvalidDataException"/> when the header is missing or the file has too many rows.</summary>
    public static GutenbergCatalogue Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.ReadLine() != Header)
        {
            throw new InvalidDataException("Not a Gutenberg catalogue: the header is missing.");
        }

        var catalogue = new GutenbergCatalogue();
        var read = 0;

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (++read > MaxFileRows)
            {
                throw new InvalidDataException($"The Gutenberg catalogue has more than {MaxFileRows} rows.");
            }

            if (Parse(line) is { } entry)
            {
                catalogue._entries.Add(entry);
            }
            else
            {
                catalogue.Malformed++;
            }
        }

        catalogue._entries.TrimExcess();

        return catalogue;
    }

    public GutenbergResult Search(GutenbergQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = Fold(query.Text ?? string.Empty).Trim();
        var subjects = query.Subjects is { Count: > 0 } wanted ? wanted : null;
        var language = string.IsNullOrWhiteSpace(query.Language) ? null : query.Language.Trim();
        var matches = new List<Entry>();

        foreach (var entry in _entries)
        {
            if (language is not null && !entry.Book.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (subjects is not null
                && !subjects.Any(subject => entry.Book.Subjects.Contains(subject, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (text.Length > 0 && !entry.Folded.Contains(text, StringComparison.Ordinal))
            {
                continue;
            }

            matches.Add(entry);
        }

        IEnumerable<Entry> ordered = query.Order switch
        {
            GutenbergOrder.AToZ => matches.OrderBy(entry => entry.FoldedTitle, StringComparer.Ordinal),
            GutenbergOrder.Newest => matches
                .OrderByDescending(entry => entry.Book.Issued ?? DateOnly.MinValue)
                .ThenByDescending(entry => entry.Book.Downloads),
            _ => matches.OrderByDescending(entry => entry.Book.Downloads),
        };

        return new GutenbergResult([.. ordered.Select(entry => entry.Book)], matches.Count);
    }

    /// <summary>The languages present with their book counts, most books first.</summary>
    public IReadOnlyList<GutenbergLanguage> Languages() =>
    [
        .. _entries
            .GroupBy(entry => entry.Book.Language, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GutenbergLanguage(group.Key, GutenbergLanguages.NameOf(group.Key), group.Count()))
            .OrderByDescending(language => language.Books)
            .ThenBy(language => language.Name, StringComparer.Ordinal),
    ];

    /// <summary>Lower case with accents removed, so "Bronte" finds "Brontë".</summary>
    internal static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static Entry? Parse(string line)
    {
        var columns = line.Split('\t');

        if (columns.Length != 7
            || !int.TryParse(columns[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || !int.TryParse(columns[5], NumberStyles.None, CultureInfo.InvariantCulture, out var downloads)
            || columns[1].Length == 0)
        {
            return null;
        }

        DateOnly? issued = null;

        if (columns[6].Length > 0)
        {
            if (!DateOnly.TryParseExact(columns[6], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return null;
            }

            issued = date;
        }

        string[] subjects = columns[4].Length == 0
            ? []
            : columns[4].Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var book = new GutenbergBook(id, columns[1], columns[2], columns[3], subjects, downloads, issued);
        var title = Fold(book.Title);

        return new Entry(book, title, title + "\n" + Fold(book.Author));
    }

    private sealed record Entry(GutenbergBook Book, string FoldedTitle, string Folded);
}
