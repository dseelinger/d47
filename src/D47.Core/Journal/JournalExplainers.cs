using System.Collections.Frozen;
using D47.Core.Help;

namespace D47.Core.Journal;

/// <summary>The paragraph for a journal event kind, read from the Journal events help page.</summary>
public static class JournalExplainers
{
    private const string Heading = "## ";

    private static readonly Lazy<FrozenDictionary<string, string>> Paragraphs = new(Read);

    /// <summary>Every kind that has a paragraph.</summary>
    public static IReadOnlyCollection<string> Kinds => Paragraphs.Value.Keys;

    /// <summary>The paragraph under the kind's heading, or null when the page has none.</summary>
    public static string? For(string kind) =>
        Paragraphs.Value.TryGetValue(kind, out var paragraph) ? paragraph : null;

    private static FrozenDictionary<string, string> Read()
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        string? kind = null;
        var lines = new List<string>();

        void Close()
        {
            if (kind is not null && lines.Count > 0)
            {
                found[kind] = string.Join(' ', lines);
            }

            lines.Clear();
        }

        var page = HelpLibrary.PageFor("general-journal-events") ?? string.Empty;

        foreach (var line in page.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith(Heading, StringComparison.Ordinal))
            {
                Close();
                kind = line[Heading.Length..].Trim();
            }
            else if (kind is not null && line.Trim() is { Length: > 0 } text)
            {
                lines.Add(text);
            }
        }

        Close();
        return found.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
