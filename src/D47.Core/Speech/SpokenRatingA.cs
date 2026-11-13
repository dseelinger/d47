using System.Text;
using System.Text.RegularExpressions;

namespace D47.Core.Speech;

/// <summary>A capital A inside a sentence respelled so a voice says the letter, for the provider only (#2412).</summary>
public static partial class SpokenRatingA
{
    /// <summary>The respelling the providers say as the letter and the Phonemiser reads as <c>ˈeɪ</c>.</summary>
    public const string Form = "ay";

    /// <summary>
    /// One line with each standalone capital A that is not the first word of a sentence respelled.
    /// Run this after <see cref="SpokenDesignations.Rewrite"/>, which splits "5A" into "five A".
    /// </summary>
    public static string Rewrite(string? line)
    {
        if (line is not { Length: > 0 } || !line.Contains('A'))
        {
            return line ?? string.Empty;
        }

        var built = new StringBuilder(line.Length + 8);
        var last = 0;
        var sentenceStart = true;

        foreach (var match in Token().EnumerateMatches(line))
        {
            var raw = line.AsSpan(match.Index, match.Length).ToString();
            var token = Phonemiser.Trim(raw);

            built.Append(line, last, match.Index - last);

            if (!sentenceStart && token.Body.Contains('A'))
            {
                built.Append(token.Lead);
                built.Append(Standalone().Replace(token.Body, Form));
                built.Append(token.Tail);
            }
            else
            {
                built.Append(raw);
            }

            last = match.Index + match.Length;
            sentenceStart = EndsSentence(token.Tail);
        }

        built.Append(line, last, line.Length - last);
        return built.ToString();
    }

    private static bool EndsSentence(string tail)
    {
        var mark = tail.TrimEnd('"', '\'', '”', ')', ']');
        return mark.Length > 0 && mark[^1] is '.' or '!' or '?';
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Token();

    /// <summary>An A that is the whole token or a whole dash-separated piece of it.</summary>
    [GeneratedRegex(@"(?<![^-–—])A(?![^-–—])")]
    private static partial Regex Standalone();
}
