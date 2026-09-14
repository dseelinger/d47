using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>An opaque block goes back to its own provider verbatim and in place, and to no other.</summary>
public class ProviderBlocksKeepTheirPlaceTests
{
    private const string SearchCall =
        """{"type":"server_tool_use","id":"srvtoolu_1","name":"tool_search_tool_regex","input":{"query":"route"}}""";

    private const string SearchResult =
        """{"type":"tool_search_tool_result","tool_use_id":"srvtoolu_1","content":{"type":"tool_search_tool_search_result","tool_references":[{"type":"tool_reference","tool_name":"plot_route"}]}}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> SentAsync(IReadOnlyList<ConversationMessage> history)
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(
            endpoint,
            new LlmRequest
            {
                Model = "claude-opus-5",
                Effort = ThinkingEffort.Medium,
                Sampling = LlmSampling.Conversation,
                Prompt = new PromptAssembly { History = history },
            },
            Token);

        return JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement.Clone();
    }

    private static List<ConversationMessage> Round(string providerId) =>
    [
        new ConversationMessage(ConversationRole.User, "plot a route to Colonia"),
        new ConversationMessage(
            ConversationRole.Assistant,
            [
                new ConversationContent.Text("Looking for the tool."),
                new ConversationContent.Opaque(providerId, SearchCall),
                new ConversationContent.Opaque(providerId, SearchResult),
                new ConversationContent.ToolUse("toolu_1", "plot_route", """{"to":"Colonia"}"""),
            ]),
        new ConversationMessage(
            ConversationRole.User,
            [new ConversationContent.ToolResult("toolu_1", "Route plotted.", IsError: false)]),
    ];

    private static List<ConversationMessage> WithoutBlocks() =>
    [
        new ConversationMessage(ConversationRole.User, "plot a route to Colonia"),
        new ConversationMessage(
            ConversationRole.Assistant,
            [
                new ConversationContent.Text("Looking for the tool."),
                new ConversationContent.ToolUse("toolu_1", "plot_route", """{"to":"Colonia"}"""),
            ]),
        new ConversationMessage(
            ConversationRole.User,
            [new ConversationContent.ToolResult("toolu_1", "Route plotted.", IsError: false)]),
    ];

    private static string[] Types(JsonElement content) =>
        [.. content.EnumerateArray().Select(block => block.GetProperty("type").GetString()!)];

    [Fact]
    public async Task AnAnthropicBlockIsSentVerbatimInItsPlace()
    {
        var sent = await SentAsync(Round("anthropic"));

        var content = sent.GetProperty("messages")[1].GetProperty("content");

        Assert.Equal(["text", "server_tool_use", "tool_search_tool_result", "tool_use"], Types(content));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(SearchCall).RootElement, content[1]));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(SearchResult).RootElement, content[2]));
    }

    [Fact]
    public async Task AnotherProvidersBlockIsNotSentToAnthropic()
    {
        var sent = await SentAsync(Round("openai"));

        var content = sent.GetProperty("messages")[1].GetProperty("content");

        Assert.Equal(["text", "tool_use"], Types(content));
    }

    [Fact]
    public void ChatCompletionsSendsTheSameRequestWithOrWithoutABlock()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint: null);

        Assert.Equal(Body(provider.BuildBody, WithoutBlocks()), Body(provider.BuildBody, WithStandaloneBlock()));
    }

    [Fact]
    public void ResponsesSendsTheSameRequestWithOrWithoutABlock()
    {
        using var provider = new ResponsesLlmProvider("sk-test", endpoint: null);

        Assert.Equal(Body(provider.BuildBody, WithoutBlocks()), Body(provider.BuildBody, WithStandaloneBlock()));
    }

    /// <summary>The blocks inline, plus a message holding nothing but a block.</summary>
    private static List<ConversationMessage> WithStandaloneBlock()
    {
        var history = Round("anthropic");
        history.Insert(1, new ConversationMessage(
            ConversationRole.Assistant,
            [new ConversationContent.Opaque("anthropic", SearchCall)]));
        return history;
    }

    private static string Body(Func<LlmRequest, ReadOnlyMemory<byte>> build, IReadOnlyList<ConversationMessage> history) =>
        System.Text.Encoding.UTF8.GetString(build(OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly { History = history },
        }).Span);
}
