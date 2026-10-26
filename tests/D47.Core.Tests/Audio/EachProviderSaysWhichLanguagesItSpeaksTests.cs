using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Which languages each voice provider speaks, as <see cref="TtsProviderInfo.SpeaksIn"/> states it (#1752).</summary>
public class EachProviderSaysWhichLanguagesItSpeaksTests
{
    [Theory]
    [InlineData(TtsProviderCatalog.ElevenLabsId)]
    [InlineData(TtsProviderCatalog.OpenAiId)]
    [InlineData(TtsProviderCatalog.CartesiaId)]
    public void AHostedProviderSpeaksJapaneseAsWellAsEnglish(string id)
    {
        var provider = TtsProviderCatalog.Selected(id);

        Assert.True(provider.SpeaksIn(null, "any-voice"));
        Assert.True(provider.SpeaksIn("ja", "any-voice"));
    }

    [Theory]
    [InlineData(TtsProviderCatalog.KokoroId, "af_heart")]
    [InlineData(TtsProviderCatalog.ChatterboxId, "0001")]
    public void ALocalProviderSpeaksEnglishOnly(string id, string voice)
    {
        var provider = TtsProviderCatalog.Selected(id);

        Assert.True(provider.SpeaksIn(null, voice));
        Assert.True(provider.SpeaksIn("en", voice));
        Assert.False(provider.SpeaksIn("ja", voice));
    }

    [Theory]
    [InlineData("ja-JP-NanamiNeural", "ja", true)]
    [InlineData("ja-JP-NanamiNeural", null, false)]
    [InlineData("en-US-AriaNeural", "ja", false)]
    [InlineData("en-GB-SoniaNeural", null, true)]
    [InlineData("en-GB-SoniaNeural", "en-GB", true)]
    public void AnEdgeVoiceSpeaksTheLanguageOfItsLocale(string voice, string? language, bool speaks)
    {
        Assert.Equal(speaks, TtsProviderCatalog.Edge.SpeaksIn(language, voice));
    }

    [Theory]
    [InlineData("ja-JP-NanamiNeural", "ja-JP")]
    [InlineData("zh-CN-liaoning-XiaobeiNeural", "zh-CN")]
    [InlineData("iu-Cans-CA-SiqiniqNeural", "iu-Cans-CA")]
    [InlineData("af_heart", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AVoiceIdsLocaleIsReadFromItsStart(string? voice, string? locale)
    {
        Assert.Equal(locale, SpokenLanguage.LocaleOfVoiceId(voice));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("en", true)]
    [InlineData("en-GB", true)]
    [InlineData("EN", true)]
    [InlineData("ja", false)]
    [InlineData("ja-JP", false)]
    public void NullAndEveryEnglishTagAreEnglish(string? language, bool english)
    {
        Assert.Equal(english, SpokenLanguage.IsEnglish(language));
    }
}
