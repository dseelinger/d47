namespace D47.Core.Audio;

/// <summary>What a provider says about a voice's gender, including saying nothing (#146).</summary>
public enum VoiceGender
{
    Unlabelled,
    Feminine,
    Masculine,
}

/// <summary>
/// Which of a provider's voices a re-voiced sender may be drawn from (remediation.md, "Named NPCs
/// should each use a different voice").
/// </summary>
public static class VoicePool
{
    /// <summary>The ids a sender may be given, in the order the provider listed them.</summary>
    public static IReadOnlyList<string> From(IEnumerable<VoiceInfo> voices) =>
        [.. voices.Where(voice => Eligible(voice.Locale)).Select(voice => voice.Id)];

    /// <summary>
    /// Which of them are a woman's, by id, so a sender whose name reads as a woman's can be given one.
    /// </summary>
    public static IReadOnlySet<string> Feminine(IEnumerable<VoiceInfo> voices) =>
        new HashSet<string>(
            voices
                .Where(voice => Eligible(voice.Locale) && GenderOf(voice.Gender) == VoiceGender.Feminine)
                .Select(voice => voice.Id),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a provider's gender tag says, as something with three states rather than a boolean (#146).
    /// </summary>
    public static VoiceGender GenderOf(string? tag) => tag switch
    {
        not null when string.Equals(tag, "female", StringComparison.OrdinalIgnoreCase) => VoiceGender.Feminine,
        not null when string.Equals(tag, "male", StringComparison.OrdinalIgnoreCase) => VoiceGender.Masculine,
        _ => VoiceGender.Unlabelled,
    };

    /// <summary>
    /// Whether a voice tagged this way belongs in the pool: untagged, tagged with something that is not
    /// a locale at all, or tagged with an English one.
    /// </summary>
    public static bool Eligible(string? locale) =>
        locale is not { Length: > 0 } tag
        || !IsLocale(tag)
        || tag.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this reads as a language tag rather than as a word.</summary>
    private static bool IsLocale(string tag)
    {
        var separator = tag.IndexOfAny(['-', '_']);
        var language = separator < 0 ? tag : tag[..separator];

        return language.Length is 2 or 3 && language.All(char.IsAsciiLetter);
    }
}
