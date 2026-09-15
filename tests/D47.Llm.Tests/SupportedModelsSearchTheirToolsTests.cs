using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>Deferred tools go to Anthropic behind the search tool, and a search comes back as blocks replayed in place.</summary>
[Collection(nameof(EndpointDemotionCollection))]
public class SupportedModelsSearchTheirToolsTests
{
    private const string SearchCall =
        """{"type":"server_tool_use","id":"srvtoolu_1","name":"tool_search_tool_bm25","input":{"query":"route"}}""";

    private const string SearchResult =
        """{"type":"tool_search_tool_result","tool_use_id":"srvtoolu_1","content":{"type":"tool_search_tool_search_result","tool_references":[{"type":"tool_reference","tool_name":"plot_route"}]}}""";

    private const string SearchRefusal =
        """{"type":"error","error":{"type":"invalid_request_error","message":"tool_search_tool_bm25_20251119 is not supported on this model"}}""";

    private static readonly ToolAdvertisement Help =
        new("get_capabilities", "Lists what d47 can do.", """{"type":"object"}""");

    private static readonly ToolAdvertisement Route =
        new("plot_route", "Plots a route.", """{"type":"object","properties":{"to":{"type":"string"}}}""", Deferred: true);

    private static readonly ToolAdvertisement Srv =
        new("control_srv", "Operates the SRV.", """{"type":"object"}""", Deferred: true);

    public SupportedModelsSearchTheirToolsTests() => EndpointDemotions.Clear();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AnthropicLlmProvider Own() => new("test-key");

    private static LlmRequest Request(
        string model,
        IReadOnlyList<ToolAdvertisement> tools,
        IReadOnlyList<ConversationMessage>? history = null) => new()
    {
        Model = model,
        Effort = ThinkingEffort.Medium,
        Sampling = LlmSampling.Conversation,
        Prompt = new PromptAssembly
        {
            Tools = tools,
            History = history ?? [new ConversationMessage(ConversationRole.User, "plot a route to Colonia")],
        },
    };

    private static async Task<JsonElement> SentAsync(LlmRequest request)
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(endpoint, request, Token);

        return JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement.Clone();
    }

    private static List<ConversationMessage> SearchedRound(string searchCall, string searchResult) =>
    [
        new ConversationMessage(ConversationRole.User, "plot a route to Colonia"),
        new ConversationMessage(
            ConversationRole.Assistant,
            [
                new ConversationContent.Text("Looking."),
                new ConversationContent.Opaque("anthropic", searchCall),
                new ConversationContent.Opaque("anthropic", searchResult),
                new ConversationContent.ToolUse("toolu_1", "plot_route", """{"to":"Colonia"}"""),
            ]),
        new ConversationMessage(
            ConversationRole.User,
            [new ConversationContent.ToolResult("toolu_1", "Route plotted.", IsError: false)]),
    ];

    private static string[] Types(JsonElement array) =>
        [.. array.EnumerateArray().Select(block => block.GetProperty("type").GetString()!)];

    [Theory]
    [InlineData("claude-opus-5", true)]
    [InlineData("claude-opus-4-8", true)]
    [InlineData("claude-opus-4-7", true)]
    [InlineData("claude-fable-5", true)]
    [InlineData("claude-mythos-5", true)]
    [InlineData("claude-haiku-4-5", true)]
    [InlineData("claude-sonnet-5", false)]
    [InlineData("claude-something-7", false)]
    public void ToolSearchIsDeclaredPerModel(string model, bool expected)
    {
        Assert.Equal(expected, Own().CapabilitiesFor(model).SupportsToolSearch);
    }

    [Fact]
    public void NeitherACustomEndpointNorAnOpenAiProviderSearchesItsTools()
    {
        Assert.False(new AnthropicLlmProvider("test-key", "http://127.0.0.1:9").CapabilitiesFor("claude-opus-5").SupportsToolSearch);

        using var chat = new ChatCompletionsLlmProvider(apiKey: null, endpoint: null);
        using var responses = new ResponsesLlmProvider("sk-test", endpoint: null);

        Assert.False(chat.CapabilitiesFor(chat.DefaultModel).SupportsToolSearch);
        Assert.False(responses.CapabilitiesFor(responses.DefaultModel).SupportsToolSearch);
    }

    [Fact]
    public async Task DeferredToolsFollowTheSearchToolAndOnlyTheLoadedOneIsNotDeferred()
    {
        var sent = await SentAsync(Request("claude-opus-5", [Help, Route, Srv]));

        var tools = sent.GetProperty("tools");

        Assert.Equal(4, tools.GetArrayLength());
        Assert.StartsWith("tool_search_tool_bm25", tools[0].GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.False(tools[0].TryGetProperty("defer_loading", out _));

        Assert.Equal("get_capabilities", tools[1].GetProperty("name").GetString());
        Assert.False(tools[1].TryGetProperty("defer_loading", out _));

        Assert.Equal("plot_route", tools[2].GetProperty("name").GetString());
        Assert.True(tools[2].GetProperty("defer_loading").GetBoolean());
        Assert.Equal("control_srv", tools[3].GetProperty("name").GetString());
        Assert.True(tools[3].GetProperty("defer_loading").GetBoolean());

        Assert.All(tools.EnumerateArray(), tool => Assert.False(tool.TryGetProperty("cache_control", out _)));
    }

    [Fact]
    public async Task AModesListCarriesNoSearchTool()
    {
        var sent = await SentAsync(Request("claude-sonnet-5", [Help, Route with { Deferred = false }]));

        var tools = sent.GetProperty("tools");

        Assert.Equal(["get_capabilities", "plot_route"], tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()));
        Assert.All(tools.EnumerateArray(), tool => Assert.False(tool.TryGetProperty("defer_loading", out _)));
    }

    [Fact]
    public async Task ASearchGoesBackUnchangedAndAheadOfTheCallThatFollowedIt()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.TextBlockStart(0),
            Recordings.TextDelta("Looking.", 0),
            Recordings.BlockStop(0),
            RecordedEndpoint.Event(
                "content_block_start",
                """{"type":"content_block_start","index":1,"content_block":{"type":"server_tool_use","id":"srvtoolu_1","name":"tool_search_tool_bm25","input":{}}}"""),
            Recordings.InputJsonDelta("""{"query":""", 1),
            Recordings.InputJsonDelta("\"route\"}", 1),
            Recordings.BlockStop(1),
            RecordedEndpoint.Event(
                "content_block_start",
                $$"""{"type":"content_block_start","index":2,"content_block":{{SearchResult}}}"""),
            Recordings.BlockStop(2),
            Recordings.ToolUseBlockStart("toolu_1", "plot_route", 3),
            Recordings.InputJsonDelta("""{"to":"Colonia"}""", 3),
            Recordings.BlockStop(3),
            Recordings.MessageDelta("tool_use"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, Request("claude-opus-5", [Help, Route]), Token);

        Assert.Collection(
            events,
            e => Assert.IsType<LlmStreamEvent.TextDelta>(e),
            e => Assert.IsType<LlmStreamEvent.Opaque>(e),
            e => Assert.IsType<LlmStreamEvent.Opaque>(e),
            e =>
            {
                var searched = Assert.IsType<LlmStreamEvent.ToolSearched>(e);
                Assert.Equal("route", searched.Query);
                Assert.Equal(["plot_route"], searched.Found);
            },
            e => Assert.IsType<LlmStreamEvent.ToolUse>(e),
            e => Assert.IsType<LlmStreamEvent.Completed>(e));

        var blocks = events.OfType<LlmStreamEvent.Opaque>().ToList();

        var sent = await SentAsync(Request("claude-opus-5", [Help, Route], SearchedRound(blocks[0].Json, blocks[1].Json)));

        var content = sent.GetProperty("messages")[1].GetProperty("content");

        Assert.Equal(["text", "server_tool_use", "tool_search_tool_result", "tool_use"], Types(content));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(SearchCall).RootElement, content[1]));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(SearchResult).RootElement, content[2]));
    }

    [Fact]
    public async Task AfterASwitchToAModelWithoutSearchNoBlockIsSent()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(
            endpoint,
            Request("claude-sonnet-5", [Help, Route with { Deferred = false }], SearchedRound(SearchCall, SearchResult)),
            Token);

        var body = Assert.Single(endpoint.Requests);
        var content = JsonDocument.Parse(body).RootElement.GetProperty("messages")[1].GetProperty("content");

        Assert.Equal(["text", "tool_use"], Types(content));
        Assert.DoesNotContain("tool_search", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedSearchIsDemotedForThatModelAndTheFailureSaysWhatHappensNext()
    {
        using var endpoint = RecordedEndpoint.Failing(400, SearchRefusal);

        var events = await Recordings.DrainAsync(endpoint, Request("claude-opus-5", [Help, Route]), Token);

        var failed = Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
        Assert.Equal(AnthropicLlmProvider.ToolSearchRefused, failed.Message);
        Assert.False(failed.Transient);
        Assert.Single(endpoint.Requests);

        Assert.False(EndpointDemotions.Allows(endpoint.BaseUrl, Demotable.ToolSearch, "claude-opus-5"));
        Assert.True(EndpointDemotions.Allows(endpoint.BaseUrl, Demotable.ToolSearch, "claude-fable-5"));
    }

    [Fact]
    public void ADemotionOnAnthropicsOwnEndpointWithdrawsSearchForThatModelOnly()
    {
        EndpointDemotions.Demote("https://api.anthropic.com", Demotable.ToolSearch, "claude-opus-5");

        Assert.False(Own().CapabilitiesFor("claude-opus-5").SupportsToolSearch);
        Assert.True(Own().CapabilitiesFor("claude-fable-5").SupportsToolSearch);
    }

    [Theory]
    [InlineData("tools.1.defer_loading: Extra inputs are not permitted", true)]
    [InlineData("tool_search_tool_bm25_20251119 is not supported on this model", true)]
    [InlineData("output_config.effort: Extra inputs are not permitted", false)]
    public void ARefusalNamingToolSearchIsToldApartFromOneNamingTheEffort(string message, bool toolSearch)
    {
        Assert.Equal(
            toolSearch ? Demotable.ToolSearch : Demotable.AdaptiveThinking,
            AnthropicLlmProvider.WhatWasRejected(message));
    }
}
