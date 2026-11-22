using System.Text.RegularExpressions;

namespace D47.Core.Books;

public enum BookTextPieceKind
{
    /// <summary>A chapter starts here; its first paragraph follows in the same list.</summary>
    Chapter,

    Paragraph,
}

public readonly record struct BookTextPiece(BookTextPieceKind Kind, string Text);

/// <summary>
/// Reads a plain-text book one line at a time. Paragraphs are separated by blank lines; a block whose first line is a
/// chapter heading starts a chapter. A Project Gutenberg licence header and footer are dropped, and the header's
/// <c>Title:</c> and <c>Author:</c> lines are kept. Lines before a <c>*** START OF</c> line are held until it arrives,
/// so a text without one gives its pieces at <see cref="Finish"/>.
/// </summary>
public sealed partial class TextBookParser
{
    private const int MostHeadingLines = 2;

    private const int MostHeadingCharacters = 80;

    private readonly List<string> _held = [];

    private readonly List<string> _block = [];

    private Stage _stage = Stage.BeforeStart;

    private string? _headerTitle;

    private string? _headerAuthor;

    private string? _pendingChapter;

    private bool _pendingTakesTitle;

    private bool _finished;

    private enum Stage
    {
        BeforeStart,
        Body,
        AfterEnd,
    }

    /// <summary>The header's <c>Title:</c> line, once a <c>*** START OF</c> line has been read.</summary>
    public string? Title => _stage != Stage.BeforeStart ? _headerTitle : null;

    /// <summary>The header's <c>Author:</c> line, once a <c>*** START OF</c> line has been read.</summary>
    public string? Author => _stage != Stage.BeforeStart ? _headerAuthor : null;

    /// <summary>The pieces this line completes, in order.</summary>
    public IReadOnlyList<BookTextPiece> Feed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        ThrowIfFinished();

        var pieces = new List<BookTextPiece>();
        line = line.TrimEnd('\r').Replace("﻿", "", StringComparison.Ordinal);

        switch (_stage)
        {
            case Stage.BeforeStart when StartMarker().IsMatch(line):
                _held.Clear();
                _stage = Stage.Body;
                break;

            case Stage.BeforeStart:
                _held.Add(line);
                _headerTitle ??= HeaderValue(line, "Title:");
                _headerAuthor ??= HeaderValue(line, "Author:");
                break;

            case Stage.Body when EndMarker().IsMatch(line):
                CloseBlock(pieces);
                _stage = Stage.AfterEnd;
                break;

            case Stage.Body:
                Body(line, pieces);
                break;
        }

        return pieces;
    }

    /// <summary>The pieces the end of the text completes. No line may be fed after it.</summary>
    public IReadOnlyList<BookTextPiece> Finish()
    {
        ThrowIfFinished();

        var pieces = new List<BookTextPiece>();

        if (_stage == Stage.BeforeStart)
        {
            foreach (var held in _held)
            {
                Body(held, pieces);
            }

            _held.Clear();
        }

        CloseBlock(pieces);
        _finished = true;

        return pieces;
    }

    private void ThrowIfFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("The text has been finished.");
        }
    }

    private void Body(string line, List<BookTextPiece> pieces)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            CloseBlock(pieces);
        }
        else
        {
            _block.Add(line.Trim());
        }
    }

    private void CloseBlock(List<BookTextPiece> pieces)
    {
        if (_block.Count == 0)
        {
            return;
        }

        var lines = _block.ToList();
        _block.Clear();

        if (IsHeading(lines))
        {
            _pendingChapter = Collapse(string.Join(' ', lines));
            _pendingTakesTitle = lines.Count == 1;
            return;
        }

        var text = Collapse(string.Join(' ', lines));

        if (_pendingChapter is not null && _pendingTakesTitle && lines.Count == 1 && IsCapitalisedTitle(text))
        {
            _pendingChapter = $"{_pendingChapter} {text}";
            _pendingTakesTitle = false;
            return;
        }

        if (_pendingChapter is not null)
        {
            pieces.Add(new BookTextPiece(BookTextPieceKind.Chapter, _pendingChapter));
            _pendingChapter = null;
        }

        pieces.Add(new BookTextPiece(BookTextPieceKind.Paragraph, text));
    }

    private static bool IsHeading(List<string> lines) =>
        lines.Count <= MostHeadingLines
        && lines.All(line => line.Length <= MostHeadingCharacters)
        && (ChapterLine().IsMatch(lines[0]) || RomanLine().IsMatch(lines[0]));

    private static bool IsCapitalisedTitle(string text) =>
        text.Length <= MostHeadingCharacters && text.Any(char.IsLetter) && !text.Any(char.IsLower);

    private static string? HeaderValue(string line, string label) =>
        line.StartsWith(label, StringComparison.OrdinalIgnoreCase) && line[label.Length..].Trim() is { Length: > 0 } value
            ? value
            : null;

    private static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"^\s*\*{3}\s*START OF", RegexOptions.IgnoreCase)]
    private static partial Regex StartMarker();

    [GeneratedRegex(@"^\s*\*{3}\s*END OF", RegexOptions.IgnoreCase)]
    private static partial Regex EndMarker();

    [GeneratedRegex(@"^chapter\s+([ivxlcdm]+|\d+|[a-z]+(-[a-z]+)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterLine();

    [GeneratedRegex(@"^[IVXLCDM]+\.?$")]
    private static partial Regex RomanLine();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
