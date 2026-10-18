using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The loopback language-model entry says it may ask LM Studio about its models and reload one.</summary>
public class TheEgressListNamesTheStoryReloadTests
{
    private static D47Settings At(string endpoint) =>
        new() { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiCompatibleId, Endpoint = endpoint } };

    [Fact]
    public void ALoopbackEndpointNamesTheReload()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, At("http://localhost:1234/v1"), llmKeyPresent: true);

        Assert.Contains("D47 also asks it which models are loaded and, before writing a story, reloads the story model with a larger context", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AHostedEndpointDoesNot()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, At("https://example.com/v1"), llmKeyPresent: true);

        Assert.DoesNotContain("which models are loaded", entry.What, StringComparison.Ordinal);
    }
}
