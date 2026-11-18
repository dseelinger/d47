using System.Text;
using System.Text.RegularExpressions;

namespace D47.Tts;

/// <summary>
/// Turns the words <see cref="JapaneseReader"/> returns into Kokoro phonemes, by upstream Kokoro's kana table and
/// its rules for digraphs, small kana, the long vowel mark, っ and ん (<c>misaki/cutlet.py</c>; see NOTICE).
/// </summary>
/// <param name="leftOut">Called with each word that has no pronunciation and no kana of its own, which is left out.</param>
public sealed partial class JapanesePhonemiser(Action<string>? leftOut = null)
{
    private const string Sutegana = "ゃゅょぁぃぅぇぉ";

    private const string Odoriji = "〃々ゝゞヽ";

    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        ["ぁ"] = "a", ["あ"] = "a", ["ぃ"] = "i", ["い"] = "i", ["ぅ"] = "ɯ", ["う"] = "ɯ", ["ぇ"] = "e", ["え"] = "e", ["ぉ"] = "o", ["お"] = "o",
        ["か"] = "ka", ["が"] = "ɡa", ["き"] = "kʲi", ["ぎ"] = "ɡʲi", ["く"] = "kɯ", ["ぐ"] = "ɡɯ", ["け"] = "ke", ["げ"] = "ɡe", ["こ"] = "ko", ["ご"] = "ɡo",
        ["さ"] = "sa", ["ざ"] = "ʣa", ["し"] = "ɕi", ["じ"] = "ʥi", ["す"] = "sɨ", ["ず"] = "zɨ", ["せ"] = "se", ["ぜ"] = "ʣe", ["そ"] = "so", ["ぞ"] = "ʣo",
        ["た"] = "ta", ["だ"] = "da", ["ち"] = "ʨi", ["ぢ"] = "ʥi", ["つ"] = "ʦɨ", ["づ"] = "zɨ", ["て"] = "te", ["で"] = "de", ["と"] = "to", ["ど"] = "do",
        ["な"] = "na", ["に"] = "ɲi", ["ぬ"] = "nɯ", ["ね"] = "ne", ["の"] = "no",
        ["は"] = "ha", ["ば"] = "ba", ["ぱ"] = "pa", ["ひ"] = "çi", ["び"] = "bʲi", ["ぴ"] = "pʲi", ["ふ"] = "ɸɯ", ["ぶ"] = "bɯ", ["ぷ"] = "pɯ",
        ["へ"] = "he", ["べ"] = "be", ["ぺ"] = "pe", ["ほ"] = "ho", ["ぼ"] = "bo", ["ぽ"] = "po",
        ["ま"] = "ma", ["み"] = "mʲi", ["む"] = "mɯ", ["め"] = "me", ["も"] = "mo",
        ["ゃ"] = "ja", ["や"] = "ja", ["ゅ"] = "jɯ", ["ゆ"] = "jɯ", ["ょ"] = "jo", ["よ"] = "jo",
        ["ら"] = "ɾa", ["り"] = "ɾʲi", ["る"] = "ɾɯ", ["れ"] = "ɾe", ["ろ"] = "ɾo",
        ["ゎ"] = "βa", ["わ"] = "βa", ["ゐ"] = "i", ["ゑ"] = "e", ["を"] = "o", ["ゔ"] = "vɯ", ["ゕ"] = "ka", ["ゖ"] = "ke",

        ["ヷ"] = "va", ["ヸ"] = "vʲi", ["ヹ"] = "ve", ["ヺ"] = "vo",

        ["いぇ"] = "je", ["うぃ"] = "βi", ["うぇ"] = "βe", ["うぉ"] = "βo",
        ["きぇ"] = "kʲe", ["きゃ"] = "kʲa", ["きゅ"] = "kʲɨ", ["きょ"] = "kʲo", ["ぎゃ"] = "ɡʲa", ["ぎゅ"] = "ɡʲɨ", ["ぎょ"] = "ɡʲo",
        ["くぁ"] = "kᵝa", ["くぃ"] = "kᵝi", ["くぇ"] = "kᵝe", ["くぉ"] = "kᵝo", ["ぐぁ"] = "ɡᵝa", ["ぐぃ"] = "ɡᵝi", ["ぐぇ"] = "ɡᵝe", ["ぐぉ"] = "ɡᵝo",
        ["しぇ"] = "ɕe", ["しゃ"] = "ɕa", ["しゅ"] = "ɕɨ", ["しょ"] = "ɕo", ["じぇ"] = "ʥe", ["じゃ"] = "ʥa", ["じゅ"] = "ʥɨ", ["じょ"] = "ʥo",
        ["ちぇ"] = "ʨe", ["ちゃ"] = "ʨa", ["ちゅ"] = "ʨɨ", ["ちょ"] = "ʨo", ["ぢゃ"] = "ʥa", ["ぢゅ"] = "ʥɨ", ["ぢょ"] = "ʥo",
        ["つぁ"] = "ʦa", ["つぃ"] = "ʦʲi", ["つぇ"] = "ʦe", ["つぉ"] = "ʦo",
        ["てぃ"] = "tʲi", ["てゅ"] = "tʲɨ", ["でぃ"] = "dʲi", ["でゅ"] = "dʲɨ", ["とぅ"] = "tɯ", ["どぅ"] = "dɯ",
        ["にぇ"] = "ɲe", ["にゃ"] = "ɲa", ["にゅ"] = "ɲɨ", ["にょ"] = "ɲo",
        ["ひぇ"] = "çe", ["ひゃ"] = "ça", ["ひゅ"] = "çɨ", ["ひょ"] = "ço",
        ["びゃ"] = "bʲa", ["びゅ"] = "bʲɨ", ["びょ"] = "bʲo", ["ぴゃ"] = "pʲa", ["ぴゅ"] = "pʲɨ", ["ぴょ"] = "pʲo",
        ["ふぁ"] = "ɸa", ["ふぃ"] = "ɸʲi", ["ふぇ"] = "ɸe", ["ふぉ"] = "ɸo", ["ふゅ"] = "ɸʲɨ", ["ふょ"] = "ɸʲo",
        ["みゃ"] = "mʲa", ["みゅ"] = "mʲɨ", ["みょ"] = "mʲo", ["りゃ"] = "ɾʲa", ["りゅ"] = "ɾʲɨ", ["りょ"] = "ɾʲo",
        ["ゔぁ"] = "va", ["ゔぃ"] = "vʲi", ["ゔぇ"] = "ve", ["ゔぉ"] = "vo", ["ゔゅ"] = "bʲɨ", ["ゔょ"] = "bʲo",

        ["。"] = ".", ["、"] = ",", ["？"] = "?", ["！"] = "!", ["「"] = "“", ["」"] = "”", ["『"] = "“", ["』"] = "”",
        ["："] = ":", ["；"] = ";", ["（"] = "(", ["）"] = ")", ["《"] = "(", ["》"] = ")", ["【"] = "[", ["】"] = "]", ["・"] = " ",
        ["，"] = ",", ["～"] = "—", ["〜"] = "—", ["—"] = "—", ["«"] = "“", ["»"] = "”", ["゚"] = string.Empty, ["゙"] = string.Empty,

        // The same marks after NFKC, which turns the full-width ones into ASCII.
        ["."] = ".", [","] = ",", ["?"] = "?", ["!"] = "!", [":"] = ":", [";"] = ";", ["("] = "(", [")"] = ")", ["["] = "[", ["]"] = "]", ["~"] = "—",
    };

    /// <summary>A line as the reader is given it: NFKC, so half-width katakana is read as full-width.</summary>
    public static string Normalise(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return line.Normalize(NormalizationForm.FormKC);
    }

    /// <summary>The phonemes for <paramref name="words"/>, a space between words.</summary>
    public string ToPhonemes(IReadOnlyList<JapaneseWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var tokens = new List<(string Text, bool Space)>();

        foreach (var word in words)
        {
            if (Reading(word) is not { } reading)
            {
                leftOut?.Invoke(word.Surface);
                continue;
            }

            var phonemes = Word(Hiragana(reading));
            var space = true;

            if (word.Surface is "「" or "『" or "«" || phonemes is "(" or "[")
            {
                if (tokens.Count > 0)
                {
                    tokens[^1] = tokens[^1] with { Space = true };
                }

                space = false;
            }
            else if (word.Surface is "」" or "』" or "»" || (phonemes.Length > 0 && "]).,?!:".Contains(phonemes, StringComparison.Ordinal)))
            {
                if (tokens.Count > 0)
                {
                    tokens[^1] = tokens[^1] with { Space = false };
                }
            }
            else if (phonemes == " ")
            {
                space = false;
            }

            tokens.Add((phonemes, space));
        }

        var joined = string.Concat(tokens.Select(token => token.Space ? token.Text + " " : token.Text));
        var spoken = Whitespace().Replace(joined.Trim(), " ").Replace('(', '«').Replace(')', '»');

        return GlottalStopSpacing().Replace(spoken, string.Empty);
    }

    /// <summary>The word's pronunciation, else its surface where that is all kana or marks the table holds, else null.</summary>
    private static string? Reading(JapaneseWord word)
    {
        if (word.Pronunciation is { Length: > 0 } pronunciation)
        {
            return pronunciation;
        }

        return word.Surface.Length > 0 && word.Surface.All(c => IsKana(c) || Table.ContainsKey(c.ToString()))
            ? word.Surface
            : null;
    }

    private static bool IsKana(char c) =>
        c is >= 'ぁ' and <= 'ゖ' or >= 'ァ' and <= 'ヺ' or 'ー' || Odoriji.Contains(c, StringComparison.Ordinal);

    /// <summary>Katakana as hiragana; ヷ to ヺ, which have none, stay as they are.</summary>
    private static string Hiragana(string text) =>
        string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c;
            }
        });

    private static string Word(string hiragana)
    {
        var phonemes = new StringBuilder();

        for (var i = 0; i < hiragana.Length; i++)
        {
            char? previous = i > 0 ? hiragana[i - 1] : null;
            char? next = i < hiragana.Length - 1 ? hiragana[i + 1] : null;

            phonemes.Append(Mapping(previous, hiragana[i], next));
        }

        return phonemes.ToString();
    }

    /// <summary>One kana, read with its neighbours.</summary>
    private static string Mapping(char? previous, char kana, char? next)
    {
        if (Odoriji.Contains(kana, StringComparison.Ordinal))
        {
            return kana switch
            {
                'ゝ' or 'ヽ' => previous is { } repeated ? Lookup(repeated) : string.Empty,
                'ゞ' => previous is { } voiced && Dakuten(voiced) is { } withDakuten ? Lookup(withDakuten) : string.Empty,
                _ => string.Empty,
            };
        }

        if (previous is { } before && Table.TryGetValue($"{before}{kana}", out var digraph))
        {
            return digraph;
        }

        if (next is { } after && Table.ContainsKey($"{kana}{after}"))
        {
            return string.Empty;
        }

        if (next is { } small && Sutegana.Contains(small, StringComparison.Ordinal))
        {
            return kana != 'っ' && Table.TryGetValue(kana.ToString(), out var mora)
                ? mora[..^1] + Lookup(small)
                : string.Empty;
        }

        if (Sutegana.Contains(kana, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return kana switch
        {
            'ー' => "ː",
            'っ' => "ʔ",
            'ん' => Moraic(next),
            _ => Lookup(kana),
        };
    }

    /// <summary>ん, assimilated to the kana after it.</summary>
    private static string Moraic(char? next)
    {
        if (next is not { } following || !Table.TryGetValue(following.ToString(), out var sound) || sound.Length == 0)
        {
            return "ɴ";
        }

        return sound[0] switch
        {
            'm' or 'p' or 'b' => "m",
            'k' or 'ɡ' => "ŋ",
            'ɲ' or 'ʨ' or 'ʥ' => "ɲ",
            'n' or 't' or 'd' or 'ɾ' or 'z' => "n",
            _ => "ɴ",
        };
    }

    private static string Lookup(char kana) => Table.GetValueOrDefault(kana.ToString(), string.Empty);

    private static char? Dakuten(char kana) =>
        "かきくけこさしすせそたちつてとはひふへほ".IndexOf(kana, StringComparison.Ordinal) is var index and >= 0
            ? "がぎぐげござじずぜぞだぢづでどばびぶべぼ"[index]
            : null;

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex("(?<![!\",.:;?»—…”]) (?=ʔ)|(?<=ʔ) (?![\"«“])", RegexOptions.CultureInvariant)]
    private static partial Regex GlottalStopSpacing();
}
