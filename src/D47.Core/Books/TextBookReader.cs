using System.Text;

namespace D47.Core.Books;

/// <summary>Reads a UTF-8 plain-text book through <see cref="TextBookParser"/>.</summary>
public static class TextBookReader
{
    /// <summary>Never throws on bad text; a text with no paragraphs gives a reason. The stream is left open.</summary>
    public static BookRead Read(Stream stream, BookSource source)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(source);

        var parser = new TextBookParser();
        var pieces = new List<BookTextPiece>();

        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            while (reader.ReadLine() is { } line)
            {
                pieces.AddRange(parser.Feed(line));
            }
        }
        catch (IOException ex)
        {
            return BookRead.Refused($"The text could not be read: {ex.Message}");
        }

        pieces.AddRange(parser.Finish());

        return Assemble(parser, pieces, source);
    }

    /// <summary>
    /// The book the pieces make. Paragraphs before the first chapter start make a chapter named for the book.
    /// </summary>
    public static BookRead Assemble(TextBookParser parser, IEnumerable<BookTextPiece> pieces, BookSource source)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(source);

        var title = parser.Title ?? source.DefaultTitle;
        var chapters = new List<BookChapter>();
        string? chapterTitle = null;
        var paragraphs = new List<BookParagraph>();

        foreach (var piece in pieces)
        {
            if (piece.Kind == BookTextPieceKind.Chapter)
            {
                Close();
                chapterTitle = piece.Text;
            }
            else
            {
                paragraphs.Add(new BookParagraph(piece.Text));
            }
        }

        Close();

        return chapters.Count == 0
            ? BookRead.Refused("The text has no paragraphs to read.")
            : BookRead.Read(new Book(title, parser.Author ?? "", source, chapters));

        void Close()
        {
            if (paragraphs.Count > 0)
            {
                chapters.Add(new BookChapter(chapterTitle ?? title, [.. paragraphs]));
                paragraphs.Clear();
            }
        }
    }
}
