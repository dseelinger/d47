namespace D47.Core.Knowledge;

/// <summary>Chooses what to ask Spansh for a typed faction name and which answers to keep.</summary>
public static class FactionNameSuggestions
{
    public const int Limit = 8;

    private static readonly char[] Separators = [' ', '\t', '\r', '\n'];

    /// <summary>The longest word of the typed text, or empty when nothing was typed.</summary>
    public static string WordToSend(string typed) =>
        Words(typed).OrderByDescending(word => word.Length).FirstOrDefault() ?? string.Empty;

    /// <summary>Names in which every typed word starts one of the name's words: whole-text starts first, then alphabetical, at most <see cref="Limit"/>.</summary>
    public static IReadOnlyList<string> Filter(string typed, IEnumerable<string> names)
    {
        var words = Words(typed);

        if (words.Length == 0)
        {
            return [];
        }

        var whole = string.Join(' ', words);

        return
        [
            .. names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => StartsEveryWord(name, words))
                .OrderBy(name => name.StartsWith(whole, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(Limit),
        ];
    }

    private static string[] Words(string typed) => typed.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    private static bool StartsEveryWord(string name, string[] typedWords)
    {
        var nameWords = Words(name);

        return typedWords.All(typed => nameWords.Any(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase)));
    }
}
