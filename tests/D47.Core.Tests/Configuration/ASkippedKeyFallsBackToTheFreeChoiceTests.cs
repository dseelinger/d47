using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>START saves each slot whose key was skipped as that slot's free choice.</summary>
public class ASkippedKeyFallsBackToTheFreeChoiceTests
{
    private static readonly SetupChoices AllPaid = new(
        LlmProviderCatalog.AnthropicId,
        TtsProviderCatalog.ElevenLabsId,
        SttProviderCatalog.DeepgramId,
        "F9",
        null,
        ListeningCapability.ToggleMode);

    [Fact]
    public void EachSlotWithoutItsKeyTakesItsFreeChoice()
    {
        var effective = FirstRun.Effective(AllPaid, _ => false);

        Assert.Equal(LlmProviderCatalog.NoneId, effective.Conversation);
        Assert.Equal(TtsProviderCatalog.EdgeId, effective.Voice);
        Assert.Equal(SttProviderCatalog.LocalId, effective.Listening);
    }

    [Fact]
    public void AStoredKeyKeepsItsChoice()
    {
        var effective = FirstRun.Effective(AllPaid, name => name == TtsProviderCatalog.ElevenLabsKeySecretName);

        Assert.Equal(LlmProviderCatalog.NoneId, effective.Conversation);
        Assert.Equal(TtsProviderCatalog.ElevenLabsId, effective.Voice);
        Assert.Equal(SttProviderCatalog.LocalId, effective.Listening);
    }

    [Fact]
    public void StartingWithTheKeySkippedLeavesNothingToAskForNextLaunch()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.True(FirstRun.IsNeeded(
            LlmProviderCatalog.Selected(surface.Settings.Current.Llm.Provider), surface.Secrets.Has));

        Assert.Empty(FirstRun.Apply(surface.Settings, AllPaid));

        var saved = surface.Settings.Current;
        Assert.Equal(LlmProviderCatalog.NoneId, saved.Llm.Provider);
        Assert.Equal(TtsProviderCatalog.EdgeId, saved.Speech.Provider);
        Assert.Equal(SttProviderCatalog.LocalId, saved.Listening.Provider);
        Assert.Equal("F9", saved.Listening.PushToTalkKey);
        Assert.Equal(ListeningCapability.ToggleMode, saved.Listening.Mode);

        Assert.False(FirstRun.IsNeeded(LlmProviderCatalog.Selected(saved.Llm.Provider), surface.Secrets.Has));
    }

    [Fact]
    public void StartingWithTheKeyStoredSavesThePaidChoice()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.Secrets.Set("anthropic.apiKey", "sk-test");

        Assert.Empty(FirstRun.Apply(surface.Settings, AllPaid));

        Assert.Equal(LlmProviderCatalog.AnthropicId, surface.Settings.Current.Llm.Provider);
        Assert.False(FirstRun.IsNeeded(
            LlmProviderCatalog.Selected(surface.Settings.Current.Llm.Provider), surface.Secrets.Has));
    }

    [Fact]
    public void TheReadyStepDisclosesWhatWillBeSavedRatherThanWhatWasPicked()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var without = FirstRun.Destinations(surface.Settings, AllPaid, _ => false);
        Assert.DoesNotContain(without, entry => entry.Id == EgressDisclosure.LanguageModel);
        Assert.Contains(without, entry => entry.Id == EgressDisclosure.TextToSpeech
            && entry.Destination == TtsProviderCatalog.Edge.Destination);

        var with = FirstRun.Destinations(surface.Settings, AllPaid, _ => true);
        Assert.Contains(with, entry => entry.Id == EgressDisclosure.LanguageModel);
        Assert.Contains(with, entry => entry.Id == EgressDisclosure.SpeechRecognition
            && entry.Destination == SttProviderCatalog.Deepgram.Destination);
    }
}
