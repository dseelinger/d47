namespace D47.Core.Speech;

/// <summary>
/// Words with an apostrophe in the middle of them, which the dictionary does not hold a single one
/// of.
/// <para>
/// <b>Found the day the local voice was first heard aloud.</b> <em>"Ship's docked in Buzhang Ku"</em>
/// came out <em>ess aitch eye pee ess</em>, because <c>Ship's</c> is not all letters, so the
/// dictionary rung skipped it, the rules rung skipped it for the same reason, and the ladder's last
/// rung spelled it — which is the honest answer for <c>GQPI</c> and the wrong one for an ordinary
/// English possessive. Measured against the shipped dictionary: <b>0 of its 274,927 entries contain
/// an apostrophe</b>, so no amount of dictionary is ever going to answer this.
/// </para>
/// <para>
/// <b>Two mechanisms, because English has two kinds.</b> The productive kind attaches to any word
/// at all — <c>Ship's</c>, <c>Commander's</c>, <c>Buzhang's</c> — so it is a rule over whatever the
/// stem turned out to sound like, and it keeps working for the invented names the ladder exists
/// for. The irregular kind is a closed list of about a dozen — <c>don't</c> is not <c>do</c> plus
/// <c>n't</c>, it is a different vowel — so it is a table, and a table is allowed to be short
/// because nobody is inventing new ones.
/// </para>
/// <para>
/// <b>Deriving beats stripping the apostrophe and looking that up</b>, which was the obvious cheap
/// fix and is wrong in the cases that matter: the dictionary reads <c>ill</c> as <em>ill</em>,
/// <c>id</c> as <em>I.D.</em>, <c>were</c> as <em>were</em> and <c>wont</c> as <em>wont</em>, so
/// <em>I'll</em>, <em>I'd</em>, <em>we're</em> and <em>won't</em> would each come out as a real
/// word that is not the one written.
/// </para>
/// </summary>
public static class Contractions
{
    /// <summary>
    /// The ones no rule reaches. Every entry here is a word whose stem is not what it sounds like
    /// — <em>do</em> plus <em>n't</em> is not <em>don't</em> — or whose stem the dictionary does
    /// not hold at all, which is the case for <c>I</c>.
    /// </summary>
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
    /// The two that are irregular <em>and</em> differ across the Atlantic, which is the same
    /// distinction <see cref="SpokenLetters"/> draws for <c>Z</c> and for the same reason: it is
    /// heard every single time.
    /// </summary>
    private static readonly Dictionary<string, (string American, string British)> ByAccent =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["can't"] = ("kˈænt", "kˈɑːnt"),
            ["shan't"] = ("ʃˈænt", "ʃˈɑːnt"),
        };

    /// <summary>
    /// The clitics that attach to anything. <c>'s</c> is absent because its sound depends on what
    /// it lands on and is decided in <see cref="Possessive"/>; <c>n't</c> is absent for the same
    /// reason.
    /// </summary>
    private static readonly Dictionary<string, string> Clitics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ll"] = "l",
        ["ve"] = "v",
        ["d"] = "d",
        ["m"] = "m",
    };

    /// <summary>Marks that are not sounds, so the last <em>sound</em> can be looked at.</summary>
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
    /// What a letters-only stem sounds like, or null where nothing can say. The ladder's own upper
    /// rungs, handed in rather than reached for, so this stays a function of its arguments.
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

        // "isn't", "doesn't", "haven't". The stem carries the n, so the word being said is the one
        // before it: "does" + n't, never "doesn" + t.
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

        // "you're", "we're" — the r-coloured schwa an American voice ends on, and the plain one a
        // British voice does.
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

    /// <summary>
    /// Which of the three sounds <c>'s</c> makes. The same rule English plurals follow, which is
    /// why <em>ship's</em> lands on <c>ʃˈɪps</c> — the very entry the dictionary holds for
    /// <em>ships</em>.
    /// </summary>
    private static string Possessive(string said)
    {
        var last = Last(said);

        return last switch
        {
            // A sibilant cannot carry another one, so a syllable is inserted: "Cortes's".
            's' or 'z' or 'ʃ' or 'ʒ' => "ᵻz",

            // Voiceless endings take a voiceless s. Everything else is voiced.
            'p' or 't' or 'k' or 'f' or 'θ' => "s",
            _ => "z",
        };
    }

    /// <summary>
    /// Whether <c>n't</c> is a syllable of its own. After a vowel or an r it is not — <em>aren't</em>,
    /// <em>weren't</em> — and after any other consonant it is: <em>isn't</em>, <em>haven't</em>,
    /// <em>didn't</em>.
    /// </summary>
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
