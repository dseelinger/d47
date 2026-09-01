using System.Text.RegularExpressions;

namespace D47.Core.Audio;

/// <summary>
/// Markdown out of what is heard (reported 2026-08-31: <i>"a lot of asterisk asterisk"</i>).
/// <para>
/// A model writes <c>**Phoenix Base**</c> without being asked, and a voice reads the asterisks
/// aloud. Stripped once, in <see cref="SpeechPipeline"/> where everything audible converges —
/// the same reason the voice log and the said-record live there — so the speaker, the caption
/// and the "said:" line all carry the sentence, never its markup. The conversation history is
/// untouched: what the model wrote is what the model reads back.
/// </para>
/// <para>
/// <b>Prose that merely contains the characters survives.</b> <c>snake_case_names</c>,
/// <c>5 * 3</c> and a bare dash at the start of a line are not markdown and come through
/// unchanged; the patterns below demand the paired, hugging shape markdown actually has.
/// </para>
/// </summary>
public static partial class PlainSpeech
{
    public static string Strip(string sentence)
    {
        if (sentence.Length == 0 || sentence.AsSpan().IndexOfAny("*_`#[") < 0)
        {
            return sentence;
        }

        var plain = sentence;

        plain = Heading().Replace(plain, string.Empty);
        plain = Link().Replace(plain, "$1");
        plain = Code().Replace(plain, "$1");
        plain = BoldItalic().Replace(plain, "$1");
        plain = Bold().Replace(plain, "$1");
        plain = Italic().Replace(plain, "$1");
        plain = BoldUnderscore().Replace(plain, "$1");
        plain = ItalicUnderscore().Replace(plain, "$1");

        // A bullet the splitter carried in, and any run of asterisks a pair-strip left behind —
        // an emphasis split across two sentences leaves one unmatched half in each.
        plain = Bullet().Replace(plain, string.Empty);
        plain = StrayStars().Replace(plain, string.Empty);

        return plain;
    }

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"\[([^\]\n]+)\]\([^)\n]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex Code();

    [GeneratedRegex(@"\*\*\*(\S(?:[^*\n]*?\S)?)\*\*\*")]
    private static partial Regex BoldItalic();

    [GeneratedRegex(@"\*\*(\S(?:[^*\n]*?\S)?)\*\*")]
    private static partial Regex Bold();

    /// <summary>The inner text may not begin or end with a space, which is what spares <c>5 * 3</c>.</summary>
    [GeneratedRegex(@"(?<!\*)\*(\S(?:[^*\n]*?\S)?)\*(?!\*)")]
    private static partial Regex Italic();

    /// <summary>
    /// <c>\b</c> is what spares <c>snake_case</c>: an underscore is a word character, so there
    /// is no boundary between a letter and the underscore inside an identifier.
    /// </summary>
    [GeneratedRegex(@"\b__([^_\n]+)__\b")]
    private static partial Regex BoldUnderscore();

    [GeneratedRegex(@"\b_([^_\n]+)_\b")]
    private static partial Regex ItalicUnderscore();

    [GeneratedRegex(@"^\s*[*•]\s+", RegexOptions.Multiline)]
    private static partial Regex Bullet();

    [GeneratedRegex(@"\*{2,}")]
    private static partial Regex StrayStars();
}
