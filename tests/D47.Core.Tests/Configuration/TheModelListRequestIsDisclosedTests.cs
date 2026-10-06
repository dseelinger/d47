using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheModelListRequestIsDisclosedTests
{
    [Fact]
    public void TheLanguageModelEntrySaysTheListIsAskedForAtStartupAndOnKeyChange()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.AnthropicId } };

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: true),
            entry => entry.Id == EgressDisclosure.LanguageModel);

        Assert.True(entry.Active);
        Assert.Contains(
            "D47 also asks for that list at startup and whenever the provider, its endpoint or its key changes",
            entry.What,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheElevenLabsEntrySaysTheListIsAskedForWithTheVoices()
    {
        Assert.Contains(
            "Each time D47 fetches the voice list, it also asks ElevenLabs for its list of models, with the same key",
            EgressDisclosure.TextToSpeechFor(TtsProviderCatalog.ElevenLabs).What,
            StringComparison.Ordinal);
    }
}
