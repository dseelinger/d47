namespace D47.Core.Speech;

/// <summary>A word's pronunciation, or nothing. Implemented by the shipped dictionary.</summary>
public interface IPronunciationDictionary
{
    /// <summary>The IPA for a word, or null where it is not held.</summary>
    string? Lookup(string word);
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
/// </summary>
public sealed class Phonemiser(IPronunciationDictionary? dictionary = null)
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

        var accent = SpokenLetters.AccentOf(voiceId);
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

        foreach (var token in prepared.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (built.Length > 0)
            {
                built.Append(' ');
            }

            built.Append(Token(token, accent));
        }

        return built.ToString();
    }

    /// <summary>
    /// One whitespace-delimited token: its trailing punctuation kept, its body run through the
    /// ladder.
    /// </summary>
    private string Token(string token, SpeechAccent accent)
    {
        var body = token.TrimEnd('.', ',', '!', '?', ';', ':', ')', ']', '"', '\'');
        var tail = token[body.Length..];
        var head = body.TrimStart('(', '[', '"', '\'');

        body = head;

        if (body.Length == 0)
        {
            return tail;
        }

        var pieces = body.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var said = new List<(string Ipa, Reading Reading)>();

        foreach (var piece in pieces)
        {
            said.Add(Segment(piece, accent));
        }

        return Join(said) + tail;
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
            return (Weakened(segment, known), Reading.Spoken);
        }

        // 1b. A word with an apostrophe inside it, which the dictionary holds none of: not one of
        //     its 274,927 entries contains one. Before the digit rungs, because those are about
        //     designations and this is about English.
        if (Contractions.Looks(segment) &&
            Contractions.Ipa(segment, accent, Sound) is { Length: > 0 } joined)
        {
            return (joined, Reading.Spoken);
        }

        // 2. All digits: a number, said casually.
        if (segment.All(char.IsAsciiDigit))
        {
            return (Words(SpokenNumber.Say(segment), accent), Reading.Spoken);
        }

        // 3. Letters and digits with nothing between them. A designation rather than a word, and
        //    every attempt to pronounce one produces a noise: B0 is "bee zero".
        if (segment.Any(char.IsAsciiDigit) && segment.Any(char.IsLetter))
        {
            return (SpokenLetters.SpellOut(segment, accent), Reading.Spelled);
        }

        // 4. Letters that parse as English syllables, said by rule.
        if (segment.All(char.IsLetter) && LetterToSound.Pronounce(segment) is { Length: > 0 } rules)
        {
            return (rules, Reading.Spoken);
        }

        // 5. Anything left. Never wrong, and the only honest answer for a run nobody can say.
        return (SpokenLetters.SpellOut(segment, accent), Reading.Spelled);
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
