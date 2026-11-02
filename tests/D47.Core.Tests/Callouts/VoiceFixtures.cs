using D47.Core.Audio;
using D47.Core.Callouts;

namespace D47.Core.Tests.Callouts;

/// <summary>Voice lists and the <see cref="VoiceCast"/> built from them.</summary>
public static class VoiceFixtures
{
    public static readonly VoiceInfo[] FourVoices =
    [
        new("gb-woman", "Sonia", "en-GB", "Female"),
        new("us-man", "Guy", "en-US", "Male"),
        new("ie-woman", "Emily", "en-IE", "Female"),
        new("au-man", "William", "en-AU", "Male"),
    ];

    public static VoiceCast Cast(VoiceInfo[] listed, string defaultVoice) => new()
    {
        Pool = VoicePool.From(listed),
        Feminine = VoicePool.Feminine(listed),
        British = VoicePool.British(listed),
        Voices = listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
        DefaultVoice = defaultVoice,
    };
}
