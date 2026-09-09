using Anthropic.Models.Messages;
using D47.Core.Conversation;
using D47.Llm;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the web search declaration actually looks like on the wire.</summary>
public class WebSearchDeclarationTests
{
    private static LlmRequest Request(string model, bool webSearch) => new()
    {
        Model = model,
        Effort = ThinkingEffort.Medium,
        Sampling = LlmSampling.Conversation,
        WebSearch = webSearch,
        Prompt = new PromptAssembly
        {
            Persona = null,
            AboutMe = null,
            History = [new ConversationMessage(ConversationRole.User, "what are people flying now?")],
        },
    };

    private static IReadOnlyList<string> ToolTypeNames(MessageCreateParams parameters) =>
        [.. parameters.Tools!.Select(tool => tool.Value?.GetType().Name ?? "unknown")];

    [Fact]
    public void NoWebSearchToolIsDeclaredWhenTheSettingIsOff()
    {
        var parameters = new AnthropicLlmProvider("key").BuildParameters(Request("claude-opus-5", webSearch: false));

        Assert.DoesNotContain(ToolTypeNames(parameters), name => name.StartsWith("WebSearchTool", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-opus-4-8")]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-fable-5")]
    public void CurrentModelsGetTheDynamicFilteringTool(string model)
    {
        var parameters = new AnthropicLlmProvider("key").BuildParameters(Request(model, webSearch: true));

        Assert.Contains("WebSearchTool20260318", ToolTypeNames(parameters));
    }

    /// <summary>Haiku 4.5 predates dynamic filtering.</summary>
    [Fact]
    public void HaikuGetsTheBasicToolBecauseItCannotRunTheSearchFromCodeExecution()
    {
        var parameters = new AnthropicLlmProvider("key").BuildParameters(Request("claude-haiku-4-5", webSearch: true));

        Assert.Contains("WebSearchTool20250305", ToolTypeNames(parameters));
        Assert.DoesNotContain("WebSearchTool20260318", ToolTypeNames(parameters));
    }

    /// <summary>
    /// Searching is capped, and the cap is asserted rather than left to a comment: it is the only thing
    /// standing between a research-minded turn and ten billed searches, and it is also what keeps a
    /// turn short enough not to be paused part-way.
    /// </summary>
    [Fact]
    public void SearchesAreCappedForTheTurn()
    {
        var parameters = new AnthropicLlmProvider("key").BuildParameters(Request("claude-opus-5", webSearch: true));

        var tool = Assert.IsType<WebSearchTool20260318>(
            parameters.Tools!.Select(t => t.Value).First(v => v is WebSearchTool20260318));

        Assert.Equal(3, tool.MaxUses);
    }

    /// <summary>The declaration goes on the end.</summary>
    [Fact]
    public void TheDeclarationIsAppendedAfterTheRegisteredTools()
    {
        var request = Request("claude-opus-5", webSearch: true) with
        {
            Prompt = new PromptAssembly
            {
                Persona = null,
                AboutMe = null,
                History = [new ConversationMessage(ConversationRole.User, "hello")],
                Tools =
                [
                    new ToolAdvertisement("find_engineer", "Find an engineer.", "{\"type\":\"object\"}"),
                ],
            },
        };

        var names = ToolTypeNames(new AnthropicLlmProvider("key").BuildParameters(request));

        Assert.Equal(2, names.Count);
        Assert.Equal("WebSearchTool20260318", names[^1]);
    }

    /// <summary>
    /// A gateway cannot be assumed to offer a server-side tool — Amazon Bedrock offers none at all — so
    /// the capability reports false there and the turn loop never asks for it.
    /// </summary>
    [Fact]
    public void AGatewayEndpointReportsNoWebSearchCapability()
    {
        Assert.False(
            new AnthropicLlmProvider("key", "http://localhost:8787").CapabilitiesFor("claude-opus-5").SupportsWebSearch);

        Assert.True(new AnthropicLlmProvider("key").CapabilitiesFor("claude-opus-5").SupportsWebSearch);
    }
}

/// <summary>The declaration as bytes rather than as a.NET type.</summary>
public class WebSearchWireShapeTests
{
    private static string ToolsJson(string model)
    {
        var parameters = new AnthropicLlmProvider("key").BuildParameters(new LlmRequest
        {
            Model = model,
            Effort = ThinkingEffort.Medium,
            Sampling = LlmSampling.Conversation,
            WebSearch = true,
            Prompt = new PromptAssembly
            {
                Persona = null,
                AboutMe = null,
                History = [new ConversationMessage(ConversationRole.User, "what changed in the last patch?")],
            },
        });

        return System.Text.Json.JsonSerializer.Serialize(parameters.Tools);
    }

    [Fact]
    public void TheCurrentToolSerialisesAsTheDocumentedObject()
    {
        var json = ToolsJson("claude-opus-5");

        Assert.Contains("\"type\":\"web_search_20260318\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"web_search\"", json, StringComparison.Ordinal);
        Assert.Contains("\"max_uses\":3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBasicToolSerialisesAsItsOwnType()
    {
        var json = ToolsJson("claude-haiku-4-5");

        Assert.Contains("\"type\":\"web_search_20250305\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"web_search\"", json, StringComparison.Ordinal);
    }
}
