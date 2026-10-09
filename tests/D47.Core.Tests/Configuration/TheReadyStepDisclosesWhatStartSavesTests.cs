using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The Ready step's destinations are computed before anything is saved; after START the disclosure must
/// say the same thing, whatever the settings held before.
/// </summary>
public class TheReadyStepDisclosesWhatStartSavesTests
{
    public static TheoryData<string, string, string, bool> Choices() => new()
    {
        { LlmProviderCatalog.AnthropicId, TtsProviderCatalog.ElevenLabsId, SttProviderCatalog.DeepgramId, true },
        { LlmProviderCatalog.AnthropicId, TtsProviderCatalog.ElevenLabsId, SttProviderCatalog.DeepgramId, false },
        { LlmProviderCatalog.OpenAiId, TtsProviderCatalog.OpenAiId, SttProviderCatalog.OpenAiId, true },
        { LlmProviderCatalog.OpenAiCompatibleId, TtsProviderCatalog.KokoroId, SttProviderCatalog.LocalId, false },
        { LlmProviderCatalog.NoneId, TtsProviderCatalog.NoneId, SttProviderCatalog.LocalId, false },
    };

    [Trait("Category", "Integration")]
    [Theory]
    [MemberData(nameof(Choices))]
    public void AfterStartTheDisclosureMatchesTheReadyStep(string conversation, string voice, string listening, bool keys)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        // What a reopened wizard can find: a compatible endpoint pointed at this machine, and one slot
        // spoken by a provider other than the main one.
        Assert.True(surface.Settings.Apply(
            ConversationCapability.ProviderKey, LlmProviderCatalog.OpenAiCompatibleId, SettingsCaller.Panel).Ok);
        Assert.True(surface.Settings.Apply(
            ConversationCapability.EndpointKey, "http://127.0.0.1:1234/v1", SettingsCaller.Panel).Ok);
        Assert.True(surface.Settings.Apply(
            SpeechCapability.SlotProviderKey(VoiceGroups.Carrier), TtsProviderCatalog.KokoroId, SettingsCaller.Panel).Ok);

        if (keys)
        {
            foreach (var secret in new[] { "anthropic.apiKey", "openai.apiKey", TtsProviderCatalog.ElevenLabsKeySecretName, "deepgram.apiKey" })
            {
                surface.Secrets.Set(secret, "sk-test");
            }
        }

        var choices = new SetupChoices(conversation, voice, listening, "F9", null, ListeningCapability.HoldMode);

        var promised = FirstRun.Destinations(surface.Settings, choices);

        Assert.Empty(FirstRun.Apply(surface.Settings, choices));

        var current = surface.Settings.Current;
        var llmKey = LlmProviderCatalog.Selected(current.Llm.Provider) is { KeySecretName: { } name } && surface.Secrets.Has(name);

        var delivered = EgressDisclosure
            .For(current, llmKey, surface.Secrets.Has(CommunityGoalCapability.KeySecretName))
            .Where(entry => entry.Active);

        Assert.Equal(
            delivered.Select(entry => (entry.Id, entry.Destination, entry.Summary)),
            promised.Select(entry => (entry.Id, entry.Destination, entry.Summary)));
    }

    [Fact]
    public void AProviderNoCatalogKnowsIsReadAsItsFallback()
    {
        var choices = SetupChoices.From(new D47Settings
        {
            Llm = new LlmSettings { Provider = "retired" },
            Speech = new SpeechSettings { Provider = "retired" },
            Listening = new ListeningSettings { Provider = "retired" },
        });

        Assert.Equal(LlmProviderCatalog.NoneId, choices.Conversation);
        Assert.Equal(TtsProviderCatalog.EdgeId, choices.Voice);
        Assert.Equal(SttProviderCatalog.LocalId, choices.Listening);
    }
}
