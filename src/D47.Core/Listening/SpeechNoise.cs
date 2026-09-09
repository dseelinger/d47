using System.Text.RegularExpressions;

namespace D47.Core.Listening;

/// <summary>Whether a transcription is actually somebody speaking (remediation.md 14, item 8).</summary>
public static partial class SpeechNoise
{
    /// <summary>Whether this is nothing anybody said: blank, or an annotation and nothing else.</summary>
    public static bool IsNothingSaid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // What is left once every annotation is taken out.
        var remaining = Annotations().Replace(text, string.Empty);

        return !remaining.Any(char.IsLetterOrDigit);
    }

    /// <summary>
    /// The bracketed forms, non-greedy and without nesting, so the pattern cannot run away on a line of
    /// open brackets.
    /// </summary>
    [GeneratedRegex(@"\([^()]*\)|\[[^\[\]]*\]|\*[^*]*\*|[♪♫♬♩]+")]
    private static partial Regex Annotations();
}
