using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A story character's voice picker words cost and refusal as the Settings Voice row does for the same provider (#1069).</summary>
public class AStoryVoicePickerSaysWhatTheSettingsRowSaysTests
{
    private static SettingAudition Audition(bool hasKey) =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Audition = (_, _, _) => Task.CompletedTask,
            HasKey = _ => hasKey,
        }).Settings.Single(row => row.Key == SpeechCapability.VoiceKey).Audition!;

    private static D47Settings On(string provider) =>
        D47Settings.Defaults with { Speech = D47Settings.Defaults.Speech with { Provider = provider } };

    public static TheoryData<string> Speakers() =>
        [.. TtsProviderCatalog.All.Where(provider => provider.Speaks).Select(provider => provider.Id)];

    [Theory]
    [MemberData(nameof(Speakers))]
    public void CostAndLineCostAreTheSettingsRowsForThatProvider(string providerId)
    {
        var settings = On(providerId);
        var row = Audition(hasKey: true);

        Assert.Equal(row.Cost(settings), SpeechCapability.AuditionCost(settings, providerId));
        Assert.Equal(row.LineCost!(settings), SpeechCapability.LineCost(settings, providerId));
    }

    [Theory]
    [MemberData(nameof(Speakers))]
    public void AMissingKeyIsRefusedInTheSettingsRowsWords(string providerId)
    {
        var settings = On(providerId);

        Assert.Equal(
            Audition(hasKey: false).Unavailable!(settings),
            SpeechCapability.AuditionUnavailable(providerId, hasKey: false));
    }

    [Fact]
    public void ElevenLabsWithoutAKeySaysItNeedsOne() =>
        Assert.Equal(
            "ElevenLabs needs an API key before it will speak.",
            SpeechCapability.AuditionUnavailable(TtsProviderCatalog.ElevenLabsId, hasKey: false));

    [Fact]
    public void AFreeProviderIsNeverRefusedForAKey() =>
        Assert.Null(SpeechCapability.AuditionUnavailable(TtsProviderCatalog.EdgeId, hasKey: false));

    [Fact]
    public void AListedCatalogueOffersTheGenderFilterAndItsCustomOption()
    {
        var catalogue = VoiceCatalogue.Of(
        [
            new VoiceInfo("a", "Ava", "en-US", "Female"),
            new VoiceInfo("b", "Ben", "en-US", "Male"),
            new VoiceInfo("c", "Mine", "en-US") { Custom = true },
        ]);

        var facet = SpeechCapability.GenderFacet(catalogue)!;

        Assert.Equal(["All", "Female", "Male", "Unlabelled", "Custom"], facet.Options.Select(option => option.Label));
        Assert.True(facet.Options.Single(option => option.Label == "Female").Matches!("a"));
        Assert.False(facet.Options.Single(option => option.Label == "Female").Matches!("b"));
        Assert.True(facet.Options.Single(option => option.Label == "Custom").Matches!("c"));
    }

    [Fact]
    public void ACatalogueWithNothingToFilterOnOffersNoFacet() =>
        Assert.Null(SpeechCapability.GenderFacet(VoiceCatalogue.Of([new VoiceInfo("a", "Ava", "en-US")])));

    [Fact]
    public void OnlyAVoiceWithAPreviewUrlHasAFreeSample()
    {
        var catalogue = VoiceCatalogue.Of(
        [
            new VoiceInfo("a", "Ava", "en-US") { PreviewUrl = "https://example.test/a.mp3" },
            new VoiceInfo("b", "Ben", "en-US"),
        ]);

        Assert.True(catalogue.HasPreview("A"));
        Assert.False(catalogue.HasPreview("b"));
        Assert.False(catalogue.HasPreview("missing"));
    }
}
