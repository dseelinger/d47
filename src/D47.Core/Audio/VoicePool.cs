namespace D47.Core.Audio;

/// <summary>
/// What a provider says about a voice's gender, including saying nothing
/// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
/// <para>
/// <b>Three states rather than a boolean, because the third one is common.</b> Some providers tag
/// every voice and some tag none, and "not known to be a woman's" is a different fact from "a
/// man's" — <see cref="VoicePool.Feminine"/> may treat them alike for casting, and a Commander
/// looking at a list may not.
/// </para>
/// </summary>
public enum VoiceGender
{
    Unlabelled,
    Feminine,
    Masculine,
}

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
    /// Which of them are a woman's, by id, so a sender whose name reads as a woman's can be
    /// given one.
    /// <para>
    /// Both providers tag this and neither agrees on the spelling — Edge writes "Female",
    /// ElevenLabs writes "female" — so the comparison is case-insensitive and anything else,
    /// including nothing at all, is left out. Absent means "not known to be", which for this
    /// purpose is the same as a man's.
    /// </para>
    /// <para>
    /// <b>The comparison itself is <see cref="GenderOf"/> since
    /// <a href="https://github.com/dseelinger/d47/issues/146">#146</a></b>, so the voice picker's
    /// gender filter and this casting rule are the same rule read twice rather than two rules that
    /// agree today. Two filters disagreeing about who is female would be worse than the bug that
    /// prompted the picker to have one.
    /// </para>
    /// </summary>
    public static IReadOnlySet<string> Feminine(IEnumerable<VoiceInfo> voices) =>
        new HashSet<string>(
            voices
                .Where(voice => Eligible(voice.Locale) && GenderOf(voice.Gender) == VoiceGender.Feminine)
                .Select(voice => voice.Id),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a provider's gender tag says, as something with three states rather than a boolean
    /// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
    /// <para>
    /// <b>Case-insensitive equality on the whole tag, and nothing cleverer.</b> The two providers
    /// disagree on capitalisation and on nothing else — Edge writes "Female" and ElevenLabs writes
    /// "female" — so a substring or prefix test would buy nothing and would read "female" as male,
    /// which is the exact bug in the picker that #146 is about.
    /// </para>
    /// <para>
    /// <b><see cref="VoiceGender.Unlabelled"/> is a real third answer.</b> For casting it collapses
    /// into "not known to be a woman's", which is what <see cref="Feminine"/> does with it; for a
    /// Commander reading a list it must not, because a filter that silently hid every untagged
    /// voice would look like a shorter list rather than like a filter.
    /// </para>
    /// </summary>
    public static VoiceGender GenderOf(string? tag) => tag switch
    {
        not null when string.Equals(tag, "female", StringComparison.OrdinalIgnoreCase) => VoiceGender.Feminine,
        not null when string.Equals(tag, "male", StringComparison.OrdinalIgnoreCase) => VoiceGender.Masculine,
        _ => VoiceGender.Unlabelled,
    };

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
