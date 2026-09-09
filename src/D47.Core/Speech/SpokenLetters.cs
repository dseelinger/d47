namespace D47.Core.Speech;

/// <summary>Which side of the Atlantic a voice is from, which changes exactly one letter.</summary>
public enum SpeechAccent
{
    American,
    British,
}

/// <summary>Letters and digits said aloud, for the parts of a name that cannot be pronounced.</summary>
public static class SpokenLetters
{
    private static readonly Dictionary<char, string> Letters = new()
    {
        ['a'] = "eɪ", ['b'] = "biː", ['c'] = "siː", ['d'] = "diː", ['e'] = "iː",
        ['f'] = "ˈɛf", ['g'] = "dʒiː", ['h'] = "eɪtʃ", ['i'] = "aɪ", ['j'] = "dʒeɪ",
        ['k'] = "keɪ", ['l'] = "ˈɛl", ['m'] = "ˈɛm", ['n'] = "ˈɛn", ['o'] = "oʊ",
        ['p'] = "piː", ['q'] = "kjuː", ['r'] = "ɑːɹ", ['s'] = "ˈɛs", ['t'] = "tiː",
        ['u'] = "juː", ['v'] = "viː", ['w'] = "dˈʌbəljuː", ['x'] = "ˈɛks", ['y'] = "waɪ",
    };

    private const string Zee = "ziː";
    private const string Zed = "zɛd";

    private static readonly string[] Digits =
        ["zˈiəɹoʊ", "wˌʌn", "tˈuː", "θɹˈiː", "fˈoːɹ", "fˈaɪv", "sˈɪks", "sˈɛvən", "ˈeɪt", "nˈaɪn"];

    /// <summary>The accent a Kokoro voice id implies.</summary>
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

    /// <summary>A run spelled out, one character at a time, with a short pause between each.</summary>
    public static string SpellOut(string run, SpeechAccent accent) =>
        string.Join(
            ", ",
            run.Select(character => Say(character, accent)).Where(said => said is not null));
}
