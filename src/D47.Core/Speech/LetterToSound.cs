namespace D47.Core.Speech;

/// <summary>Spelling to IPA for a word no dictionary holds, by rule.</summary>
public static class LetterToSound
{
    /// <summary>Consonant spellings to IPA, longest first.</summary>
    private static readonly (string Spelling, string Ipa)[] Consonants =
    [
        ("sch", "ʃ"), ("shr", "ʃɹ"), ("zhr", "ʒɹ"), ("thr", "θɹ"), ("chr", "kɹ"),
        ("phr", "fɹ"), ("spl", "spl"), ("spr", "spɹ"), ("str", "stɹ"), ("scr", "skɹ"),
        ("squ", "skw"), ("sph", "sf"),

        // The silent gh (#184).
        ("ght", "t"),

        // A doubled consonant is one sound.
        ("bb", "b"), ("cc", "k"), ("dd", "d"), ("ff", "f"), ("gg", "ɡ"), ("kk", "k"),
        ("ll", "l"), ("mm", "m"), ("nn", "n"), ("pp", "p"), ("rr", "ɹ"), ("ss", "s"),
        ("tt", "t"), ("zz", "z"),

        ("ch", "tʃ"), ("ck", "k"), ("dge", "dʒ"), ("gh", "ɡ"), ("gn", "n"), ("kn", "n"),

        // Ahead of "ng", which would otherwise match first and swallow the /dʒ/ (#179).
        ("nge", "ndʒ"),

        ("ng", "ŋ"), ("ph", "f"), ("ps", "s"), ("qu", "kw"), ("rh", "ɹ"), ("sh", "ʃ"),
        ("th", "θ"), ("tch", "tʃ"), ("wh", "w"), ("wr", "ɹ"), ("zh", "ʒ"),

        ("b", "b"), ("c", "k"), ("d", "d"), ("f", "f"), ("g", "ɡ"), ("h", "h"),
        ("j", "dʒ"), ("k", "k"), ("l", "l"), ("m", "m"), ("n", "n"), ("p", "p"),
        ("q", "k"), ("r", "ɹ"), ("s", "s"), ("t", "t"), ("v", "v"), ("w", "w"),
        ("x", "ks"), ("y", "j"), ("z", "z"),
    ];

    /// <summary>Vowel spellings, in their closed reading — the short one, which is what a coda produces.</summary>
    private static readonly Dictionary<string, string> Short = new(StringComparer.Ordinal)
    {
        ["a"] = "æ", ["e"] = "ɛ", ["i"] = "ɪ", ["o"] = "ɑː", ["u"] = "ʌ", ["y"] = "ɪ",
        ["aa"] = "ɑː", ["ae"] = "eɪ", ["ai"] = "eɪ", ["au"] = "ɔː", ["aw"] = "ɔː", ["ay"] = "eɪ",
        ["ea"] = "iː", ["ee"] = "iː", ["ei"] = "eɪ", ["eu"] = "juː", ["ew"] = "juː", ["ey"] = "eɪ",
        ["ia"] = "iːə", ["ie"] = "iː", ["io"] = "iːoʊ", ["iu"] = "iːuː",
        ["oa"] = "oʊ", ["oe"] = "oʊ", ["oi"] = "ɔɪ", ["oo"] = "uː", ["ou"] = "aʊ",
        ["ow"] = "aʊ", ["oy"] = "ɔɪ",
        ["ua"] = "uːə", ["ue"] = "uː", ["ui"] = "uːɪ", ["uo"] = "uːoʊ", ["uy"] = "aɪ",
        ["eau"] = "oʊ",
    };

    /// <summary>
    /// The single vowels in their open reading — the long one, which is what nothing closing the
    /// syllable produces.
    /// </summary>
    private static readonly Dictionary<string, string> Long = new(StringComparer.Ordinal)
    {
        ["a"] = "eɪ", ["e"] = "iː", ["i"] = "aɪ", ["o"] = "oʊ", ["u"] = "juː", ["y"] = "aɪ",
    };

    /// <summary>The single vowels that reduce to a schwa when they end an unstressed final syllable.</summary>
    private static readonly HashSet<string> Reduces = ["a", "e", "o", "u"];

    /// <summary>
    /// Consonant spellings of two letters that are one sound, which is what decides whether a silent
    /// <c>e</c> lengthens the vowel in front of it.
    /// </summary>
    private static readonly HashSet<string> OneSound =
        new(StringComparer.Ordinal) { "ch", "gh", "ph", "sh", "th", "zh" };

    /// <summary>The silent <c>gh</c> (#184).</summary>
    private const string SilentGh = "ght";

    /// <summary>
    /// The one vowel that reads differently in front of <see cref="SilentGh"/>: <c>ou</c> is /ɔː/
    /// rather than its usual /aʊ/, so fought, bought and thought are /ɔːt/.
    /// </summary>
    private static string? Rereads(string coda, string vowel) =>
        coda == SilentGh && vowel == "ou" ? "ɔː" : null;

    /// <summary>
    /// The three codas that carry their own silent <c>e</c> inside them — <c>nge</c>, <c>dge</c> and
    /// <c>ze</c> — and whether that <c>e</c> lengthens the vowel in front of it (#179).
    /// </summary>
    private static bool CarriesASilentE(string coda, string vowel) => coda switch
    {
        "ze" => true,
        "nge" => vowel == "a",
        _ => false,
    };

    /// <summary>The word in IPA, or null where it cannot be said and must be spelled instead.</summary>
    public static string? Pronounce(string? word)
    {
        var syllables = Phonotactics.Syllabify(word);

        if (syllables.Count == 0)
        {
            return null;
        }

        // A final e after a consonant is silent (#153, reported 2026-08-28).
        var silentE = syllables.Count > 1 && IsSilentE(syllables[^1]);
        var count = silentE ? syllables.Count - 1 : syllables.Count;

        // A final -le or -re is a syllabic consonant, not a consonant and a schwa (#179). "table" was
        // "tˈæblə" rather than "tˈeɪbəl": the parse hands back "ta.ble", the l is that syllable's onset, and
        // the reduction rule below then voiced the e after it.
        var syllabic = syllables.Count > 1 && IsSyllabic(syllables[^1]);

        // And whatever else stands in that onset stays in front of the schwa (#184). #179 asked the
        // onset to be exactly "l" or "r", which is what the parse hands back only when nothing before the l
        // could close the syllable before it — "ta.ble", "ac.re".
        var ahead = syllabic ? syllables[^1].Onset[..^1] : string.Empty;

        // Which syllable the trailing e — silent, or standing behind a syllabic l — reaches back over.
        var behind = silentE ? count - 1 : syllabic ? count - 2 : -1;

        var built = new System.Text.StringBuilder();

        for (var i = 0; i < count; i++)
        {
            var syllable = syllables[i];

            if (syllabic && i == count - 1)
            {
                built.Append(Spell(ahead));
                built.Append('ə');
                built.Append(Spell(syllable.Onset[^1..]));
                continue;
            }

            built.Append(Spell(SoftenOnset(syllable.Onset, syllable.Vowel)));

            // An unstressed final vowel reduces. English does this to nearly every word that ends in
            // one: the a of "Dezhra" is a schwa, not the a of "day", and a final y is /i/ rather than /aɪ/.
            var last = i == count - 1 && count > 1;

            // And the e it was hiding behind lengthens what it left. That is the other half of the
            // same rule: the e in "Lave" is not merely silent, it is what makes the a say its own name.
            var lengthens = (i == behind && Lengthens(syllable.Coda + ahead))
                            || CarriesASilentE(syllable.Coda, syllable.Vowel)
                            || syllable.Coda == SilentGh;

            var vowel =
                // "ought" is /ɔːt/, which is neither this vowel's short reading nor its long one.
                Rereads(syllable.Coda, syllable.Vowel) is { } reread ? reread
                : last && syllable.IsOpen && syllable.Vowel == "y" ? "i"
                : last && syllable.IsOpen && Reduces.Contains(syllable.Vowel) ? "ə"
                : (syllable.IsOpen || lengthens)
                  && Long.TryGetValue(syllable.Vowel, out var open) ? open
                : Short.GetValueOrDefault(syllable.Vowel, syllable.Vowel);

            // A leading glide belongs to the onset, not to the nucleus. Four vowel spellings here
            // answer with one — <c>eu</c>, <c>ew</c> and a long <c>u</c> all give <c>juː</c> — so marking the
            // start of the vowel string would put the mark back in front of a consonant for exactly
            // those words, which is the fault being fixed reached by a different road.
            var glide = vowel.Length > 1 && vowel[0] is 'j' or 'w' ? 1 : 0;

            built.Append(vowel[..glide]);

            // The mark goes immediately before the stressed vowel, never before the consonants in front of
            // it (reported 2026-08-28, against the local voice shipped in 0.84.0).
            if (i == 0 && count > 1)
            {
                built.Append('ˈ');
            }

            built.Append(vowel[glide..]);

            // The silent e softens as well as lengthens — ace is not ake and page
            // is not pag.
            built.Append(Spell(Coda(
                silentE && i == count - 1 ? SoftenCoda(syllable.Coda) : syllable.Coda)));

            // And "ng" keeps its /ɡ/ in front of a syllabic l (#184). "single" was "sˈɪŋəl" and
            // "angle" was "ˈæŋəl": the coda spelling answers /ŋ/, which is right at the end of a word —
            // "sing", "long" — and wrong here, where English says both sounds.
            if (Hardens(syllabic, i, count, syllable.Coda))
            {
                built.Append('ɡ');
            }
        }

        return built.ToString();
    }

    /// <summary>
    /// A syllable that is nothing but the letter <c>e</c> — no onset, no coda — which is what the parse
    /// makes of a silent final one.
    /// </summary>
    private static bool IsSilentE(Syllable syllable) =>
        syllable.Onset.Length == 0 && syllable.Vowel == "e" && syllable.Coda.Length == 0;

    /// <summary>
    /// A final syllable that ends in an <c>l</c> or an <c>r</c> and an <c>e</c> — which is what the
    /// parse makes of <c>-ble</c>, <c>-tle</c>, <c>-cre</c> and their family, because the consonant in
    /// front closes the syllable before it and leaves the <c>l</c> to open this one.
    /// </summary>
    private static bool IsSyllabic(Syllable syllable) =>
        syllable.Coda.Length == 0
        && syllable.Vowel == "e"
        && syllable.Onset.Length > 0
        && syllable.Onset[^1] is 'l' or 'r';

    /// <summary>
    /// Whether this syllable's coda is the <c>ng</c> standing in front of a syllabic <c>l</c>, which
    /// keeps its /ɡ/ (#184) — the <c>ngle</c> of single, angle and jungle.
    /// </summary>
    private static bool Hardens(bool syllabic, int at, int count, string coda) =>
        syllabic && at == count - 2 && coda.EndsWith("ng", StringComparison.Ordinal);

    /// <summary>A coda as it is spelled, with a lone <c>h</c> dropped (#179).</summary>
    private static string Coda(string coda) => coda == "h" ? string.Empty : coda;

    /// <summary>Whether a silent <c>e</c> reaches back over this coda to lengthen the vowel.</summary>
    private static bool Lengthens(string coda) => coda.Length == 1 || OneSound.Contains(coda);

    /// <summary><c>c</c> to /s/ and <c>g</c> to /dʒ/ where a silent <c>e</c> stood behind them.</summary>
    private static string SoftenCoda(string coda) => coda.Length == 0 ? coda : coda[^1] switch
    {
        'c' when !coda.EndsWith("ch", StringComparison.Ordinal) => coda[..^1] + "s",
        'g' when !coda.EndsWith("gh", StringComparison.Ordinal)
                 && !coda.EndsWith("ng", StringComparison.Ordinal) => coda[..^1] + "j",
        _ => coda,
    };

    /// <summary>A consonant run to IPA, longest spelling first.</summary>
    private static string Spell(string letters)
    {
        if (letters.Length == 0)
        {
            return string.Empty;
        }

        var built = new System.Text.StringBuilder();
        var at = 0;

        while (at < letters.Length)
        {
            var matched = false;

            foreach (var (spelling, ipa) in Consonants)
            {
                if (at + spelling.Length > letters.Length ||
                    string.CompareOrdinal(letters, at, spelling, 0, spelling.Length) != 0)
                {
                    continue;
                }

                built.Append(ipa);
                at += spelling.Length;
                matched = true;
                break;
            }

            if (!matched)
            {
                at++;
            }
        }

        return built.ToString();
    }

    /// <summary>Whether the letter at <paramref name="index"/> is soft-making.</summary>
    private static bool Softens(string vowel) =>
        vowel.Length > 0 && vowel[0] is 'e' or 'i' or 'y';

    /// <summary><c>c</c> to /s/ and <c>g</c> to /dʒ/ where the syllable's vowel softens them.</summary>
    public static string SoftenOnset(string onset, string vowel)
    {
        if (onset.Length == 0 || !Softens(vowel))
        {
            return onset;
        }

        return onset[^1] switch
        {
            'c' when !onset.EndsWith("ch", StringComparison.Ordinal) => onset[..^1] + "s",
            'g' when !onset.EndsWith("gh", StringComparison.Ordinal)
                     && !onset.EndsWith("ng", StringComparison.Ordinal) => onset[..^1] + "j",
            _ => onset,
        };
    }
}
