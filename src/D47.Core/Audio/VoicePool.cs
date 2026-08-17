namespace D47.Core.Audio;

/// <summary>
/// Which of a provider's voices a re-voiced sender may be drawn from
/// (remediation.md, "Named NPCs should each use a different voice").
/// <para>
/// <b>The filter is about language, and it was reading an accent as one.</b> Edge tags every
/// voice with a real locale — <c>en-GB</c>, <c>de-DE</c> — and it offers several hundred across
/// every language it supports, so drawing a wingmate's voice from all of them means most
/// Commanders hear their wing in a language they do not speak. ElevenLabs tags an <em>accent</em>
/// in the same slot, because its model is multilingual and a voice does not belong to a locale:
/// "american", "british", "multilingual". Keeping only what started with "en" therefore discarded
/// 472 of a 473-voice account and left a pool of one, so every NPC in a system shared a single
/// voice and the per-name assignment had nothing to assign.
/// </para>
/// <para>
/// So a value is only filtered on when it is a locale. Anything that is not one is a label this
/// class has no opinion about, and a voice is not thrown away for wearing one.
/// </para>
/// </summary>
public static class VoicePool
{
    /// <summary>The ids a sender may be given, in the order the provider listed them.</summary>
    public static IReadOnlyList<string> From(IEnumerable<VoiceInfo> voices) =>
        [.. voices.Where(voice => Eligible(voice.Locale)).Select(voice => voice.Id)];

    /// <summary>
    /// Whether a voice tagged this way belongs in the pool: untagged, tagged with something that
    /// is not a locale at all, or tagged with an English one.
    /// </summary>
    public static bool Eligible(string? locale) =>
        locale is not { Length: > 0 } tag
        || !IsLocale(tag)
        || tag.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this reads as a language tag rather than as a word.
    /// <para>
    /// A language subtag is two or three letters, optionally followed by region and script
    /// subtags after a hyphen or an underscore. "en", "en-GB" and "de-DE" are locales;
    /// "american", "british" and "multilingual" are words that happen to be in the same field.
    /// </para>
    /// </summary>
    private static bool IsLocale(string tag)
    {
        var separator = tag.IndexOfAny(['-', '_']);
        var language = separator < 0 ? tag : tag[..separator];

        return language.Length is 2 or 3 && language.All(char.IsAsciiLetter);
    }
}
