namespace D47.Core.Speech;

/// <summary>A word's pronunciation, or nothing. Implemented by the shipped dictionary.</summary>
public interface IPronunciationDictionary
{
    /// <summary>The IPA for a word, or null where it is not held.</summary>
    string? Lookup(string word);
}

/// <summary>
/// Which rung of the ladder a segment came off (#153).
/// <para>
/// <b>One debug line naming this would have made #153 a read instead of an investigation.</b>
/// Three words came out wrong on the same day and two of them failed differently — one was ruled
/// where it should have been looked up, two were spelled where they should have been ruled — and
/// nothing in the log said which. The rung is the whole diagnosis, so it is the whole line.
/// </para>
/// </summary>
public enum PhonemeRung
{
    /// <summary>The Commander's own override file, which wins over everything.</summary>
    Override,

    /// <summary>The shipped dictionary.</summary>
    Dictionary,

    /// <summary>A word with an apostrophe inside it, built from its stem.</summary>
    Contraction,

    /// <summary>All digits, said as a number.</summary>
    Number,

    /// <summary>Letters and digits with nothing between them, so a designation and spelled.</summary>
    Designation,

    /// <summary>Letters that parse as English syllables, said by rule.</summary>
    Rules,

    /// <summary>Anything left, spelled out one character at a time.</summary>
    Spelled,
}

/// <summary>
/// Text to the phonemes a local voice can speak, by a ladder that never guesses.
/// <para>
/// <b>Kokoro takes phonemes and has no text path at all</b> — 115 IPA symbols and punctuation — so
/// this is not an optimisation, it is the whole of what stands between d47 and a local voice. Every
/// other provider does this inside its own service, invisibly.
/// </para>
/// <para>
/// <b>The ladder, ruled by the Commander on 2026-08-28.</b> Split on whitespace and dashes, then
/// per segment:
/// </para>
/// <list type="number">
/// <item>a word the Commander has corrected in <see cref="PronunciationOverrides"/> is said their
/// way, which is the one rung nothing outranks (#150);</item>
/// <item>a word the dictionary holds is said the dictionary's way;</item>
/// <item>a word with an apostrophe inside it is built from its stem — see
/// <see cref="Contractions"/>, added on the day the voice was first heard aloud saying
/// <em>ess aitch eye pee ess</em> for <c>Ship's</c>;</item>
/// <item>digits are said as a number — <c>385</c> is <em>three eighty-five</em>;</item>
/// <item>letters and digits mixed with nothing between them are spelled — <c>B0</c> is
/// <em>bee zero</em>;</item>
/// <item>letters that parse as English syllables are pronounced by rule;</item>
/// <item>anything left is spelled out.</item>
/// </list>
/// <para>
/// So <c>COL 385 SECTOR B0-GQPI</c> comes out roughly <em>call three eighty-five sector bee zero
/// dash gee queue pee eye</em>.
/// </para>
/// <para>
/// <b>The dash is spoken only when it is separating things that had to be spelled.</b> That
/// refinement is not in the original ruling and was taken while building, because without it every
/// ordinary hyphenated word in d47's prose — <em>well-known</em>, <em>re-entry</em> — would have a
/// spoken <em>dash</em> in the middle of it. Where both sides are words, the dash is a compound's
/// joint and is silent; where either side is being spelled, it is part of the designation and is
/// said. The Commander's own example still says its dash, because <c>B0</c> and <c>GQPI</c> are
/// both spelled.
/// </para>
/// <para>
/// <b>What a model writes around a word is not part of the word</b> (#153). Markdown emphasis,
/// curly quotes, an em dash and an ellipsis all reach here in ordinary d47 prose, and a segment
/// wearing one of them is not <c>All(char.IsLetter)</c> — so it skipped the dictionary
/// <em>and</em> the rules and was spelled out. <c>**Guardian modules**</c> was read as
/// <em>gee, you, ay, ar, dee, eye, ay, en</em>. Those marks are stripped before the ladder sees
/// anything, and the ones Kokoro reads as phrasing are carried through beside the word.
/// </para>
/// </summary>
public sealed class Phonemiser(
    IPronunciationDictionary? dictionary = null,
    PronunciationOverrides? overrides = null,
    Action<string, PhonemeRung, string>? note = null)
{
    /// <summary>What a segment turned out to be, which is what decides the dash beside it.</summary>
    private enum Reading
    {
        Spoken,
        Spelled,
    }

    /// <summary>The dash, said. Only reached where a segment beside it had to be spelled.</summary>
    private const string Dash = "dˈæʃ";

    /// <summary>
    /// Marks a model writes around a word that are neither part of it nor phrasing. Markdown
    /// emphasis is the one that cost the most: d47's own prose is full of it — <c>**Guardian
    /// modules**</c>, <c>**Kuk**</c> — and every emphasised word in it was spelled out letter by
    /// letter. Stripped from both ends and never spoken, because there is no sound for bold.
    /// </summary>
    private static readonly char[] Decoration = ['*', '_', '`', '~'];

    /// <summary>
    /// Punctuation that ends a token and is kept, because Kokoro reads it as phrasing and its
    /// vocabulary contains every one of these. The curly closers and the ellipsis were the gap:
    /// the straight ones were trimmed and their curly twins were not, so a quoted word was spelled.
    /// </summary>
    private static readonly char[] Closers =
        ['.', ',', '!', '?', ';', ':', ')', ']', '"', '\'', '”', '…', '—', '–'];

    /// <summary>The same at the front of a token, where the mark is dropped rather than kept.</summary>
    private static readonly char[] Openers = ['(', '[', '"', '\'', '“', '‘'];

    /// <summary>
    /// Both at once, which is what a token actually wears: <c>**Guardian modules**.</c> ends
    /// <c>**.</c>, so trimming the decoration and then the punctuation strips neither — the full
    /// stop stops the first pass and the asterisks stop the second. Trimmed together and sorted
    /// out afterwards.
    /// </summary>
    private static readonly char[] Trailing = [.. Closers, .. Decoration];

    private static readonly char[] Leading = [.. Openers, .. Decoration];

    /// <summary>
    /// Every dash a compound can be written with. The rule below is about the joint rather than
    /// the character, and a model reaches for the long ones as freely as a Commander reaches for
    /// the short one.
    /// </summary>
    private static readonly char[] Dashes = ['-', '–', '—'];

    /// <summary>
    /// One line of text as IPA for <paramref name="voiceId"/>'s accent.
    /// <para>
    /// Punctuation is carried through rather than dropped: Kokoro's vocabulary contains it, and it
    /// is what the model reads as phrasing. A line stripped of its commas is said in one breath.
    /// </para>
    /// </summary>
    public string ToPhonemes(string? text, string? voiceId = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Before anything is read: the Commander may have corrected a word since the last line was
        // spoken. Once per utterance rather than once per word, and a stat rather than a read
        // unless the file has actually moved — see PronunciationOverrides.Refresh.
        overrides?.Refresh();

        return Say(text, SpokenLetters.AccentOf(voiceId), overrides);
    }

    /// <summary>
    /// One line, with <paramref name="layer"/> as its top rung. Null for the respellings in that
    /// layer, which go down the rest of the ladder as if they were the text — and must not consult
    /// the overrides again, or an entry naming itself would never come back.
    /// </summary>
    private string Say(string text, SpeechAccent accent, PronunciationOverrides? layer)
    {
        var built = new System.Text.StringBuilder();

        // A model writes the curly one and a Commander types the straight one, and they are the
        // same word. Settled here rather than in five places further down.
        //
        // Mark and class numerals are written out before the split, and they have to be: the
        // context spans tokens, so `Mk II` arrives as two segments and `MkII` as one, and the
        // per-segment ladder below can see neither pair (#138). What comes back is ordinary
        // English, so "Mark three" then goes down the ladder out of the dictionary like any other
        // words rather than through a second set of rules that could disagree with it.
        var prepared = SpokenNumerals.Expand(text.Replace('’', '\''));

        // Trimmed up front rather than inside the ladder, because an override key is matched
        // against words and the marks around them are not part of the word.
        var tokens = prepared
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Trim)
            .ToList();

        var bodies = tokens.Select(token => token.Body).ToList();

        for (var at = 0; at < tokens.Count;)
        {
            if (built.Length > 0)
            {
                built.Append(' ');
            }

            // 0. The Commander's own correction, matched over whole words so an entry can never
            //    capture a substring (#146's lesson, applied in advance).
            if (layer?.Match(bodies, at) is { } correction)
            {
                var ipa = correction.Said.IsIpa
                    ? correction.Said.Value
                    : Say(correction.Said.Value, accent, null);

                note?.Invoke(correction.Said.Key, PhonemeRung.Override, ipa);

                built.Append(ipa);
                built.Append(tokens[at + correction.Words - 1].Tail);
                at += correction.Words;
                continue;
            }

            built.Append(Token(tokens[at], accent));
            at++;
        }

        return built.ToString();
    }

    /// <summary>A token split into what is said and the punctuation kept after it.</summary>
    private readonly record struct Trimmed(string Body, string Tail);

    /// <summary>
    /// The marks around a token stripped off it: the phrasing kept as the tail, the decoration
    /// dropped wherever in the run it sat.
    /// </summary>
    private static Trimmed Trim(string token)
    {
        var body = token.TrimEnd(Trailing);
        var tail = token[body.Length..];

        return new Trimmed(
            body.TrimStart(Leading),
            tail.Any(mark => Decoration.Contains(mark))
                ? string.Concat(tail.Where(mark => !Decoration.Contains(mark)))
                : tail);
    }

    /// <summary>
    /// One whitespace-delimited token: its trailing punctuation kept, its body run through the
    /// ladder.
    /// </summary>
    private string Token(Trimmed token, SpeechAccent accent)
    {
        if (token.Body.Length == 0)
        {
            return token.Tail;
        }

        var pieces = token.Body.Split(Dashes, StringSplitOptions.RemoveEmptyEntries);
        var said = new List<(string Ipa, Reading Reading)>();

        foreach (var piece in pieces)
        {
            said.Add(Segment(piece, accent));
        }

        return Join(said) + token.Tail;
    }

    /// <summary>
    /// The dash rule. Spoken between two segments where either had to be spelled, silent where both
    /// are words.
    /// </summary>
    private static string Join(List<(string Ipa, Reading Reading)> pieces)
    {
        if (pieces.Count == 0)
        {
            return string.Empty;
        }

        var built = new System.Text.StringBuilder(pieces[0].Ipa);

        for (var i = 1; i < pieces.Count; i++)
        {
            var spelled = pieces[i - 1].Reading == Reading.Spelled
                          || pieces[i].Reading == Reading.Spelled;

            built.Append(spelled ? " " + Dash + " " : " ");
            built.Append(pieces[i].Ipa);
        }

        return built.ToString();
    }

    /// <summary>One dash-delimited segment, down the ladder in order.</summary>
    private (string Ipa, Reading Reading) Segment(string segment, SpeechAccent accent)
    {
        // 1. A word somebody has already written down the pronunciation of.
        if (segment.All(char.IsLetter) &&
            dictionary?.Lookup(segment.ToLowerInvariant()) is { Length: > 0 } known)
        {
            return Fell(segment, PhonemeRung.Dictionary, Weakened(segment, known), Reading.Spoken);
        }

        // 1b. A word with an apostrophe inside it, which the dictionary holds none of: not one of
        //     its 274,927 entries contains one. Before the digit rungs, because those are about
        //     designations and this is about English.
        if (Contractions.Looks(segment) &&
            Contractions.Ipa(segment, accent, Sound) is { Length: > 0 } joined)
        {
            return Fell(segment, PhonemeRung.Contraction, joined, Reading.Spoken);
        }

        // 2. All digits: a number, said casually.
        if (segment.All(char.IsAsciiDigit))
        {
            return Fell(
                segment, PhonemeRung.Number, Words(SpokenNumber.Say(segment), accent), Reading.Spoken);
        }

        // 3. Letters and digits with nothing between them. A designation rather than a word, and
        //    every attempt to pronounce one produces a noise: B0 is "bee zero".
        if (segment.Any(char.IsAsciiDigit) && segment.Any(char.IsLetter))
        {
            return Fell(
                segment,
                PhonemeRung.Designation,
                SpokenLetters.SpellOut(segment, accent),
                Reading.Spelled);
        }

        // 4. Letters that parse as English syllables, said by rule.
        if (segment.All(char.IsLetter) && LetterToSound.Pronounce(segment) is { Length: > 0 } rules)
        {
            return Fell(segment, PhonemeRung.Rules, rules, Reading.Spoken);
        }

        // 5. Anything left. Never wrong, and the only honest answer for a run nobody can say.
        return Fell(
            segment, PhonemeRung.Spelled, SpokenLetters.SpellOut(segment, accent), Reading.Spelled);
    }

    /// <summary>
    /// Says which rung answered, on the way past. Free when nobody is listening, which is every
    /// ordinary session: the note is null unless a Commander has turned the Voice subsystem up.
    /// </summary>
    private (string Ipa, Reading Reading) Fell(
        string segment, PhonemeRung rung, string ipa, Reading reading)
    {
        note?.Invoke(segment, rung, ipa);
        return (ipa, reading);
    }

    /// <summary>
    /// Words the dictionary marks as stressed which English says unstressed inside a sentence
    /// (2026-08-28).
    /// <para>
    /// <b>The dictionary stores citation forms — how a word is said on its own.</b> That is right
    /// for a lookup and wrong for a sentence: read back, <em>"JOHN DEPARAGON is <b>in</b>
    /// Kamitra"</em> and <em>"you <b>have</b> 256 tonnes"</em> put the emphasis on the preposition
    /// and the auxiliary, which is not a thing a person does.
    /// </para>
    /// <para>
    /// <b>This is a small change and it is worth saying how small.</b> Measured over ten lines d47
    /// actually said, <b>9 of 84</b> stress marks land on one of these — 11%. It was reached for as
    /// a fix for the <em>density</em> of marks, which it is not: the dictionary already leaves most
    /// function words weak, and the remaining marks are on content words where they belong. What it
    /// removes is nine <em>misplaced</em> prominences, which are audible out of proportion to their
    /// number because a wrongly stressed preposition is jarring in a way a correctly stressed noun
    /// is not.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here.</b> <c>no</c> and <c>not</c>, because negation is the one
    /// thing a sentence most needs to carry. <c>one</c>, which is a number far more often than a
    /// pronoun in anything d47 says. <c>this</c>, <c>that</c>, <c>these</c> and <c>those</c>,
    /// because a demonstrative is usually pointing at something and pointing is emphasis. Each of
    /// those would be wrong more often than right, which is the whole test for being on this list.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> Weak = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the",
        "of", "to", "in", "on", "at", "by", "for", "with", "from", "into", "onto", "than",
        "and", "or", "but", "as",
        "is", "are", "was", "were", "be", "been", "am", "has", "have", "had",
        "will", "would", "shall", "should", "can", "could", "may", "might", "must",
        "do", "does", "did",
        "it", "its", "he", "she", "they", "them", "we", "us", "you",
        "my", "your", "our", "their", "his", "her",
    };

    /// <summary>
    /// One dictionary reading, with its stress marks dropped where the word is one English says
    /// weak. Everything else comes back exactly as the dictionary wrote it.
    /// </summary>
    private static string Weakened(string word, string ipa) =>
        Weak.Contains(word) ? ipa.Replace("ˈ", string.Empty).Replace("ˌ", string.Empty) : ipa;

    /// <summary>
    /// What a stem sounds like, by the two rungs that answer rather than spell — the dictionary,
    /// then the rules. Null where neither can say, which is what stops a contraction being built
    /// on top of a spelled-out stem: <em>GQPI's</em> is spelled whole, not spelled and then given
    /// a possessive.
    /// </summary>
    private string? Sound(string stem)
    {
        if (stem.Length == 0)
        {
            return null;
        }

        if (dictionary?.Lookup(stem.ToLowerInvariant()) is { Length: > 0 } known)
        {
            return known;
        }

        return LetterToSound.Pronounce(stem);
    }

    /// <summary>
    /// The number words back through the ladder, so <em>eighty-five</em> is said the way the
    /// dictionary says it rather than by a second set of rules that could disagree.
    /// </summary>
    private string Words(string words, SpeechAccent accent)
    {
        var built = new System.Text.StringBuilder();

        foreach (var word in words.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (built.Length > 0)
            {
                built.Append(' ');
            }

            // The number words first, because they are irregular and the rules mangle them.
            built.Append(
                SpokenNumber.Sounds.TryGetValue(word, out var sound)
                    ? sound
                    : Segment(word, accent).Ipa);
        }

        return built.ToString();
    }
}
