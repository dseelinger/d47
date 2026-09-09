namespace D47.Core.Speech;

/// <summary>
/// Words with an apostrophe in the middle of them, which the dictionary does not hold a single one of.
/// </summary>
public static class Contractions
{
    /// <summary>The ones no rule reaches.</summary>
    private static readonly Dictionary<string, string> Irregular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["don't"] = "dˈoʊnt",
        ["won't"] = "wˈoʊnt",
        ["ain't"] = "ˈeɪnt",
        ["i'm"] = "aɪm",
        ["i've"] = "aɪv",
        ["i'll"] = "aɪl",
        ["i'd"] = "aɪd",
        ["y'all"] = "jˈɔːl",
        ["o'clock"] = "əklˈɑːk",
        ["ma'am"] = "mˈæm",
    };

    /// <summary>
    /// The two that are irregular and differ across the Atlantic, which is the same distinction <see
    /// cref="SpokenLetters"/> draws for <c>Z</c> and for the same reason: it is heard every single
    /// time.
    /// </summary>
    private static readonly Dictionary<string, (string American, string British)> ByAccent =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["can't"] = ("kˈænt", "kˈɑːnt"),
            ["shan't"] = ("ʃˈænt", "ʃˈɑːnt"),
        };

    /// <summary>The clitics that attach to anything.</summary>
    private static readonly Dictionary<string, string> Clitics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ll"] = "l",
        ["ve"] = "v",
        ["d"] = "d",
        ["m"] = "m",
    };

    /// <summary>Marks that are not sounds, so the last sound can be looked at.</summary>
    private const string Marks = "ˈˌː";

    /// <summary>Whether a word is this rung's business at all: letters, with apostrophes inside.</summary>
    public static bool Looks(string word) =>
        word.Contains('\'') && word.All(character => char.IsLetter(character) || character == '\'');

    /// <summary>
    /// The word said, or null where it cannot be — which sends it on down the ladder rather than
    /// inventing something.
    /// </summary>
    /// <param name="word">The whole word, apostrophe included.</param>
    /// <param name="accent">Which side of the Atlantic the voice is from.</param>
    /// <param name="pronounce">
    /// What a letters-only stem sounds like, or null where nothing can say.
    /// </param>
    public static string? Ipa(string word, SpeechAccent accent, Func<string, string?> pronounce)
    {
        if (ByAccent.TryGetValue(word, out var sides))
        {
            return accent == SpeechAccent.British ? sides.British : sides.American;
        }

        if (Irregular.TryGetValue(word, out var known))
        {
            return known;
        }

        var mark = word.LastIndexOf('\'');

        if (mark <= 0 || mark == word.Length - 1)
        {
            return null;
        }

        var stem = word[..mark];
        var suffix = word[(mark + 1)..];

        // "isn't", "doesn't", "haven't".
        if (string.Equals(suffix, "t", StringComparison.OrdinalIgnoreCase)
            && stem.EndsWith("n", StringComparison.OrdinalIgnoreCase))
        {
            return pronounce(stem[..^1]) is { Length: > 0 } negated
                ? negated + Negation(negated)
                : null;
        }

        if (pronounce(stem) is not { Length: > 0 } said)
        {
            return null;
        }

        // "you're", "we're" — the r-coloured schwa an American voice ends on, and the plain one a British
        // voice does.
        if (string.Equals(suffix, "re", StringComparison.OrdinalIgnoreCase))
        {
            return said + (accent == SpeechAccent.British ? "ə" : "ɚ");
        }

        if (string.Equals(suffix, "s", StringComparison.OrdinalIgnoreCase))
        {
            return said + Possessive(said);
        }

        return Clitics.TryGetValue(suffix, out var clitic) ? said + clitic : null;
    }

    /// <summary>Which of the three sounds <c>'s</c> makes.</summary>
    private static string Possessive(string said)
    {
        var last = Last(said);

        return last switch
        {
            // A sibilant cannot carry another one, so a syllable is inserted: "Cortes's".
            's' or 'z' or 'ʃ' or 'ʒ' => "ᵻz",

            // Voiceless endings take a voiceless s.
            'p' or 't' or 'k' or 'f' or 'θ' => "s",
            _ => "z",
        };
    }

    /// <summary>Whether <c>n't</c> is a syllable of its own.</summary>
    private static string Negation(string said)
    {
        var last = Last(said);

        return "aeiouæɑɔəɚɜɛɪʊʌʔɹɻɐɒɘɵʉɨ".Contains(last, StringComparison.Ordinal) ? "nt" : "ənt";
    }

    /// <summary>The last thing in a transcription that is a sound rather than a mark on one.</summary>
    private static char Last(string said)
    {
        for (var i = said.Length - 1; i >= 0; i--)
        {
            if (!Marks.Contains(said[i], StringComparison.Ordinal))
            {
                return said[i];
            }
        }

        return '\0';
    }
}
