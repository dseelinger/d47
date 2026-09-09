using System.Text.RegularExpressions;

namespace D47.Core.Audio;

/// <summary>
/// Delivery direction a model writes in square brackets — <c>[sighs]</c>, <c>[alarmed]</c> — for a
/// provider that performs it, removed for every provider that would read it out loud (#291).
/// </summary>
public static partial class AudioTags
{
    /// <summary>
    /// The written sentence with its direction removed, and the spacing a removal leaves tidied.
    /// </summary>
    public static string Strip(string sentence) =>
        Has(sentence) ? Tidy(Tag().Replace(sentence, string.Empty)) : sentence;

    /// <summary>Whether there is any direction in here at all.</summary>
    public static bool Has(string sentence) =>
        sentence.Contains('[', StringComparison.Ordinal) && Tag().IsMatch(sentence);

    /// <summary>The direction found, in the order written, without their brackets.</summary>
    public static IReadOnlyList<string> In(string sentence) =>
        Has(sentence)
            ? [.. Tag().Matches(sentence).Select(match => match.Groups["tag"].Value)]
            : [];

    /// <summary>The direction kept only if it will be performed, and removed otherwise.</summary>
    public static string For(string sentence, bool performed) =>
        performed ? sentence : Strip(sentence);

    /// <summary>
    /// A run of spaces where a tag used to be, and a space before punctuation that followed one.
    /// </summary>
    private static string Tidy(string sentence) =>
        Gap().Replace(sentence, " ").Replace(" ,", ",", StringComparison.Ordinal).Trim();

    /// <summary>One bracketed direction.</summary>
    [GeneratedRegex(@"\[(?<tag>[A-Za-z][A-Za-z0-9 '’-]{0,39})\](?!\()")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Gap();
}
