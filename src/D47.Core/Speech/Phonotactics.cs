namespace D47.Core.Speech;

/// <summary>
/// Whether a run of letters is something an English speaker can say, decided by parsing it into
/// syllables rather than by asking a model.
/// <para>
/// <b>This is the gate the whole local-voice phase rests on, and it exists because the alternative
/// measured zero.</b> Kokoro takes phonemes and no text, so something must turn a name into sounds.
/// The neural grapheme-to-phoneme model scored <b>0.0% exact</b> on words drawn from its own
/// training dictionary — <c>station</c> came back as <c>stetɔn</c> — and Elite has 400 billion
/// system names, so the tail is not a thing a dictionary can be extended to cover.
/// </para>
/// <para>
/// English phonotactics is a closed system: a syllable is a legal onset, a vowel, and a legal coda,
/// and the inventories are finite. So <em>can this be said</em> is a parse, and a parse is
/// deterministic, testable, and wrong only in ways a test can show. <c>COL</c> parses; <c>GQPI</c>
/// does not, because no English syllable begins <c>gq</c> and there is no vowel to begin one with.
/// </para>
/// <para>
/// <b>Sounds English speakers can make are admitted even where English does not use them</b> —
/// the Commander's ruling of 2026-08-28. <c>zh</c> is the example that matters: it is not native
/// English spelling, every English speaker says it in <em>measure</em>, and admitting it as a coda
/// is what makes <c>Dezhra</c> parse as <c>dezh-ra</c> rather than being spelled out letter by
/// letter. That is the most-spoken system name in the game, so the ruling pays for itself on the
/// first jump.
/// </para>
/// </summary>
public static class Phonotactics
{
    /// <summary>
    /// Vowel spellings, longest first, because <c>eau</c> must be tried before <c>ea</c> and
    /// <c>ea</c> before <c>e</c> — a shorter match would leave letters that then parse as a
    /// consonant cluster nobody can say.
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

    /// <summary>
    /// What a syllable may begin with, longest first.
    /// <para>
    /// The three-letter clusters lead because <c>str</c> must beat <c>st</c>, and <c>shr</c> and
    /// <c>zhr</c> sit beside each other deliberately: the second is the ruling above, and leaving
    /// it out while keeping the first would be admitting a sound and refusing its cluster.
    /// </para>
    /// </summary>
    private static readonly string[] Onsets =
    [
        "spl", "spr", "str", "scr", "squ", "sch", "shr", "zhr", "thr", "chr", "phr", "sph",

        "bl", "br", "ch", "cl", "cr", "dr", "dw", "fl", "fr", "gh", "gl", "gn", "gr",
        "kl", "kn", "kr", "kw", "ph", "pl", "pr", "ps", "qu", "rh", "sc", "sh", "sk",
        "sl", "sm", "sn", "sp", "st", "sv", "sw", "th", "tr", "ts", "tw", "vl", "vr",
        "wh", "wr", "zh", "zl",

        "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n",
        // No "x": English has no /ks/ onset. <em>Xylophone</em> begins /z/, and letting x
        // start a syllable is what made XYZ parse as a word rather than three letters.
        "p", "q", "r", "s", "t", "v", "w", "y", "z",
    ];

    /// <summary>
    /// What a syllable may end with, longest first. Richer than the onset set, which is how English
    /// works: <c>strengths</c> is one syllable and its coda is doing most of the labour.
    /// </summary>
    private static readonly string[] Codas =
    [
        "ngths", "rlds", "ndst",

        // <b>The silent gh</b> (#184). Without this row there was no coda for it at all: the parse
        // took "gh", was left with a "t" that begins no syllable, backtracked to "g" and was left
        // with "ht", and gave up — so "light" was not a sayable word and was spelled out letter by
        // letter. Ahead of "gh" because the list is longest-first, and #155 rewrites "ly" to
        // "light years" before any provider sees it, which makes this a word d47 says constantly.
        "ght",

        "nch", "nge", "rch", "rsh", "rst", "rth", "sch", "sht", "sks", "sps", "sts",
        "tch", "ths", "cts", "lds", "lfs", "lks", "lms", "lps", "lts", "mps", "nds",
        "ngs", "nks", "nts", "ppt", "rds", "rks", "rls", "rms", "rns", "rps", "rts",

        "bs", "ch", "ck", "cs", "ct", "dge", "ds", "ft", "gh", "gs", "ks", "lb", "lch",
        "ld", "lf", "lk", "lm", "ln", "lp", "ls", "lt", "lv", "mb", "mn", "mp", "ms",
        "nd", "ng", "nk", "ns", "nt", "ph", "ps", "pt", "rb", "rc", "rd", "rf", "rg",
        "rk", "rl", "rm", "rn", "rp", "rs", "rt", "rv", "sh", "sk", "sm", "sp", "st",
        "th", "ts", "tz", "zh", "ze",

        // Doubled consonants close a syllable exactly as the single does. Without these,
        // "well" does not parse and every ordinary doubled word in d47's prose gets spelled.
        "bb", "cc", "dd", "ff", "gg", "kk", "ll", "mm", "nn", "pp", "rr", "ss", "tt", "zz",

        "b", "c", "d", "f", "g", "h", "k", "l", "m", "n",
        "p", "r", "s", "t", "v", "w", "x", "y", "z",
    ];

    /// <summary>
    /// Whether every letter of <paramref name="word"/> can be accounted for by legal syllables.
    /// <para>
    /// Greedy with a backtrack on the coda, which is the one place greed goes wrong: <c>rock-et</c>
    /// wants the <c>ck</c> to close a syllable, and <c>ro-cket</c> wants it not to. Trying the
    /// longest coda first and shortening on failure covers both without a general parser, because
    /// the onset that follows is what decides.
    /// </para>
    /// </summary>
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
    /// Exists so the letter-to-sound rules can work a syllable at a time, which is what makes an
    /// open vowel long and a closed one short without a second parse.
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
            // No vowel after that onset. A shorter onset may leave one — "sky" wants "sk" then "y",
            // and a greedy read that took "sk" and stopped would be right, but "psy" wants "ps"
            // where "p" alone would strand "sy" perfectly well. Trying shorter is cheap.
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

    /// <summary>
    /// Everything after the vowel: the coda, longest first, then the rest of the word. The
    /// backtrack lives here because a coda that swallows the next syllable's onset is the only way
    /// this parse fails on a word that is perfectly sayable.
    /// </summary>
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
/// One syllable, kept as the three parts the letter-to-sound rules ask about rather than as a
/// string: whether a vowel is open decides how it sounds, and that is a question about the coda.
/// </summary>
public sealed record Syllable(string Onset, string Vowel, string Coda)
{
    /// <summary>Nothing closes it, so its vowel runs long — the <c>go</c> in <c>go-ing</c>.</summary>
    public bool IsOpen => Coda.Length == 0;

    public override string ToString() => Onset + Vowel + Coda;
}
