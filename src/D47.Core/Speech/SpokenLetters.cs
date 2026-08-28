namespace D47.Core.Speech;

/// <summary>Which side of the Atlantic a voice is from, which changes exactly one letter.</summary>
public enum SpeechAccent
{
    American,
    British,
}

/// <summary>
/// Letters and digits said aloud, for the parts of a name that cannot be pronounced.
/// <para>
/// <b><c>Z</c> is <em>zed</em> for a voice that is not American</b> — the Commander's ruling of
/// 2026-08-28, and it costs one table entry because Kokoro's voice ids carry the accent in their
/// prefix: <c>af_</c> and <c>am_</c> are American, <c>bf_</c> and <c>bm_</c> British. A British
/// voice saying <em>zee</em> is the kind of small wrongness that is heard every single time.
/// </para>
/// </summary>
public static class SpokenLetters
{
    private static readonly Dictionary<char, string> Letters = new()
    {
        ['a'] = "eɪ", ['b'] = "biː", ['c'] = "siː", ['d'] = "diː", ['e'] = "iː",
        ['f'] = "ˈɛf", ['g'] = "dʒiː", ['h'] = "eɪtʃ", ['i'] = "aɪ", ['j'] = "dʒeɪ",
        ['k'] = "keɪ", ['l'] = "ˈɛl", ['m'] = "ˈɛm", ['n'] = "ˈɛn", ['o'] = "oʊ",
        ['p'] = "piː", ['q'] = "kjuː", ['r'] = "ɑːɹ", ['s'] = "ˈɛs", ['t'] = "tiː",
        ['u'] = "juː", ['v'] = "viː", ['w'] = "ˈdʌbəljuː", ['x'] = "ˈɛks", ['y'] = "waɪ",
    };

    private const string Zee = "ziː";
    private const string Zed = "zɛd";

    private static readonly string[] Digits =
        ["ˈzɪɹoʊ", "wʌn", "tuː", "θɹiː", "fɔːɹ", "faɪv", "sɪks", "ˈsɛvən", "eɪt", "naɪn"];

    /// <summary>The accent a Kokoro voice id implies. Unknown ids are read as American.</summary>
    public static SpeechAccent AccentOf(string? voiceId) =>
        voiceId is { Length: > 1 } id && char.ToLowerInvariant(id[0]) == 'b'
            ? SpeechAccent.British
            : SpeechAccent.American;

    /// <summary>One character said aloud, or null for anything that is not a letter or a digit.</summary>
    public static string? Say(char character, SpeechAccent accent)
    {
        var lower = char.ToLowerInvariant(character);

        if (lower == 'z')
        {
            return accent == SpeechAccent.British ? Zed : Zee;
        }

        if (Letters.TryGetValue(lower, out var letter))
        {
            return letter;
        }

        return char.IsAsciiDigit(lower) ? Digits[lower - '0'] : null;
    }

    /// <summary>
    /// A run spelled out, one character at a time, with a short pause between each.
    /// <para>
    /// The pauses matter more than they look: <c>GQPI</c> run together is a noise, and the same
    /// four sounds with breaks between them is four letters a Commander can write down. A comma is
    /// how Kokoro's vocabulary spells a short pause, and it is in the vocabulary.
    /// </para>
    /// </summary>
    public static string SpellOut(string run, SpeechAccent accent) =>
        string.Join(
            ", ",
            run.Select(character => Say(character, accent)).Where(said => said is not null));
}
