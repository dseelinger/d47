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

        // <b>The silent gh</b> (#184). Ahead of "gh", which would otherwise answer /ɡ/ and leave
        // the t to be matched on its own — and ahead of everything, because a coda is only ever
        // reached by <see cref="Spell"/> scanning this array from the top. See
        // <see cref="SilentGh"/> for what a word this could not say used to sound like.
        ("ght", "t"),

        // A doubled consonant is one sound. Without these the letters are matched singly and
        // "well" comes out with two /l/ in it.
        ("bb", "b"), ("cc", "k"), ("dd", "d"), ("ff", "f"), ("gg", "ɡ"), ("kk", "k"),
        ("ll", "l"), ("mm", "m"), ("nn", "n"), ("pp", "p"), ("rr", "ɹ"), ("ss", "s"),
        ("tt", "t"), ("zz", "z"),

        ("ch", "tʃ"), ("ck", "k"), ("dge", "dʒ"), ("gh", "ɡ"), ("gn", "n"), ("kn", "n"),

        // <b>Ahead of "ng", which used to swallow it and drop the e</b> (#179). "change" came out
        // "tʃæŋ": the coda parses — "nge" is one — but with no spelling for it the letters matched
        // singly, "ng" answered first, and the /dʒ/ vanished with nothing to say the e. The n is
        // /n/ rather than /ŋ/ in front of the affricate, which is what the dictionary writes:
        // "tʃˈeɪndʒ", "hˈɪndʒ", "spˈʌndʒ".
        ("nge", "ndʒ"),

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
    /// <para>
    /// <b><c>ck</c> was on this list and is not any more</b> (#184). It is one sound, so it
    /// belonged here by the rule as stated — and it lengthened nothing in English, because
    /// <c>ck</c> is what English writes to say a vowel is <em>short</em>, exactly as <c>dge</c> is.
    /// It cost little through the silent <c>e</c>, which almost never stands behind one, and a
    /// great deal through the syllabic <c>-le</c> that reaches the same test: <em>tickle</em> was
    /// <c>tˈaɪkəl</c>, <em>tackle</em> was <c>tˈeɪkəl</c> and <em>suckle</em> was <c>sjˈuːkəl</c>.
    /// </para>
    /// <para>
    /// <b>Counted rather than judged, on 2026-08-29.</b> Of the shipped dictionary's 65 entries
    /// ending <c>-ckle</c>, <b>64 are short</b> — the one exception is <em>ickle</em>. And of the
    /// 13 entries that end in a vowel and <c>cke</c>, which is the silent-<c>e</c> side of the same
    /// test, <b>every single one is short</b>: <em>becke</em> is <c>bˈɛk</c>, <em>pecke</em> is
    /// <c>pˈɛk</c>. So the row was wrong on both roads it reached.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> OneSound =
        new(StringComparer.Ordinal) { "ch", "gh", "ph", "sh", "th", "zh" };

    /// <summary>
    /// <b>The silent <c>gh</c></b> (#184). <c>ght</c> is /t/ and the vowel in front of it is long:
    /// <em>light</em>, <em>night</em>, <em>fought</em>.
    /// <para>
    /// <b>Before this there was no such coda at all, so the word could not be said</b> — the parse
    /// tried <c>gh</c>, was left with a <c>t</c> that could begin no syllable, and gave up. A word
    /// the rules cannot say is spelled, so on a build whose dictionary failed to download,
    /// <em>light</em> was <em>ell eye gee aitch tee</em>. #155 rewrites <c>ly</c> to <em>light
    /// years</em> before any provider sees it, so that is a word d47 now says constantly.
    /// </para>
    /// <para>
    /// <b>The length is measured, not judged.</b> Of the dictionary's entries ending in a single
    /// <c>i</c> and <c>ght</c>, <b>181 of 182</b> are /aɪt/ — the exception is <em>anight</em>. A
    /// digraph needs no rule and deliberately gets none: <c>ei</c> and <c>au</c> already carry
    /// their own length, which is why <em>weight</em>, <em>freight</em>, <em>caught</em> and
    /// <em>taught</em> come out right without being special cases — and they are the whole of the
    /// apparent counter-evidence, since the 34 <c>-ight</c> entries that are not /aɪt/ are almost
    /// all <em>weight</em> and <em>freight</em> compounds.
    /// </para>
    /// </summary>
    private const string SilentGh = "ght";

    /// <summary>
    /// The one vowel that reads differently in front of <see cref="SilentGh"/>: <c>ou</c> is /ɔː/
    /// rather than its usual /aʊ/, so <em>fought</em>, <em>bought</em> and <em>thought</em> are
    /// /ɔːt/.
    /// <para>
    /// <b>Counted like the rest:</b> 54 of the dictionary's 56 <c>-ought</c> entries are /ɔːt/, and
    /// the two that are not are <em>drought</em> and <em>dought</em>. Every other spelling of
    /// <c>ough</c> — <em>tough</em>, <em>through</em>, <em>though</em>, <em>plough</em> — ends
    /// <c>gh</c> rather than <c>ght</c>, so none of them reaches this rule and none of them is
    /// evidence against it.
    /// </para>
    /// </summary>
    private static string? Rereads(string coda, string vowel) =>
        coda == SilentGh && vowel == "ou" ? "ɔː" : null;

    /// <summary>
    /// <b>The three codas that carry their own silent <c>e</c> inside them</b> — <c>nge</c>,
    /// <c>dge</c> and <c>ze</c> — and whether that <c>e</c> lengthens the vowel in front of it
    /// (#179).
    /// <para>
    /// These are the codas <see cref="Phonotactics"/> admits whole, so the parse never hands the
    /// <c>e</c> back as a syllable of its own and the silent-e rule above never sees it. Whether
    /// they lengthen is the question #179 was left open on, because the examples pull both ways:
    /// <em>change</em> and <em>maze</em> lengthen, <em>hinge</em> and <em>bridge</em> do not.
    /// </para>
    /// <para>
    /// <b>Settled by counting the shipped dictionary rather than by ear, on 2026-08-29.</b> Every
    /// entry ending in a single vowel and one of these three, by what the dictionary puts in front
    /// of the final consonant:
    /// </para>
    /// <list type="bullet">
    /// <item><c>ze</c> — <b>1,227 long of 1,236</b> (99.3%). <em>maze</em>, <em>size</em>,
    /// <em>doze</em>, <em>prize</em>. It lengthens.</item>
    /// <item><c>dge</c> — <b>74 short of 76</b> (97.4%). <em>badge</em>, <em>bridge</em>,
    /// <em>judge</em>, <em>hedge</em>. It does not, which is the whole reason the spelling exists:
    /// <c>dge</c> is how English writes a short vowel before /dʒ/.</item>
    /// <item><c>nge</c> — <b>it depends on the vowel, and only on the vowel.</b> After <c>a</c> it
    /// is long, 25 of 32 (<em>change</em>, <em>range</em>, <em>strange</em>, <em>arrange</em>, and
    /// the seven exceptions are French loans plus <em>orange</em> and <em>flange</em>). After every
    /// other single vowel it is short, 57 of 58 — <em>hinge</em>, <em>fringe</em>, <em>sponge</em>,
    /// <em>plunge</em>, <em>revenge</em>.</item>
    /// </list>
    /// <para>
    /// <b>A digraph is deliberately not asked about</b> and needs no row: <c>au</c>, <c>ou</c> and
    /// <c>ee</c> already carry their own length in <see cref="Short"/>, which is why
    /// <em>lounge</em> and <em>chaunge</em> come out right without being special cases. They are
    /// also the whole of the apparent counter-evidence — every "long" reading under a vowel other
    /// than <c>a</c> in that count was one of them.
    /// </para>
    /// </summary>
    private static bool CarriesASilentE(string coda, string vowel) => coda switch
    {
        "ze" => true,
        "nge" => vowel == "a",
        _ => false,
    };

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

        // <b>A final -le or -re is a syllabic consonant, not a consonant and a schwa</b> (#179).
        // "table" was "tˈæblə" rather than "tˈeɪbəl": the parse hands back "ta.ble", the l is that
        // syllable's onset, and the reduction rule below then voiced the e after it. English says
        // the schwa first and the consonant second — and the e, like a silent one, lengthens what
        // it left behind, which is the other half of why "table" is not "tabble".
        var syllabic = syllables.Count > 1 && IsSyllabic(syllables[^1]);

        // <b>And whatever else stands in that onset stays in front of the schwa</b> (#184). #179
        // asked the onset to be exactly "l" or "r", which is what the parse hands back only when
        // nothing before the l could close the syllable before it — "ta.ble", "ac.re". Where two
        // consonants stand there and the pair is a legal onset but not a legal coda, the parse
        // hands back "un.cle" and "mus.cle" instead, the test missed them, and #179's own defect
        // survived in exactly those words: "uncle" was "ˈʌnklə". The consonants ahead of the l are
        // the previous syllable's to say — /ʌn.kəl/ — so they are spelled before the schwa and the
        // l alone after it.
        var ahead = syllabic ? syllables[^1].Onset[..^1] : string.Empty;

        // Which syllable the trailing e — silent, or standing behind a syllabic l — reaches back
        // over. Nothing, where there is no such e.
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

            // <b>An unstressed final vowel reduces.</b> English does this to nearly every word
            // that ends in one: the a of "Dezhra" is a schwa, not the a of "day", and a final
            // y is /i/ rather than /aɪ/. Without this every invented name ends on a diphthong
            // and they all sound like the same name.
            var last = i == count - 1 && count > 1;

            // <b>And the e it was hiding behind lengthens what it left.</b> That is the other
            // half of the same rule: the e in "Lave" is not merely silent, it is what makes the
            // a say its own name. It only reaches across one sound, which is why "serve" and
            // "paste" stay short — see <see cref="OneSound"/>.
            //
            // <b>The sounds it reaches across include the ones ahead of a syllabic l</b> (#184),
            // because the parse put them in the next syllable's onset and the ear puts them here:
            // the cluster between the vowel of "uncle" and its l is "nc" whichever side of the
            // break the c was written on, and two sounds do not lengthen. Without this the
            // generalisation above would have made "uncle" "jˈuːnkəl".
            //
            // And "ght" lengthens wherever it stands rather than only where an e reaches back
            // over it (#184) — there is no e in "light", and the length is the coda's own.
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
            built.Append(Spell(Coda(
                silentE && i == count - 1 ? SoftenCoda(syllable.Coda) : syllable.Coda)));

            // <b>And "ng" keeps its /ɡ/ in front of a syllabic l</b> (#184). "single" was "sˈɪŋəl"
            // and "angle" was "ˈæŋəl": the coda spelling answers /ŋ/, which is right at the end of
            // a word — "sing", "long" — and wrong here, where English says both sounds. The /ɡ/ is
            // appended rather than written into the coda's spelling because it belongs to this
            // shape and not to "ng" generally, and because a second spelling of "ng" would have to
            // agree with the first.
            //
            // Counted, like the rest of this file: <b>63 of the dictionary's 64</b> entries ending
            // "-ngle" have /ŋɡ/, the exception being "comingle", which is a prefix on "mingle".
            if (Hardens(syllabic, i, count, syllable.Coda))
            {
                built.Append('ɡ');
            }
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
    /// A final syllable that ends in an <c>l</c> or an <c>r</c> and an <c>e</c> — which is what the
    /// parse makes of <c>-ble</c>, <c>-tle</c>, <c>-cre</c> and their family, because the consonant
    /// in front closes the syllable before it and leaves the <c>l</c> to open this one.
    /// <para>
    /// <b>The onset used to have to be that single letter, and #184 widened it to any onset ending
    /// in one.</b> Where two consonants stand between the vowel and the <c>l</c>, whether the parse
    /// hands the first one back as a coda or keeps the pair as an onset depends on which of them
    /// <see cref="Phonotactics"/> admits — <c>st</c> is a coda so <em>castle</em> is <em>cas.tle</em>
    /// and was caught, while <c>nc</c> is not, so <em>uncle</em> is <em>un.cle</em> and was missed.
    /// The two words are the same shape to a listener, and #179's defect survived in the second one
    /// unchanged: <em>uncle</em> was <c>ˈʌnklə</c>, <em>muscle</em> was <c>mˈʌsklə</c>, and
    /// <em>centre</em> was <c>sˈɛntɹə</c>.
    /// </para>
    /// <para>
    /// There must still be an onset: <c>-ale</c> and <c>-ole</c> hand their <c>e</c> back bare and
    /// are <see cref="IsSilentE"/>'s, not this.
    /// </para>
    /// </summary>
    private static bool IsSyllabic(Syllable syllable) =>
        syllable.Coda.Length == 0
        && syllable.Vowel == "e"
        && syllable.Onset.Length > 0
        && syllable.Onset[^1] is 'l' or 'r';

    /// <summary>
    /// Whether this syllable's coda is the <c>ng</c> standing in front of a syllabic <c>l</c>,
    /// which keeps its /ɡ/ (#184) — the <c>ngle</c> of <em>single</em>, <em>angle</em> and
    /// <em>jungle</em>.
    /// </summary>
    private static bool Hardens(bool syllabic, int at, int count, string coda) =>
        syllabic && at == count - 2 && coda.EndsWith("ng", StringComparison.Ordinal);

    /// <summary>
    /// A coda as it is spelled, with a lone <c>h</c> dropped (#179).
    /// <para>
    /// <b>English has no coda /h/ at all</b>, and the rules were producing one: <c>tah</c> was
    /// <c>tæh</c>. It bites hardest through the Commander's own pronunciations file
    /// (<a href="https://github.com/dseelinger/d47/issues/150">#150</a>), where <em>tah</em> and
    /// <em>rah</em> are exactly how a person writes a syllable down — so anybody using that file
    /// meets it on their first entry.
    /// </para>
    /// <para>
    /// Only a lone one. <c>gh</c>, <c>sh</c>, <c>th</c>, <c>ch</c>, <c>ph</c> and <c>zh</c> are
    /// codas that end in the letter and are not the sound, and dropping the h out of those would
    /// break far more than it fixed. The syllable stays closed, so its vowel stays short — a
    /// Commander who wants the broad /ɑː/ of <em>ah</em> writes the symbol, which is the one road
    /// that rung never second-guesses.
    /// </para>
    /// </summary>
    private static string Coda(string coda) => coda == "h" ? string.Empty : coda;

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
