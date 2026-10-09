using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The keys step derives its keys from the chosen providers and asks for each secret once.</summary>
public class TheSetupWizardAsksForEachKeyOnceTests
{
    private static SetupChoices Choose(string conversation, string voice, string listening) =>
        new(conversation, voice, listening, "Scroll", null, ListeningCapability.HoldMode);

    [Trait("Category", "Integration")]
    [Fact]
    public void OneOpenAiKeyServesConversationVoiceAndListening()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var keys = FirstRun.Keys(
            surface.Settings,
            Choose(LlmProviderCatalog.OpenAiId, TtsProviderCatalog.OpenAiId, SttProviderCatalog.OpenAiId));

        var key = Assert.Single(keys);
        Assert.Equal("openai.apiKey", key.SecretName);
        Assert.Equal([SetupSlot.Conversation, SetupSlot.Voice, SetupSlot.Listening], key.Serves);
        Assert.Equal(3, key.Egress.Count);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void OneElevenLabsKeyServesVoiceAndListening()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var keys = FirstRun.Keys(
            surface.Settings,
            Choose(LlmProviderCatalog.AnthropicId, TtsProviderCatalog.ElevenLabsId, SttProviderCatalog.ElevenLabsId));

        Assert.Equal(["anthropic.apiKey", TtsProviderCatalog.ElevenLabsKeySecretName], keys.Select(key => key.SecretName));
        Assert.Equal([SetupSlot.Voice, SetupSlot.Listening], keys[1].Serves);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheDefaultsNeedOnlyTheAnthropicKey()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var keys = FirstRun.Keys(surface.Settings, SetupChoices.From(surface.Settings.Current));

        Assert.Equal("anthropic.apiKey", Assert.Single(keys).SecretName);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void FreeChoicesNeedNoKeys()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Empty(FirstRun.Keys(
            surface.Settings,
            Choose(LlmProviderCatalog.NoneId, TtsProviderCatalog.KokoroId, SttProviderCatalog.LocalId)));

        // A compatible endpoint's key is optional, so it is not asked for.
        Assert.Empty(FirstRun.Keys(
            surface.Settings,
            Choose(LlmProviderCatalog.OpenAiCompatibleId, TtsProviderCatalog.EdgeId, SttProviderCatalog.LocalId)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void EachKeyUsesTheRegistrysOwnRow()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var registered = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        var keys = FirstRun.Keys(
            surface.Settings,
            Choose(LlmProviderCatalog.AnthropicId, TtsProviderCatalog.CartesiaId, SttProviderCatalog.DeepgramId));

        Assert.Equal(3, keys.Count);
        Assert.All(keys, key => Assert.Same(registered[key.Row.Key], key.Row));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AKeyDisclosesItsOwnProviderRatherThanTheSelectedOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(TtsProviderCatalog.EdgeId, surface.Settings.Current.Speech.Provider);

        var voice = FirstRun.Keys(
                surface.Settings,
                Choose(LlmProviderCatalog.NoneId, TtsProviderCatalog.ElevenLabsId, SttProviderCatalog.LocalId))
            .Single();

        var egress = Assert.Single(voice.Egress);
        Assert.Equal(TtsProviderCatalog.ElevenLabs.Destination, egress.Destination);
        Assert.Equal(EgressDisclosure.TextToSpeechFor(TtsProviderCatalog.ElevenLabs).Summary, egress.Summary);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheConversationKeyDisclosesTheChosenProvidersEndpoint()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var key = FirstRun.Keys(
                surface.Settings,
                Choose(LlmProviderCatalog.OpenAiId, TtsProviderCatalog.EdgeId, SttProviderCatalog.LocalId))
            .Single();

        var egress = Assert.Single(key.Egress);
        Assert.True(egress.Active);
        Assert.Equal(LlmProviderCatalog.Find(LlmProviderCatalog.OpenAiId)!.DefaultEndpoint, egress.Destination);
    }

    [Fact]
    public void EveryKeyTheWizardCanAskForHasAPageToGetItFrom()
    {
        var secrets = LlmProviderCatalog.All.Where(provider => provider.NeedsKey).Select(provider => provider.KeySecretName)
            .Concat(TtsProviderCatalog.All.Select(provider => provider.KeySecretName))
            .Concat(SttProviderCatalog.All.Select(provider => provider.KeySecretName))
            .OfType<string>()
            .Distinct();

        Assert.All(secrets, secret => Assert.True(FirstRun.KeyPages.ContainsKey(secret), secret));
    }
}
