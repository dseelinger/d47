using System.Text.RegularExpressions;

namespace D47.Core.Callouts;

/// <summary>Finds figures and lengths of time in a reworded recap that the authored line does not contain.</summary>
public static partial class RecapFigures
{
    private static readonly HashSet<string> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
        "hundred", "thousand", "million", "dozen", "half", "couple",
    };

    private static readonly string[] TimePhrases = ["an hour", "half an hour"];

    /// <summary>The first figure or length of time in the reply that the authored line lacks, or null.</summary>
    public static string? FirstInvented(string authored, string reply)
    {
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(reply);

        foreach (var phrase in TimePhrases)
        {
            if (reply.Contains(phrase, StringComparison.OrdinalIgnoreCase)
                && !authored.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return phrase;
            }
        }

        var known = Figures(authored).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Figures(reply).FirstOrDefault(figure => !known.Contains(figure));
    }

    private static IEnumerable<string> Figures(string text)
    {
        foreach (Match match in Token().Matches(text))
        {
            if (char.IsDigit(match.Value[0]))
            {
                yield return match.Value;
            }
            else if (NumberWords.Contains(match.Value))
            {
                yield return match.Value;
            }
        }
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)*|[A-Za-z]+")]
    private static partial Regex Token();
}
