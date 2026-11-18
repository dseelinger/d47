using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class AJapaneseCharacterOnChatterboxNeedsChatterboxJapaneseTests
{
    private const string Needed = "Download Chatterbox Japanese, about 1,483 MB, under Settings, Providers, Speech synthesis.";

    private static readonly StorySecret Japanese = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker
            {
                Id = "ren-ja", Name = "Hana", Who = "A pilot.", Provider = TtsProviderCatalog.EdgeId, Voice = "ja-JP-NanamiNeural", Pinned = true, Language = "ja",
            },
        ],
    };

    private static readonly string Key = $"{Japanese.Id}.ren-ja";

    private static Dictionary<string, StoryVoiceChoice> Chose(string key, string provider, string voice) =>
        new(StringComparer.Ordinal) { [key] = new StoryVoiceChoice(provider, voice) };

    private static readonly CastVoicesHere WithoutIt = new(true, true, true) { ChatterboxJapanese = false };

    [Fact]
    public void AChatterboxVoiceForTheJapaneseCharacterNamesTheDownload()
    {
        var choices = Chose(Key, TtsProviderCatalog.ChatterboxId, "marlow");

        Assert.Equal([Needed], StoryVoices.Missing(Japanese, null, WithoutIt, choices));
        Assert.True(StoryVoices.SpeaksJapaneseOnChatterbox(Japanese, null, choices));
    }

    [Fact]
    public void OnceItIsDownloadedNothingIsMissing()
    {
        var choices = Chose(Key, TtsProviderCatalog.ChatterboxId, "marlow");

        Assert.Empty(StoryVoices.Missing(Japanese, null, CastVoicesHere.All, choices));
    }

    [Fact]
    public void AnEnglishCharacterOnChatterboxDoesNotNeedIt()
    {
        var choices = Chose($"{Japanese.Id}.dock-hand", TtsProviderCatalog.ChatterboxId, "marlow");

        Assert.Empty(StoryVoices.Missing(Japanese, null, WithoutIt, choices));
        Assert.False(StoryVoices.SpeaksJapaneseOnChatterbox(Japanese, null, choices));
    }

    [Fact]
    public void TheJapaneseCharacterOnAnotherProviderDoesNotNeedIt()
    {
        var choices = Chose(Key, TtsProviderCatalog.EdgeId, "ja-JP-KeitaNeural");

        Assert.Empty(StoryVoices.Missing(Japanese, null, WithoutIt, choices));
    }

    [Fact]
    public void TheChoiceIsKeptWithTheStorysVoiceBehindIt()
    {
        var voice = StoryVoices.Voice(Japanese.Cast[^1], Japanese.Cast[^1].Shown(Japanese.Id, null), Chose(Key, TtsProviderCatalog.ChatterboxId, "marlow"));

        Assert.Equal(TtsProviderCatalog.ChatterboxId, voice.ProviderId);
        Assert.Equal("ja-JP-NanamiNeural", voice.Fallback?.VoiceId);
    }
}
