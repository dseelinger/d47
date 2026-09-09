namespace D47.Core.Speech;

/// <summary>A word's pronunciation, or nothing.</summary>
public interface IPronunciationDictionary
{
    /// <summary>The IPA for a word, or null where it is not held.</summary>
    string? Lookup(string word);
}

/// <summary>Which rung of the ladder a segment came off (#153).</summary>
public enum PhonemeRung
{
    /// <summary>The Commander's own override file, which wins over everything.</summary>
    Override,

    /// <summary>The shipped dictionary.</summary>
    Dictionary,

    /// <summary>A word with an apostrophe inside it, built from its stem.</summary>
    Contraction,

    /// <summary>
    /// Digits, with or without a decimal point among them and with or without grouping commas between
    /// them, said as a number.
    /// </summary>
    Number,

    /// <summary>Letters and digits with nothing between them, so a designation and spelled.</summary>
    Designation,

    /// <summary>Letters that parse as English syllables, said by rule.</summary>
    Rules,

    /// <summary>Anything left, spelled out one character at a time.</summary>
    Spelled,
}

/// <summary>Text to the phonemes a local voice can speak, by a ladder that never guesses.</summary>
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

    /// <summary>The dash, said.</summary>
    private const string Dash = "dˈæʃ";

    /// <summary>Marks a model writes around a word that are neither part of it nor phrasing.</summary>
    private static readonly char[] Decoration = ['*', '_', '`', '~'];

    /// <summary>
    /// Punctuation that ends a token and is kept, because Kokoro reads it as phrasing and its
    /// vocabulary contains every one of these.
    /// </summary>
    private static readonly char[] Closers =
        ['.', ',', '!', '?', ';', ':', ')', ']', '"', '\'', '”', '…', '—', '–'];

    /// <summary>The same at the front of a token, where the mark is dropped rather than kept.</summary>
    private static readonly char[] Openers = ['(', '[', '"', '\'', '“', '‘'];

    /// <summary>
    /// Both at once, which is what a token actually wears: <c>**Guardian modules**.</c> ends
    /// <c>**.</c>, so trimming the decoration and then the punctuation strips neither — the full stop
    /// stops the first pass and the asterisks stop the second.
    /// </summary>
    private static readonly char[] Trailing = [.. Closers, .. Decoration];

    private static readonly char[] Leading = [.. Openers, .. Decoration];

    /// <summary>Every dash a compound can be written with.</summary>
    private static readonly char[] Dashes = ['-', '–', '—'];

    /// <summary>One line of text as IPA for <paramref name="voiceId"/>'s accent.</summary>
    public string ToPhonemes(string? text, string? voiceId = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Before anything is read: the Commander may have corrected a word since the last line was spoken.
        overrides?.Refresh();

        return Say(text, SpokenLetters.AccentOf(voiceId), overrides);
    }

    /// <summary>One line, with <paramref name="layer"/> as its top rung.</summary>
    private string Say(string text, SpeechAccent accent, PronunciationOverrides? layer)
    {
        var built = new System.Text.StringBuilder();

        // A model writes the curly one and a Commander types the straight one, and they are the same word.
        var prepared = SpokenNumerals.Expand(text.Replace('’', '\''));

        // Trimmed up front rather than inside the ladder, because an override key is matched against words
        // and the marks around them are not part of the word.
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

            // 0.
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
    /// The marks around a token stripped off it: the phrasing kept as the tail, the decoration dropped
    /// wherever in the run it sat.
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
    /// One whitespace-delimited token: its trailing punctuation kept, its body run through the ladder.
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

    /// <summary>The dash rule.</summary>
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
        // 1.
        if (segment.All(char.IsLetter) &&
            dictionary?.Lookup(segment.ToLowerInvariant()) is { Length: > 0 } known)
        {
            return Fell(segment, PhonemeRung.Dictionary, Weakened(segment, known), Reading.Spoken);
        }

        // 1b.
        if (Contractions.Looks(segment) &&
            Contractions.Ipa(segment, accent, Sound) is { Length: > 0 } joined)
        {
            return Fell(segment, PhonemeRung.Contraction, joined, Reading.Spoken);
        }

        // 2.
        if (SpokenNumber.Looks(segment))
        {
            return Fell(
                segment, PhonemeRung.Number, Words(SpokenNumber.Say(segment), accent), Reading.Spoken);
        }

        // 3.
        if (segment.Any(char.IsAsciiDigit) && segment.Any(char.IsLetter))
        {
            return Fell(
                segment,
                PhonemeRung.Designation,
                SpokenLetters.SpellOut(segment, accent),
                Reading.Spelled);
        }

        // 4.
        if (segment.All(char.IsLetter) && LetterToSound.Pronounce(segment) is { Length: > 0 } rules)
        {
            return Fell(segment, PhonemeRung.Rules, rules, Reading.Spoken);
        }

        // 5.
        return Fell(
            segment, PhonemeRung.Spelled, SpokenLetters.SpellOut(segment, accent), Reading.Spelled);
    }

    /// <summary>Says which rung answered, on the way past.</summary>
    private (string Ipa, Reading Reading) Fell(
        string segment, PhonemeRung rung, string ipa, Reading reading)
    {
        note?.Invoke(segment, rung, ipa);
        return (ipa, reading);
    }

    /// <summary>
    /// Words the dictionary marks as stressed which English says unstressed inside a sentence
    /// (2026-08-28).
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
    /// One dictionary reading, with its stress marks dropped where the word is one English says weak.
    /// </summary>
    private static string Weakened(string word, string ipa) =>
        Weak.Contains(word) ? ipa.Replace("ˈ", string.Empty).Replace("ˌ", string.Empty) : ipa;

    /// <summary>
    /// What a stem sounds like, by the two rungs that answer rather than spell — the dictionary, then
    /// the rules.
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
    /// The number words back through the ladder, so eighty-five is said the way the dictionary says it
    /// rather than by a second set of rules that could disagree.
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
