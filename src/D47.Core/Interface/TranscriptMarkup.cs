namespace D47.Core.Interface;

/// <summary>
/// How a stretch of transcript is drawn, as flags so nesting composes: bold inside a heading, a code
/// span inside a bullet.
/// </summary>
[Flags]
public enum MarkupStyle
{
    /// <summary>The conversation as it reads.</summary>
    None = 0,

    /// <summary><c>**like this**</c>.</summary>
    Strong = 1,

    /// <summary><c>*like this*</c>.</summary>
    Emphasis = 2,

    /// <summary><c>`like this`</c>, and everything inside a fence.</summary>
    Code = 4,
}

/// <summary>A stretch of one page drawn one way.</summary>
public readonly record struct MarkupSpan(string Text, MarkupStyle Style);

/// <summary>The markdown a model writes, turned into stretches a surface can draw.</summary>
public static class TranscriptMarkup
{
    /// <summary>The text split where its drawing changes, in order, with every marker consumed.</summary>
    public static IReadOnlyList<MarkupSpan> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var spans = new List<MarkupSpan>();
        var fenced = false;

        for (var at = 0; at < text.Length;)
        {
            var newline = text.IndexOf('\n', at);
            var end = newline < 0 ? text.Length : newline;
            var line = text[at..end];

            at = newline < 0 ? text.Length : newline + 1;

            // The fence line goes, and its newline with it, so a block does not open with a blank line where
            // its ``` used to be.
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                // Verbatim.
                Add(spans, line, MarkupStyle.Code);
            }
            else
            {
                Block(spans, line);
            }

            if (newline >= 0)
            {
                Add(spans, "\n", MarkupStyle.None);
            }
        }

        return spans;
    }

    /// <summary>What <see cref="Parse"/> would draw, as one string.</summary>
    public static string Plain(string? text) =>
        string.Concat(Parse(text).Select(span => span.Text));

    /// <summary>One line, once its leading marks have been read.</summary>
    private static void Block(List<MarkupSpan> spans, string line)
    {
        var indent = line.Length - line.TrimStart(' ', '\t').Length;
        var rest = line[indent..];

        // A rule is not a bullet, and `---` under a line is not a heading either.
        if (IsRule(rest))
        {
            Add(spans, line, MarkupStyle.None);
            return;
        }

        var hashes = 0;

        while (hashes < rest.Length && rest[hashes] == '#')
        {
            hashes++;
        }

        if (hashes is > 0 and <= 6 && hashes < rest.Length && rest[hashes] == ' ')
        {
            Add(spans, line[..indent], MarkupStyle.None);
            Inline(spans, rest[(hashes + 1)..].TrimStart(), MarkupStyle.Strong);
            return;
        }

        if (rest.Length > 2 && rest[0] is '-' or '*' or '+' && rest[1] == ' ')
        {
            // The bullet the reader would have drawn.
            Add(spans, line[..indent] + "• ", MarkupStyle.None);
            Inline(spans, rest[2..], MarkupStyle.None);
            return;
        }

        Add(spans, line[..indent], MarkupStyle.None);
        Inline(spans, rest, MarkupStyle.None);
    }

    /// <summary>One line's emphasis, code and nesting.</summary>
    private static void Inline(List<MarkupSpan> spans, string line, MarkupStyle carried)
    {
        var literal = 0;
        var at = 0;

        while (at < line.Length)
        {
            var character = line[at];

            if (character == '`' && Closer(line, at + 1, "`") is { } code)
            {
                Add(spans, line[literal..at], carried);

                // Never parsed further: inside backticks an asterisk is an asterisk.
                Add(spans, line[(at + 1)..code], carried | MarkupStyle.Code);
                at = literal = code + 1;
                continue;
            }

            if (character is '*' or '_' && Opens(line, at))
            {
                var marker = at + 1 < line.Length && line[at + 1] == character
                    ? line[at..(at + 2)]
                    : line[at..(at + 1)];

                if (Closer(line, at + marker.Length, marker) is { } close)
                {
                    Add(spans, line[literal..at], carried);

                    Inline(
                        spans,
                        line[(at + marker.Length)..close],
                        carried | (marker.Length == 2 ? MarkupStyle.Strong : MarkupStyle.Emphasis));

                    at = literal = close + marker.Length;
                    continue;
                }
            }

            at++;
        }

        Add(spans, line[literal..], carried);
    }

    /// <summary>Whether a marker at <paramref name="at"/> can open.</summary>
    private static bool Opens(string line, int at)
    {
        var marker = at + 1 < line.Length && line[at + 1] == line[at] ? 2 : 1;

        return at + marker < line.Length
            && !char.IsWhiteSpace(line[at + marker])
            && (at == 0 || !char.IsLetterOrDigit(line[at - 1]));
    }

    /// <summary>
    /// Where <paramref name="marker"/> closes at or after <paramref name="from"/>, or null when it
    /// never does — which is every marker in a reply still arriving, so an opener with no closer stays
    /// literal until its other half lands rather than swallowing the rest of the line.
    /// </summary>
    private static int? Closer(string line, int from, string marker)
    {
        for (var at = from; at >= 0 && at < line.Length;)
        {
            var hit = line.IndexOf(marker, at, StringComparison.Ordinal);

            if (hit < 0)
            {
                return null;
            }

            // Nothing between the two halves is not emphasis, and a marker after a space is opening something
            // rather than closing this.
            var closes = hit > from && !char.IsWhiteSpace(line[hit - 1]);

            // A one-character marker up against its twin is half of the other length rather than a closer of
            // this one, whichever side the twin is on: without the second reading, the emphasis in "*an
            // **A-rated** thruster*" closes on the first asterisk of the bold.
            if (marker.Length == 1
                && ((hit + 1 < line.Length && line[hit + 1] == marker[0])
                    || line[hit - 1] == marker[0]))
            {
                closes = false;
            }

            if (closes)
            {
                return hit;
            }

            at = hit + marker.Length;
        }

        return null;
    }

    /// <summary>A line of nothing but rule characters, three or more of them.</summary>
    private static bool IsRule(string line)
    {
        var trimmed = line.TrimEnd();

        return trimmed.Length >= 3
            && trimmed.All(character => character is '-' or '*' or '_' or ' ')
            && trimmed.Count(character => character is '-' or '*' or '_') >= 3;
    }

    /// <summary>
    /// Appends, dropping empties and merging into the span before it when they are drawn the same way.
    /// </summary>
    private static void Add(List<MarkupSpan> spans, string text, MarkupStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (spans.Count > 0 && spans[^1].Style == style)
        {
            spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            return;
        }

        spans.Add(new MarkupSpan(text, style));
    }
}
