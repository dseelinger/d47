using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AJapaneseCastMemberOnlyTakesJapaneseVoicesTests
{
    private static readonly StorySpeaker Member = new()
    {
        Id = "ren", Name = "Ren", Who = "A pilot.", Provider = TtsProviderCatalog.EdgeId, Voice = "ja-JP-NanamiNeural", Pinned = true, Language = "ja",
    };

    private static readonly StorySpeakerShown Shown = new("ren", "Ren", TtsProviderCatalog.EdgeId, "ja-JP-NanamiNeural", "s.ren") { Pinned = true };

    [Fact]
    public void ChatterboxDoesNotSpeakJapaneseAndEveryOtherProviderDoes()
    {
        Assert.True(TtsProviderCatalog.Kokoro.CanSpeak("ja"));
        Assert.False(TtsProviderCatalog.Chatterbox.CanSpeak("ja"));
        Assert.True(TtsProviderCatalog.Edge.CanSpeak("ja"));
        Assert.True(TtsProviderCatalog.ElevenLabs.CanSpeak("ja"));
        Assert.True(TtsProviderCatalog.OpenAi.CanSpeak("ja"));
        Assert.True(TtsProviderCatalog.Cartesia.CanSpeak("ja"));
    }

    [Fact]
    public void EveryProviderSpeaksEnglish()
    {
        Assert.All(TtsProviderCatalog.All.Where(provider => provider.Speaks), provider => Assert.True(provider.CanSpeak(null)));
    }

    [Fact]
    public void EdgeSpeaksJapaneseOnlyInAJapaneseVoice()
    {
        Assert.True(TtsProviderCatalog.Edge.SpeaksIn("ja", "ja-JP-KeitaNeural"));
        Assert.False(TtsProviderCatalog.Edge.SpeaksIn("ja", "en-US-AriaNeural"));
    }

    [Fact]
    public void KokoroSpeaksJapaneseOnlyInAJapaneseVoice()
    {
        Assert.True(TtsProviderCatalog.Kokoro.SpeaksIn("ja", "jf_alpha"));
        Assert.True(TtsProviderCatalog.Kokoro.SpeaksIn("ja", "jm_kumo"));
        Assert.False(TtsProviderCatalog.Kokoro.SpeaksIn("ja", "af_river"));
        Assert.False(TtsProviderCatalog.Kokoro.SpeaksIn(null, "jf_alpha"));
        Assert.False(TtsProviderCatalog.Kokoro.CanSpeak("fr"));
    }

    [Fact]
    public void AStoredChoiceThatDoesNotFitTheLanguageIsIgnored()
    {
        var kokoro = new Dictionary<string, StoryVoiceChoice> { [Shown.Picture] = new(StorySpeaker.Kokoro, "af_river") };
        var english = new Dictionary<string, StoryVoiceChoice> { [Shown.Picture] = new(TtsProviderCatalog.EdgeId, "en-US-AriaNeural") };
        var japanese = new Dictionary<string, StoryVoiceChoice> { [Shown.Picture] = new(TtsProviderCatalog.EdgeId, "ja-JP-KeitaNeural") };
        var kokoroJapanese = new Dictionary<string, StoryVoiceChoice> { [Shown.Picture] = new(StorySpeaker.Kokoro, "jf_alpha") };

        Assert.Equal("ja-JP-NanamiNeural", StoryVoices.Voice(Member, Shown, kokoro).VoiceId);
        Assert.Equal("ja-JP-NanamiNeural", StoryVoices.Voice(Member, Shown, english).VoiceId);
        Assert.Equal("ja-JP-KeitaNeural", StoryVoices.Voice(Member, Shown, japanese).VoiceId);

        var chosen = StoryVoices.Voice(Member, Shown, kokoroJapanese);
        Assert.Equal("jf_alpha", chosen.VoiceId);
        Assert.Equal("ja-JP-NanamiNeural", chosen.Fallback?.VoiceId);
    }
}
