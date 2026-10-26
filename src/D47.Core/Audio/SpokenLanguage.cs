using System.Text.RegularExpressions;

namespace D47.Core.Audio;

/// <summary>The language a line is spoken in, as a BCP 47 tag where null means English.</summary>
public static partial class SpokenLanguage
{
    public const string English = "en";

    public const string Japanese = "ja";

    /// <summary>Whether <paramref name="language"/> is English, null included.</summary>
    public static bool IsEnglish(string? language) =>
        language is null || string.Equals(Primary(language), English, StringComparison.OrdinalIgnoreCase);

    /// <summary>The English name of <paramref name="language"/> as the Commander reads it; the tag itself for one D47 has no name for.</summary>
    public static string NameOf(string? language) =>
        IsEnglish(language) ? "English" : string.Equals(Primary(language!), Japanese, StringComparison.OrdinalIgnoreCase) ? "Japanese" : language!;

    /// <summary>"ja" from "ja-JP".</summary>
    public static string Primary(string language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return language.Split('-', 2)[0];
    }

    /// <summary>The locale an Edge voice id starts with — "ja-JP" from "ja-JP-NanamiNeural" — or null where it starts with none.</summary>
    public static string? LocaleOfVoiceId(string? voiceId) =>
        voiceId is { Length: > 0 } && Locale().Match(voiceId) is { Success: true } match ? match.Groups["locale"].Value : null;

    [GeneratedRegex(@"^(?<locale>[a-z]{2,3}(?:-[A-Z][a-z]{3})?-[A-Z]{2})-", RegexOptions.CultureInvariant)]
    private static partial Regex Locale();
}
