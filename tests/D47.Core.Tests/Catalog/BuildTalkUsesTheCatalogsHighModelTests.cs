using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Catalog;

public class BuildTalkUsesTheCatalogsHighModelTests
{
    [Fact]
    public void AnthropicUsesItsHighModel()
    {
        Assert.Equal("claude-opus-5-5", HighModels.Resolve(new D47Settings()));
    }

    [Fact]
    public void OpenAiUsesItsHighModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId } };

        Assert.Equal("gpt-6-astra", HighModels.Resolve(settings));
    }

    [Fact]
    public void ACustomEndpointKeepsTheConversationModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Endpoint = "http://localhost:1234", Model = "local-model" } };

        Assert.Equal("local-model", HighModels.Resolve(settings));
    }
}
