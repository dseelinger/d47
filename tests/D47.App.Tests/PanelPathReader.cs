using System.Text.RegularExpressions;

namespace D47.App.Tests;

/// <summary>A path written with " › " in prose: the parts as written, and the group the text names, if any.</summary>
/// <param name="Parts">
/// The first part is the words before the first separator that a title could start with, the last part runs to
/// the next punctuation, and the parts between are exact.
/// </param>
internal sealed record WrittenPath(IReadOnlyList<string> Parts, string? Group)
{
    public override string ToString() => string.Join(PanelPathReader.Separator, Parts) + (Group is null ? "" : ", group " + Group);
}

/// <summary>Finds the paths written with " › " in a page or a string, outside code blocks and comments.</summary>
internal static partial class PanelPathReader
{
    public const string Separator = " › ";

    private const string Terminators = ".,;:()\"<>?!";

    private const string GroupWord = ", group ";

    private const int MostWordsBeforeTheFirstSeparator = 3;

    /// <summary>The paths in one block of prose, with its line breaks read as spaces.</summary>
    public static IReadOnlyList<WrittenPath> Read(string prose)
    {
        var text = prose.Replace("**", "", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal);
        text = Whitespace().Replace(text, " ");

        var paths = new List<WrittenPath>();
        var at = 0;

        while (text.IndexOf(Separator, at, StringComparison.Ordinal) is var first and >= 0)
        {
            var parts = new List<string> { Leading(text[..first]) };
            var from = first + Separator.Length;

            while (true)
            {
                var next = text.IndexOf(Separator, from, StringComparison.Ordinal);
                var run = next < 0 ? text[from..] : text[from..next];

                if (next < 0 || run.IndexOfAny(Terminators.ToCharArray()) >= 0)
                {
                    parts.Add(Tail(run, out var length));
                    var group = Group(run[length..]);

                    at = from + length;
                    paths.Add(new WrittenPath(parts, group));
                    break;
                }

                parts.Add(run);
                from = next + Separator.Length;
            }
        }

        return paths;
    }

    /// <summary>The paths in a markdown page, with fenced code blocks and HTML comments left out.</summary>
    public static IReadOnlyList<WrittenPath> ReadMarkdown(string markdown)
    {
        var withoutComments = Comment().Replace(markdown, "");
        var paragraph = new List<string>();
        var paths = new List<WrittenPath>();
        var fenced = false;

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                paths.AddRange(Read(string.Join(' ', paragraph)));
                paragraph.Clear();
            }
        }

        foreach (var line in withoutComments.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                fenced = !fenced;
            }
            else if (fenced)
            {
                continue;
            }
            else if (line.Trim().Length == 0)
            {
                Flush();
            }
            else
            {
                paragraph.Add(line.Trim());
            }
        }

        Flush();
        return paths;
    }

    /// <summary>The last words of a stretch of prose: its final word, and capitalised words before it.</summary>
    private static string Leading(string before)
    {
        var sentence = before[(before.LastIndexOfAny(Terminators.ToCharArray()) + 1)..].Trim();
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var taken = 1;

        while (taken < words.Length && taken < MostWordsBeforeTheFirstSeparator && char.IsUpper(words[^(taken + 1)][0]))
        {
            taken++;
        }

        return string.Join(' ', words[^Math.Min(taken, words.Length)..]);
    }

    private static string Tail(string run, out int length)
    {
        var end = run.IndexOfAny(Terminators.ToCharArray());
        length = end < 0 ? run.Length : end;
        return run[..length].Trim();
    }

    private static string? Group(string after)
    {
        if (!after.StartsWith(GroupWord, StringComparison.Ordinal))
        {
            return null;
        }

        var named = after[GroupWord.Length..];
        var end = named.IndexOfAny(Terminators.ToCharArray());
        return (end < 0 ? named : named[..end]).Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
