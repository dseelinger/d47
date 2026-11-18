using System.Text;
using System.Text.RegularExpressions;

namespace D47.Tts;

/// <summary>A Japanese line as Chatterbox Multilingual reads it: kanji as hiragana, then NFKD, behind the <c>[ja]</c> tag.</summary>
internal static partial class ChatterboxJapaneseText
{
    public const string LanguageTag = "[ja]";

    [GeneratedRegex(@"\[[^\[\]]*\]")]
    private static partial Regex Tags();

    /// <summary>
    /// Removes every bracketed tag, replaces each word containing kanji with its reading in hiragana and keeps every
    /// other word as written. A kanji word with no reading is left out and passed to <paramref name="leftOut"/>.
    /// </summary>
    public static string Prepare(string line, Func<string, IReadOnlyList<JapaneseWord>> read, Action<string>? leftOut = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(read);

        var text = new StringBuilder();

        foreach (var word in read(Tags().Replace(line, " ")))
        {
            if (!HasKanji(word.Surface))
            {
                text.Append(word.Surface);
            }
            else if (word.Reading is { Length: > 0 } reading)
            {
                text.Append(Hiragana(reading));
            }
            else
            {
                leftOut?.Invoke(word.Surface);
            }
        }

        return LanguageTag + text.ToString().Normalize(NormalizationForm.FormKD);
    }

    /// <summary>Whether any character is a kanji or the repeat mark 々.</summary>
    public static bool HasKanji(string text) =>
        text.Any(c => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿') or '々');

    /// <summary>Katakana ァ to ヶ shifted to hiragana; anything else, such as ー, as it is.</summary>
    public static string Hiragana(string katakana)
    {
        var shifted = new char[katakana.Length];

        for (var i = 0; i < katakana.Length; i++)
        {
            var c = katakana[i];
            shifted[i] = c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c;
        }

        return new string(shifted);
    }
}
