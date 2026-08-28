namespace D47.Core.Speech;

/// <summary>
/// Spelling to IPA for a word no dictionary holds, by rule.
/// <para>
/// <b>For an invented name there is no correct answer, only a consistent one</b>, and that is the
/// whole argument for rules over a model here. A net asked for <c>Shinrarta</c> produces something
/// confidently wrong and differently wrong next time; rules produce the same thing every time, and
/// when a Commander says it is wrong there is a line to change.
/// </para>
/// <para>
/// Syllable by syllable, because the same letter is two sounds depending on what closes it: the
/// <c>a</c> in <c>La-ve</c> is not the <c>a</c> in <c>Lav</c>. <see cref="Phonotactics.Syllabify"/>
/// has already decided where those breaks are, so this does not re-derive them.
/// </para>
/// </summary>
public static class LetterToSound
{
    /// <summary>
    /// Consonant spellings to IPA, longest first. <c>zh</c> is here because the Commander ruled
    /// that sounds an English speaker can make are spoken rather than spelled, and it is the sound
    /// in <em>measure</em>.
    /// </summary>
    private static readonly (string Spelling, string Ipa)[] Consonants =
    [
        ("sch", "ʃ"), ("shr", "ʃɹ"), ("zhr", "ʒɹ"), ("thr", "θɹ"), ("chr", "kɹ"),
        ("phr", "fɹ"), ("spl", "spl"), ("spr", "spɹ"), ("str", "stɹ"), ("scr", "skɹ"),
        ("squ", "skw"), ("sph", "sf"),

        // A doubled consonant is one sound. Without these the letters are matched singly and
        // "well" comes out with two /l/ in it.
        ("bb", "b"), ("cc", "k"), ("dd", "d"), ("ff", "f"), ("gg", "ɡ"), ("kk", "k"),
        ("ll", "l"), ("mm", "m"), ("nn", "n"), ("pp", "p"), ("rr", "ɹ"), ("ss", "s"),
        ("tt", "t"), ("zz", "z"),

        ("ch", "tʃ"), ("ck", "k"), ("dge", "dʒ"), ("gh", "ɡ"), ("gn", "n"), ("kn", "n"),
        ("ng", "ŋ"), ("ph", "f"), ("ps", "s"), ("qu", "kw"), ("rh", "ɹ"), ("sh", "ʃ"),
        ("th", "θ"), ("tch", "tʃ"), ("wh", "w"), ("wr", "ɹ"), ("zh", "ʒ"),

        ("b", "b"), ("c", "k"), ("d", "d"), ("f", "f"), ("g", "ɡ"), ("h", "h"),
        ("j", "dʒ"), ("k", "k"), ("l", "l"), ("m", "m"), ("n", "n"), ("p", "p"),
        ("q", "k"), ("r", "ɹ"), ("s", "s"), ("t", "t"), ("v", "v"), ("w", "w"),
        ("x", "ks"), ("y", "j"), ("z", "z"),
    ];

    /// <summary>
    /// Vowel spellings, in their closed reading — the short one, which is what a coda produces.
    /// </summary>
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
    /// syllable produces. Only the five need it; every digraph above already says its own length.
    /// </summary>
    private static readonly Dictionary<string, string> Long = new(StringComparer.Ordinal)
    {
        ["a"] = "eɪ", ["e"] = "iː", ["i"] = "aɪ", ["o"] = "oʊ", ["u"] = "juː", ["y"] = "aɪ",
    };

    /// <summary>
    /// The single vowels that reduce to a schwa when they end an unstressed final syllable.
    /// <c>i</c> and <c>y</c> are not here: they go to /i/, which is a different reduction.
    /// </summary>
    private static readonly HashSet<string> Reduces = ["a", "e", "o", "u"];

    /// <summary>
    /// The word in IPA, or null where it cannot be said and must be spelled instead.
    /// <para>
    /// Stress is marked on the first syllable of a multi-syllable word and nowhere else. That is
    /// crude and it is deliberate: English stress on an invented name is not recoverable from its
    /// spelling, first-syllable is the commonest English pattern, and a wrong mark is less audible
    /// than no mark at all, which makes the whole name flat.
    /// </para>
    /// </summary>
    public static string? Pronounce(string? word)
    {
        var syllables = Phonotactics.Syllabify(word);

        if (syllables.Count == 0)
        {
            return null;
        }

        var built = new System.Text.StringBuilder();

        for (var i = 0; i < syllables.Count; i++)
        {
            if (i == 0 && syllables.Count > 1)
            {
                built.Append('ˈ');
            }

            var syllable = syllables[i];

            built.Append(Spell(SoftenOnset(syllable.Onset, syllable.Vowel)));

            // <b>An unstressed final vowel reduces.</b> English does this to nearly every word
            // that ends in one: the a of "Dezhra" is a schwa, not the a of "day", and a final
            // y is /i/ rather than /aɪ/. Without this every invented name ends on a diphthong
            // and they all sound like the same name.
            var last = i == syllables.Count - 1 && syllables.Count > 1;

            var vowel =
                last && syllable.IsOpen && syllable.Vowel == "y" ? "i"
                : last && syllable.IsOpen && Reduces.Contains(syllable.Vowel) ? "ə"
                : syllable.IsOpen && Long.TryGetValue(syllable.Vowel, out var open) ? open
                : Short.GetValueOrDefault(syllable.Vowel, syllable.Vowel);

            built.Append(vowel);
            built.Append(Spell(syllable.Coda));
        }

        return built.ToString();
    }

    /// <summary>
    /// A consonant run to IPA, longest spelling first.
    /// <para>
    /// <c>c</c> and <c>g</c> soften before <c>e</c>, <c>i</c> and <c>y</c> — <em>Ceres</em> is not
    /// <em>Keres</em> — which is the one context rule worth having, because it is the one an
    /// English reader applies without thinking and would hear broken immediately.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Whether the letter at <paramref name="index"/> is soft-making. Used by
    /// <see cref="SoftenOnset"/>, which is applied where the vowel is known.
    /// </summary>
    private static bool Softens(string vowel) =>
        vowel.Length > 0 && vowel[0] is 'e' or 'i' or 'y';

    /// <summary>
    /// <c>c</c> to /s/ and <c>g</c> to /dʒ/ where the syllable's vowel softens them. Applied to the
    /// onset only, and only to a run whose last letter is the one being softened — <c>ch</c> is
    /// already its own sound and must not be touched.
    /// </summary>
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
