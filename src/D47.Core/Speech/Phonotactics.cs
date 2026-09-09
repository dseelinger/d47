namespace D47.Core.Speech;

/// <summary>
/// Whether a run of letters is something an English speaker can say, decided by parsing it into
/// syllables rather than by asking a model.
/// </summary>
public static class Phonotactics
{
    /// <summary>
    /// Vowel spellings, longest first, because <c>eau</c> must be tried before <c>ea</c> and <c>ea</c>
    /// before <c>e</c> — a shorter match would leave letters that then parse as a consonant cluster
    /// nobody can say.
    /// </summary>
    private static readonly string[] Vowels =
    [
        "eau",
        "aa", "ae", "ai", "au", "aw", "ay",
        "ea", "ee", "ei", "eu", "ew", "ey",
        "ia", "ie", "io", "iu",
        "oa", "oe", "oi", "oo", "ou", "ow", "oy",
        "ua", "ue", "ui", "uo", "uy",
        "a", "e", "i", "o", "u", "y",
    ];

    /// <summary>What a syllable may begin with, longest first.</summary>
    private static readonly string[] Onsets =
    [
        "spl", "spr", "str", "scr", "squ", "sch", "shr", "zhr", "thr", "chr", "phr", "sph",

        "bl", "br", "ch", "cl", "cr", "dr", "dw", "fl", "fr", "gh", "gl", "gn", "gr",
        "kl", "kn", "kr", "kw", "ph", "pl", "pr", "ps", "qu", "rh", "sc", "sh", "sk",
        "sl", "sm", "sn", "sp", "st", "sv", "sw", "th", "tr", "ts", "tw", "vl", "vr",
        "wh", "wr", "zh", "zl",

        "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n",
        // No "x": English has no /ks/ onset. Xylophone begins /z/, and letting x start a syllable is
        // what made XYZ parse as a word rather than three letters.
        "p", "q", "r", "s", "t", "v", "w", "y", "z",
    ];

    /// <summary>What a syllable may end with, longest first.</summary>
    private static readonly string[] Codas =
    [
        "ngths", "rlds", "ndst",

        // The silent gh (#184).
        "ght",

        "nch", "nge", "rch", "rsh", "rst", "rth", "sch", "sht", "sks", "sps", "sts",
        "tch", "ths", "cts", "lds", "lfs", "lks", "lms", "lps", "lts", "mps", "nds",
        "ngs", "nks", "nts", "ppt", "rds", "rks", "rls", "rms", "rns", "rps", "rts",

        "bs", "ch", "ck", "cs", "ct", "dge", "ds", "ft", "gh", "gs", "ks", "lb", "lch",
        "ld", "lf", "lk", "lm", "ln", "lp", "ls", "lt", "lv", "mb", "mn", "mp", "ms",
        "nd", "ng", "nk", "ns", "nt", "ph", "ps", "pt", "rb", "rc", "rd", "rf", "rg",
        "rk", "rl", "rm", "rn", "rp", "rs", "rt", "rv", "sh", "sk", "sm", "sp", "st",
        "th", "ts", "tz", "zh", "ze",

        // Doubled consonants close a syllable exactly as the single does.
        "bb", "cc", "dd", "ff", "gg", "kk", "ll", "mm", "nn", "pp", "rr", "ss", "tt", "zz",

        "b", "c", "d", "f", "g", "h", "k", "l", "m", "n",
        "p", "r", "s", "t", "v", "w", "x", "y", "z",
    ];

    /// <summary>Whether every letter of <paramref name="word"/> can be accounted for by legal syllables.</summary>
    public static bool IsSayable(string? word)
    {
        if (string.IsNullOrWhiteSpace(word) || !word.All(char.IsLetter))
        {
            return false;
        }

        return Parse(word.ToLowerInvariant(), 0);
    }

    /// <summary>
    /// The syllables <paramref name="word"/> breaks into, or an empty list where it cannot be said.
    /// </summary>
    public static IReadOnlyList<Syllable> Syllabify(string? word)
    {
        if (!IsSayable(word))
        {
            return [];
        }

        var syllables = new List<Syllable>();

        return Collect(word!.ToLowerInvariant(), 0, syllables) ? syllables : [];
    }

    private static bool Parse(string word, int at) => Collect(word, at, null);

    private static bool Collect(string word, int at, List<Syllable>? into)
    {
        if (at >= word.Length)
        {
            // A word has to have had at least one vowel to be a word.
            return into is null || into.Count > 0;
        }

        var onset = LongestAt(word, at, Onsets) ?? string.Empty;
        var vowel = LongestAt(word, at + onset.Length, Vowels);

        if (vowel is null)
        {
            // No vowel after that onset.
            foreach (var shorter in Onsets.Where(o => o.Length < onset.Length && Matches(word, at, o)))
            {
                var after = LongestAt(word, at + shorter.Length, Vowels);

                if (after is not null && TryRest(word, at, shorter, after, into))
                {
                    return true;
                }
            }

            return false;
        }

        return TryRest(word, at, onset, vowel, into);
    }

    /// <summary>Everything after the vowel: the coda, longest first, then the rest of the word.</summary>
    private static bool TryRest(string word, int at, string onset, string vowel, List<Syllable>? into)
    {
        var start = at + onset.Length + vowel.Length;

        foreach (var coda in CodasAt(word, start))
        {
            var mark = into?.Count ?? 0;
            into?.Add(new Syllable(onset, vowel, coda));

            if (Collect(word, start + coda.Length, into))
            {
                return true;
            }

            into?.RemoveRange(mark, into.Count - mark);
        }

        return false;
    }

    /// <summary>Every coda that fits at this point, longest first, including none at all.</summary>
    private static IEnumerable<string> CodasAt(string word, int at)
    {
        foreach (var coda in Codas.Where(c => Matches(word, at, c)))
        {
            yield return coda;
        }

        yield return string.Empty;
    }

    private static string? LongestAt(string word, int at, string[] candidates) =>
        candidates.FirstOrDefault(candidate => Matches(word, at, candidate));

    private static bool Matches(string word, int at, string candidate) =>
        at + candidate.Length <= word.Length
        && string.CompareOrdinal(word, at, candidate, 0, candidate.Length) == 0;
}

/// <summary>
/// One syllable, kept as the three parts the letter-to-sound rules ask about rather than as a string:
/// whether a vowel is open decides how it sounds, and that is a question about the coda.
/// </summary>
public sealed record Syllable(string Onset, string Vowel, string Coda)
{
    /// <summary>Nothing closes it, so its vowel runs long — the <c>go</c> in <c>go-ing</c>.</summary>
    public bool IsOpen => Coda.Length == 0;

    public override string ToString() => Onset + Vowel + Coda;
}
