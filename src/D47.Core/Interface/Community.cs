using D47.Core.Capabilities.Builtin;

namespace D47.Core.Interface;

/// <summary>d47's community page and the transcript line that links to it.</summary>
public static class Community
{
    public const string Url = "https://dseelinger.github.io/d47/community.html";

    public const string SourceKey = "community.discord";

    public const string Phrase = "D47 Discord";

    /// <summary>The place for <see cref="Phrase"/> in <paramref name="text"/> when <paramref name="sourceKey"/> is <see cref="SourceKey"/>; otherwise null.</summary>
    public static PanelPlace? Find(string text, string? sourceKey)
    {
        if (sourceKey != SourceKey || string.IsNullOrEmpty(text))
        {
            return null;
        }

        var at = text.IndexOf(Phrase, StringComparison.Ordinal);

        return at < 0
            ? null
            : new PanelPlace(at, Phrase.Length, PanelTab.Settings, SettingKey: AboutCapability.CommunityKey, Url: Url);
    }
}
