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
    /// Consonant spellings of two letters that are one sound, which is what decides whether a
    /// silent <c>e</c> lengthens the vowel in front of it. One sound between the vowel and the
    /// <c>e</c> lengthens — <em>Lave</em>, <em>bathe</em> — and two do not: <em>serve</em>,
    /// <em>dense</em>, <em>paste</em> are all short, which is why the rule counts sounds rather
    /// than letters.
    /// </summary>
    private static readonly HashSet<string> OneSound =
        new(StringComparer.Ordinal) { "ch", "ck", "gh", "ph", "sh", "th", "zh" };

    /// <summary>
    /// The word in IPA, or null where it cannot be said and must be spelled instead.
    /// <para>
    /// Stress is marked on the first syllable of a multi-syllable word and nowhere else. That is
    /// crude and it is deliberate: English stress on an invented name is not recoverable from its
    /// spelling, first-syllable is the commonest English pattern, and a wrong mark is less audible
    /// than no mark at all, which makes the whole name flat.
    /// </para>
    /// <para>
    /// <b>Which syllable is marked is that judgement; where in the syllable the mark goes is not.</b>
    /// It goes immediately before the vowel, which is the one convention the shipped dictionary
    /// follows without a single exception — see the note at the mark itself for what putting it
    /// anywhere else sounded like.
    /// </para>
    /// </summary>
    public static string? Pronounce(string? word)
    {
        var syllables = Phonotactics.Syllabify(word);

        if (syllables.Count == 0)
        {
            return null;
        }

        // <b>A final e after a consonant is silent</b> (#153, reported 2026-08-28). The parse
        // hands it back as a syllable of its own — "lav.e", "obs.erv.e" — and every one of them
        // was then voiced by the reduction rule below, which is right for the a of "Dezhra" and
        // wrong for every English word that ends this way. Half the proper nouns in the galaxy
        // end this way too, so "Lave" was "lav-uh" the day it missed the dictionary.
        //
        // Said as a count rather than by rebuilding the list, so the mark, the reduction and the
        // last-syllable test all read the same as they did.
        var silentE = syllables.Count > 1 && IsSilentE(syllables[^1]);
        var count = silentE ? syllables.Count - 1 : syllables.Count;

        var built = new System.Text.StringBuilder();

        for (var i = 0; i < count; i++)
        {
            var syllable = syllables[i];

            built.Append(Spell(SoftenOnset(syllable.Onset, syllable.Vowel)));

            // <b>An unstressed final vowel reduces.</b> English does this to nearly every word
            // that ends in one: the a of "Dezhra" is a schwa, not the a of "day", and a final
            // y is /i/ rather than /aɪ/. Without this every invented name ends on a diphthong
            // and they all sound like the same name.
            var last = i == count - 1 && count > 1;

            // <b>And the e it was hiding behind lengthens what it left.</b> That is the other
            // half of the same rule: the e in "Lave" is not merely silent, it is what makes the
            // a say its own name. It only reaches across one sound, which is why "serve" and
            // "paste" stay short — see <see cref="OneSound"/>.
            var lengthens = silentE && i == count - 1 && Lengthens(syllable.Coda);

            var vowel =
                last && syllable.IsOpen && syllable.Vowel == "y" ? "i"
                : last && syllable.IsOpen && Reduces.Contains(syllable.Vowel) ? "ə"
                : (syllable.IsOpen || lengthens)
                  && Long.TryGetValue(syllable.Vowel, out var open) ? open
                : Short.GetValueOrDefault(syllable.Vowel, syllable.Vowel);

            // <b>A leading glide belongs to the onset, not to the nucleus.</b> Four vowel spellings
            // here answer with one — <c>eu</c>, <c>ew</c> and a long <c>u</c> all give <c>juː</c> —
            // so marking the start of the vowel <em>string</em> would put the mark back in front of
            // a consonant for exactly those words, which is the fault being fixed reached by a
            // different road. The dictionary is unambiguous: <c>jˈuːnɪt</c>, <c>mjˈuːzɪk</c>,
            // <c>fjˈuː</c>, <c>kjˈuːt</c> — glide, then mark, then vowel.
            var glide = vowel.Length > 1 && vowel[0] is 'j' or 'w' ? 1 : 0;

            built.Append(vowel[..glide]);

            // <b>The mark goes immediately before the stressed vowel, never before the consonants
            // in front of it</b> (reported 2026-08-28, against the local voice shipped in 0.84.0).
            //
            // It used to sit at the head of the syllable — <c>ˈdɛpæɹæɡɑːn</c> — and the shipped
            // dictionary does not do that <b>once in 274,927 entries</b>: not a single one begins
            // with a stress mark followed by a consonant. It writes <c>dʒˈɑːn</c> and
            // <c>tˈɜːmɪnəl</c>, marking the vowel rather than the syllable. So every word these
            // rules answered for reached Kokoro in a shape it had never once been given, and Kokoro
            // rendered the unfamiliar shape as an extra vowel: <em>"JOHN ay DEPARAGON is in ay
            // Kamitra, near ay Hammel Terminal"</em>.
            //
            // <b>The reported line is what makes the cause certain rather than likely.</b> Every
            // word an intruded vowel was heard before — Deparagon, Kamitra, Hammel, Hammel — is a
            // word these rules answered for, and every word without one — John, is, in, near,
            // Terminal, docked, at — came from the dictionary. Four for four and seven for seven,
            // in one sentence.
            //
            // It costs a name nothing to be marked the dictionary's way, and names are most of what
            // d47 says: systems, stations, Commanders. A vowel-initial word is unaffected, because
            // there the two positions are the same one — which is exactly the 5.7% of dictionary
            // entries that do begin with the mark, every one of them with a vowel after it.
            if (i == 0 && count > 1)
            {
                built.Append('ˈ');
            }

            built.Append(vowel[glide..]);

            // The silent e softens as well as lengthens — <em>ace</em> is not <em>ake</em> and
            // <em>page</em> is not <em>pag</em>. Same rule as the onset's, applied at the other
            // end of the syllable because that is the end the e was standing at.
            built.Append(Spell(
                silentE && i == count - 1 ? SoftenCoda(syllable.Coda) : syllable.Coda));
        }

        return built.ToString();
    }

    /// <summary>
    /// A syllable that is nothing but the letter <c>e</c> — no onset, no coda — which is what the
    /// parse makes of a silent final one. <c>-le</c> and <c>-re</c> are deliberately not this:
    /// they carry an onset, and their e is a syllable an English speaker says.
    /// </summary>
    private static bool IsSilentE(Syllable syllable) =>
        syllable.Onset.Length == 0 && syllable.Vowel == "e" && syllable.Coda.Length == 0;

    /// <summary>
    /// Whether a silent <c>e</c> reaches back over this coda to lengthen the vowel. One sound
    /// does — a single letter, or one of the digraphs that spell one — and more than one does not.
    /// </summary>
    private static bool Lengthens(string coda) => coda.Length == 1 || OneSound.Contains(coda);

    /// <summary>
    /// <c>c</c> to /s/ and <c>g</c> to /dʒ/ where a silent <c>e</c> stood behind them. The onset's
    /// rule read from the other end: <see cref="SoftenOnset"/> asks what vowel follows, and here
    /// the vowel that follows is the one being dropped.
    /// </summary>
    private static string SoftenCoda(string coda) => coda.Length == 0 ? coda : coda[^1] switch
    {
        'c' when !coda.EndsWith("ch", StringComparison.Ordinal) => coda[..^1] + "s",
        'g' when !coda.EndsWith("gh", StringComparison.Ordinal)
                 && !coda.EndsWith("ng", StringComparison.Ordinal) => coda[..^1] + "j",
        _ => coda,
    };

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
