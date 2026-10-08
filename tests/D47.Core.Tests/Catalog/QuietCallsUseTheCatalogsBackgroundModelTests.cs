using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Catalog;

public class QuietCallsUseTheCatalogsBackgroundModelTests
{
    [Fact]
    public void AnUnsetRowUsesTheCatalogsBackgroundModel()
    {
        var named = ModelCatalog.Embedded.BackgroundDefaultFor(LlmProviderCatalog.AnthropicId);

        Assert.Equal("claude-haiku-5-5", named);
        Assert.Equal(named, BackgroundModels.Resolve(new D47Settings()));
    }

    [Fact]
    public void ASetRowWinsOverTheCatalog()
    {
        var settings = new D47Settings { Llm = new LlmSettings { BackgroundModel = "claude-opus-5" } };

        Assert.Equal("claude-opus-5", BackgroundModels.Resolve(settings));
    }

    [Fact]
    public void ACustomEndpointKeepsTheConversationModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Endpoint = "http://localhost:1234", Model = "local-model" } };

        Assert.Equal("local-model", BackgroundModels.Resolve(settings));
    }

    [Fact]
    public void AProviderWithNoBackgroundModelKeepsTheConversationModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId, Model = "gpt-x" } };

        Assert.Equal("gpt-x", BackgroundModels.Resolve(settings));
    }
}
