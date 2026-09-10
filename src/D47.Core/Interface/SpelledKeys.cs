using D47.Core.Listening;

namespace D47.Core.Interface;

/// <summary>One key on a drawn keyboard.</summary>
public enum SpelledPress
{
    /// <summary>A key that types <see cref="SpelledKey.Character"/>.</summary>
    Character,

    /// <summary>The last character back.</summary>
    Delete,

    /// <summary>The whole value gone.</summary>
    Clear,

    /// <summary>Commit, which is the only way a board writes anything.</summary>
    Done,

    /// <summary>Away, committing nothing.</summary>
    Cancel,
}

/// <summary>One press, in the order it was said.</summary>
public readonly record struct SpelledKey(SpelledPress Press, char Character = default)
{
    /// <summary>A character key.</summary>
    public static SpelledKey Types(char character) => new(SpelledPress.Character, character);
}

/// <summary>What one utterance turned out to be.</summary>
/// <param name="Keys">The presses, in order, when every word was one.</param>
/// <param name="Refused">The first word that was not a key, or null when they all were.</param>
public sealed record Spelled(IReadOnlyList<SpelledKey> Keys, string? Refused)
{
    /// <summary>Whether this is spelling rather than a value: every word a key, and at least one.</summary>
    public bool IsSpelling => Refused is null && Keys.Count > 0;
}

/// <summary>What a board does with what it heard.</summary>
public enum SpelledOutcome
{
    /// <summary>A partial: shown, never pressed.</summary>
    Waiting,

    /// <summary>Nothing said, or not heard well enough to act on.</summary>
    NotCaught,

    /// <summary>Every word was a key, so press them all.</summary>
    Presses,

    /// <summary>Not spelling, so the whole utterance is the value.</summary>
    Dictation,
}

/// <summary>What was heard, decided once for both boards.</summary>
/// <param name="Outcome">What to do with it.</param>
/// <param name="Text">The utterance, trimmed.</param>
/// <param name="Keys">The presses, for <see cref="SpelledOutcome.Presses"/>.</param>
/// <param name="Say">The board's state line, or null when there is nothing to say.</param>
public sealed record SpelledHeard(
    SpelledOutcome Outcome,
    string Text,
    IReadOnlyList<SpelledKey> Keys,
    string? Say);

/// <summary>
/// Spelling a value onto a drawn keyboard by voice (#51): the NATO alphabet for letters, digits as
/// words or figures, and one word each for the keys that are neither.
/// </summary>
/// <remarks>
/// The presses this produces go to a drawn board and nowhere else. Nothing here may reach the
/// keyboard Elite is listening to: a spelled run is a value being typed into a panel, not a macro.
/// </remarks>
public static class Spelling
{
    /// <summary>The word for each letter, as the help page prints it.</summary>
    public static IReadOnlyDictionary<char, string> Alphabet { get; } =
        new Dictionary<char, string>
        {
            ['a'] = "alpha",
            ['b'] = "bravo",
            ['c'] = "charlie",
            ['d'] = "delta",
            ['e'] = "echo",
            ['f'] = "foxtrot",
            ['g'] = "golf",
            ['h'] = "hotel",
            ['i'] = "india",
            ['j'] = "juliett",
            ['k'] = "kilo",
            ['l'] = "lima",
            ['m'] = "mike",
            ['n'] = "november",
            ['o'] = "oscar",
            ['p'] = "papa",
            ['q'] = "quebec",
            ['r'] = "romeo",
            ['s'] = "sierra",
            ['t'] = "tango",
            ['u'] = "uniform",
            ['v'] = "victor",
            ['w'] = "whiskey",
            ['x'] = "x-ray",
            ['y'] = "yankee",
            ['z'] = "zulu",
        };

    /// <summary>The word for each digit.</summary>
    public static IReadOnlyDictionary<char, string> Figures { get; } =
        new Dictionary<char, string>
        {
            ['0'] = "zero",
            ['1'] = "one",
            ['2'] = "two",
            ['3'] = "three",
            ['4'] = "four",
            ['5'] = "five",
            ['6'] = "six",
            ['7'] = "seven",
            ['8'] = "eight",
            ['9'] = "nine",
        };

    /// <summary>The word for each character key that is neither a letter nor a digit. One each.</summary>
    public static IReadOnlyDictionary<char, string> Marks { get; } =
        new Dictionary<char, string>
        {
            [' '] = "space",
            ['-'] = "dash",
            ['_'] = "underscore",
            ['.'] = "dot",
        };

    /// <summary>The word for each key that types nothing. One each.</summary>
    public static IReadOnlyDictionary<SpelledPress, string> Commands { get; } =
        new Dictionary<SpelledPress, string>
        {
            [SpelledPress.Delete] = "delete",
            [SpelledPress.Clear] = "clear",
            [SpelledPress.Done] = "done",
            [SpelledPress.Cancel] = "cancel",
        };

    /// <summary>Every character this can press, which must be every character the board draws.</summary>
    public static IReadOnlyList<char> Characters { get; } =
        [.. Alphabet.Keys, .. Figures.Keys, .. Marks.Keys];

    /// <summary>What the tutor says when asked how spelling works at all.</summary>
    public const string Shape =
        "Say each letter as its word: alpha, bravo, charlie. "
        + "Ask me for the whole alphabet, or for one letter.";

    /// <summary>
    /// Every word that is a key, including the spellings Whisper writes for the same word — "alfa"
    /// for alpha, "juliet" for juliett — which are that word spelled differently rather than another.
    /// </summary>
    private static readonly Dictionary<string, SpelledKey> Words = Vocabulary();

    /// <summary>
    /// One utterance as a run of presses, or the first word that was not a key. Nothing is pressed
    /// unless every word is a key: half a spelled system name is worse than none of it.
    /// </summary>
    public static Spelled Parse(string? utterance)
    {
        var keys = new List<SpelledKey>();

        foreach (var token in (utterance ?? string.Empty).Split(
            [' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var reduced = Reduce(token);

            if (reduced.Length == 0)
            {
                continue;
            }

            if (Words.TryGetValue(reduced.ToLowerInvariant(), out var key))
            {
                keys.Add(key);
                continue;
            }

            // The second form: Whisper writes "ABC" for a run somebody spelled out, and "75" for two
            // figures. Lower case is not this form, or the article "a" would be a key.
            if (reduced.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterUpper(character)))
            {
                keys.AddRange(reduced.Select(character => SpelledKey.Types(char.ToLowerInvariant(character))));
                continue;
            }

            return new Spelled([], token);
        }

        return new Spelled(keys, null);
    }

    /// <summary>What a board should do with what it heard.</summary>
    public static SpelledHeard Hear(Heard heard)
    {
        var said = heard.Text.Trim();

        if (!heard.Final)
        {
            return new SpelledHeard(SpelledOutcome.Waiting, said, [], null);
        }

        if (SpeechNoise.IsNothingSaid(said))
        {
            return new SpelledHeard(
                SpelledOutcome.NotCaught,
                said,
                [],
                TextEntryLoop.Explain(EntryFallback.NothingHeard, null));
        }

        // Below the bar nothing is pressed: a misheard press is one wrong character in the middle of a
        // value, which is the failure a Commander is least likely to see.
        if (heard.Confidence < TextEntryLoop.ConfidentEnough)
        {
            return new SpelledHeard(
                SpelledOutcome.NotCaught,
                said,
                [],
                TextEntryLoop.Explain(EntryFallback.LowConfidence, null));
        }

        var spelled = Parse(said);

        return spelled.IsSpelling
            ? new SpelledHeard(SpelledOutcome.Presses, said, spelled.Keys, null)
            : new SpelledHeard(
                SpelledOutcome.Dictation,
                said,
                [],
                spelled.Refused is { } refused ? Refusal(refused) : null);
    }

    /// <summary>What the board says about a word that was not a key.</summary>
    public static string Refusal(string word) => $"“{word}” is not a key, so I took that as the value.";

    /// <summary>The tutor, answered from the table above rather than by the model.</summary>
    /// <param name="letter">One letter, "all" for the whole alphabet, or null for the shape of it.</param>
    public static string Tutor(string? letter)
    {
        var asked = letter?.Trim() ?? string.Empty;

        if (asked.Length == 0)
        {
            return Shape;
        }

        if (string.Equals(asked, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Whole();
        }

        var one = char.ToLowerInvariant(asked[0]);

        return asked.Length == 1 && Alphabet.TryGetValue(one, out var word)
            ? $"{char.ToUpperInvariant(one)} is {word}."
            : $"I have no word for “{asked}”. {Shape}";
    }

    /// <summary>
    /// Every letter's word, and what the rest of the board answers to. The words alone: a Commander
    /// reading twenty-six of these does not need the letter each one starts with spelled out to them.
    /// </summary>
    public static string Whole() =>
        "Say each letter as its word."
        + Environment.NewLine
        + string.Join(Environment.NewLine, Alphabet.Values.Select(word => $"- {Capitalised(word)}"))
        + Environment.NewLine
        + "Digits are said as figures or as their own words. "
        + $"The rest of the keys are {string.Join(", ", Marks.Values)}, "
        + $"{string.Join(", ", Commands.Values)}.";

    private static string Capitalised(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>
    /// A word as it can be looked up: no surrounding punctuation, and no hyphen or apostrophe inside
    /// it, so "X-ray" and "Xray" are the same word.
    /// </summary>
    private static string Reduce(string token) => new([.. token.Where(char.IsAsciiLetterOrDigit)]);

    private static Dictionary<string, SpelledKey> Vocabulary()
    {
        var words = new Dictionary<string, SpelledKey>(StringComparer.Ordinal);

        foreach (var (character, word) in Alphabet)
        {
            words[Reduce(word)] = SpelledKey.Types(character);
        }

        foreach (var (character, word) in Figures)
        {
            words[word] = SpelledKey.Types(character);
        }

        foreach (var (character, word) in Marks)
        {
            words[word] = SpelledKey.Types(character);
        }

        foreach (var (press, word) in Commands)
        {
            words[word] = new SpelledKey(press);
        }

        // The same word, spelled the other way.
        words["alfa"] = SpelledKey.Types('a');
        words["juliet"] = SpelledKey.Types('j');
        words["whisky"] = SpelledKey.Types('w');
        words["niner"] = SpelledKey.Types('9');

        return words;
    }
}
