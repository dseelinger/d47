using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace D47.Core.Logbook;

/// <summary>One sentence of the written log, and the facts it claimed.</summary>
public sealed record LogClaim(string Text, IReadOnlyList<int> Cited)
{
    public bool Traced => Cited.Count > 0;
}

/// <summary>Reads the written log back against the digest it was written from (Phase 33, item 2).</summary>
public sealed partial record LogAudit
{
    public required IReadOnlyList<LogClaim> Claims { get; init; }

    /// <summary>Sentences carrying no citation at all.</summary>
    public required IReadOnlyList<LogClaim> Uncited { get; init; }

    /// <summary>Fact numbers cited that the digest does not contain, in order, deduplicated.</summary>
    public required IReadOnlyList<int> Unknown { get; init; }

    /// <summary>Fact numbers the log actually drew on.</summary>
    public required IReadOnlyList<int> Used { get; init; }

    /// <summary>The prose with every untraceable sentence marked where it stands.</summary>
    public required string Annotated { get; init; }

    public int Sentences => Claims.Count;

    public bool Clean => Uncited.Count == 0 && Unknown.Count == 0;

    /// <summary>The marker put after a sentence that cannot be traced.</summary>
    public const string UnsupportedMarker = " **[unsupported]**";

    public string Summary()
    {
        if (Sentences == 0)
        {
            return "Nothing was written.";
        }

        if (Clean)
        {
            return $"{Number(Sentences)} of {Number(Sentences)} sentences trace back to a fact.";
        }

        var parts = new List<string>();

        if (Uncited.Count > 0)
        {
            parts.Add($"{Number(Uncited.Count)} cite nothing");
        }

        if (Unknown.Count > 0)
        {
            parts.Add(
                $"{Number(Unknown.Count)} cite a fact that does not exist "
                + $"({string.Join(", ", Unknown.Select(id => $"[{Number(id)}]"))})");
        }

        return $"{Number(Sentences - Uncited.Count)} of {Number(Sentences)} sentences trace back to a fact — "
            + string.Join("; ", parts) + ".";
    }

    /// <summary>Checks one reply against one digest.</summary>
    public static LogAudit Check(string prose, LogDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        var known = digest.Facts.Select(fact => fact.Id).ToHashSet();
        var claims = new List<LogClaim>();
        var uncited = new List<LogClaim>();
        var unknown = new List<int>();
        var used = new SortedSet<int>();
        var annotated = new StringBuilder();

        // Paragraphs rather than lines.
        var paragraph = new List<string>();

        void Flush()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            foreach (var (sentence, trailing) in Split(string.Join("\n", paragraph)))
            {
                var cited = Citations(sentence);

                foreach (var id in cited)
                {
                    if (known.Contains(id))
                    {
                        used.Add(id);
                    }
                    else if (!unknown.Contains(id))
                    {
                        unknown.Add(id);
                    }
                }

                var claim = new LogClaim(Whitespace().Replace(sentence, " ").Trim(), cited);
                claims.Add(claim);

                annotated.Append(sentence);

                if (!claim.Traced)
                {
                    uncited.Add(claim);
                    annotated.Append(UnsupportedMarker);
                }

                annotated.Append(trailing);
            }

            annotated.AppendLine();
            paragraph.Clear();
        }

        foreach (var line in (prose ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!IsStructure(line))
            {
                paragraph.Add(line);
                continue;
            }

            Flush();
            annotated.AppendLine(line);
        }

        Flush();

        return new LogAudit
        {
            Claims = claims,
            Uncited = uncited,
            Unknown = [.. unknown.Order()],
            Used = [.. used],

            // TrimEnd, because the walk above appends a line terminator per line including the last one, and
            // a file that grows a blank line every time it is written is a diff nobody asked for.
            Annotated = annotated.ToString().TrimEnd(),
        };
    }

    /// <summary>Markdown that is not a claim: headings, rules, list bullets that carry no words, blanks.</summary>
    private static bool IsStructure(string line)
    {
        var trimmed = line.Trim();

        return trimmed.Length == 0
            || trimmed.StartsWith('#')
            || trimmed.All(character => character is '-' or '*' or '_' or '=' or ' ')
            || trimmed.StartsWith("```", StringComparison.Ordinal);
    }

    /// <summary>
    /// Splits a line into sentences, each with the whitespace that followed it so the line can be put
    /// back together exactly.
    /// </summary>
    private static IEnumerable<(string Sentence, string Trailing)> Split(string line)
    {
        var start = 0;

        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] is not ('.' or '!' or '?'))
            {
                continue;
            }

            if (line[index] is '.'
                && index > 0
                && char.IsDigit(line[index - 1])
                && index + 1 < line.Length
                && char.IsDigit(line[index + 1]))
            {
                continue;
            }

            var end = index + 1;

            // A run of closing punctuation belongs to the sentence that ended: "over." and "over."" and
            // "over.)" are one sentence each.
            while (end < line.Length && line[end] is '"' or '\'' or ')' or ']' or '*' or '_')
            {
                end++;
            }

            if (end < line.Length && !char.IsWhiteSpace(line[end]))
            {
                continue;
            }

            var after = end;

            while (after < line.Length && char.IsWhiteSpace(line[after]))
            {
                after++;
            }

            yield return (line[start..end], line[end..after]);
            start = after;
            index = after - 1;
        }

        if (start < line.Length)
        {
            // A trailing fragment with no terminator is still a claim.
            yield return (line[start..], string.Empty);
        }
    }

    private static IReadOnlyList<int> Citations(string sentence)
    {
        var ids = new List<int>();

        foreach (Match match in CitationPattern().Matches(sentence))
        {
            foreach (var part in match.Groups[1].Value.Split(','))
            {
                if (int.TryParse(part.Trim(), CultureInfo.InvariantCulture, out var id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary><c>[4]</c> or <c>[4,11]</c>, anywhere in the sentence.</summary>
    [GeneratedRegex(@"\[(\d+(?:\s*,\s*\d+)*)\]")]
    private static partial Regex CitationPattern();

    /// <summary>Runs of whitespace, for reporting a claim.</summary>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
