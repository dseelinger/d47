using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class OpenAiResponsesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public OpenAiResponsesTests() => EndpointDemotions.Clear();

    [Fact]
    public async Task AOneWordTurnArrivesAsTextThenCompleted()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.TextDelta("Acknowledged"),
            OpenAiRecordings.Responses.Completed(input: 120, cached: 0, written: 0, output: 3));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Acknowledged", Assert.IsType<LlmStreamEvent.TextDelta>(events[0]).Text);

        var done = Assert.IsType<LlmStreamEvent.Completed>(events[^1]);
        Assert.Equal(LlmStopReason.Completed, done.StopReason);
        Assert.Equal(120, done.Usage.InputTokens);
    }

    [Fact]
    public async Task CachedAndWrittenTokensBothComeOutOfTheInputCount()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.TextDelta("Fine."),
            OpenAiRecordings.Responses.Completed(input: 2_600, cached: 2_000, written: 400, output: 150));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.Equal(200, done.Usage.InputTokens);
        Assert.Equal(400, done.Usage.CacheCreationInputTokens);
        Assert.Equal(2_000, done.Usage.CacheReadInputTokens);
        Assert.Equal(2_600, done.Usage.TotalInputTokens);
    }

    /// <summary>
    /// The name arrives on <c>response.output_item.added</c> against an item id; the arguments stream
    /// against that same item id; and the answer has to quote the <c>call_id</c>, which is a different
    /// string.
    /// </summary>
    [Fact]
    public async Task AToolCallIsRejoinedFromTwoDifferentIdentifiers()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.ToolCallAdded("fc_item_1", "call_abc", "set_route"),
            OpenAiRecordings.Responses.ToolCallArguments("fc_item_1", "{\"system\":\"Sola"),
            OpenAiRecordings.Responses.ToolCallArguments("fc_item_1", "ria\"}"),
            OpenAiRecordings.Responses.ToolCallDone("fc_item_1", arguments: null),
            OpenAiRecordings.Responses.Completed(input: 900, cached: 0, written: 0, output: 30));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);
        var call = Assert.Single(events.OfType<LlmStreamEvent.ToolUse>());

        Assert.Equal("call_abc", call.Id);
        Assert.Equal("set_route", call.Name);

        using var parsed = JsonDocument.Parse(call.InputJson);
        Assert.Equal("Solaria", parsed.RootElement.GetProperty("system").GetString());

        Assert.Equal(LlmStopReason.ToolUse, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).StopReason);
    }

    /// <summary>
    /// The done event carries the whole argument string as well, and it is the authoritative copy.
    /// </summary>
    [Fact]
    public async Task TheCompleteArgumentStringWinsOverTheFragments()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.ToolCallAdded("fc_1", "call_1", "fuel"),
            OpenAiRecordings.Responses.ToolCallArguments("fc_1", "{\"tank\":"),
            OpenAiRecordings.Responses.ToolCallDone("fc_1", "{\"tank\":\"main\"}"),
            OpenAiRecordings.Responses.Completed(input: 10, cached: 0, written: 0, output: 5));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var call = Assert.Single(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))
            .OfType<LlmStreamEvent.ToolUse>());

        Assert.Equal("{\"tank\":\"main\"}", call.InputJson);
    }

    [Fact]
    public async Task ACallLeftOpenIsStillEmitted()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.ToolCallAdded("fc_1", "call_1", "fuel"),
            OpenAiRecordings.Responses.ToolCallArguments("fc_1", "{}"),
            OpenAiRecordings.Responses.Completed(input: 10, cached: 0, written: 0, output: 5));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("{}", Assert.Single(events.OfType<LlmStreamEvent.ToolUse>()).InputJson);
        Assert.Equal(LlmStopReason.ToolUse, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).StopReason);
    }

    [Fact]
    public async Task ReasoningSummariesAreKeptApartFromTheAnswer()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.ReasoningDelta("Checking the tank."),
            OpenAiRecordings.Responses.TextDelta("Half full."),
            OpenAiRecordings.Responses.Completed(input: 10, cached: 0, written: 0, output: 5));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Checking the tank.", Assert.Single(events.OfType<LlmStreamEvent.ThinkingDelta>()).Text);
        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
    }

    /// <summary>A server-side search is billed separately and costs more than an entire cheap turn.</summary>
    [Fact]
    public async Task ServerSideSearchesAreCounted()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.WebSearchCall("ws_1"),
            OpenAiRecordings.Responses.WebSearchCall("ws_2"),
            OpenAiRecordings.Responses.TextDelta("Two sources agree."),
            OpenAiRecordings.Responses.Completed(input: 5_000, cached: 0, written: 0, output: 200));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask() with { WebSearch = true }, Token))[^1]);

        Assert.Equal(2, done.Usage.WebSearchRequests);
    }

    [Theory]
    [InlineData("max_output_tokens", LlmStopReason.MaxTokens)]
    [InlineData("content_filter", LlmStopReason.Refusal)]
    [InlineData("something_new", LlmStopReason.Paused)]
    public async Task AnIncompleteTurnSaysWhyRatherThanReadingAsFinished(string reason, LlmStopReason expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.TextDelta("The route is"),
            OpenAiRecordings.Responses.Incomplete(reason));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.Equal(expected, done.StopReason);
    }

    [Fact]
    public async Task AFailureInsideTheStreamEndsTheTurnAsAFailure()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.TextDelta("Working"),
            OpenAiRecordings.Responses.Failed("the upstream model is unavailable"));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Contains(
            "unavailable",
            Assert.IsType<LlmStreamEvent.Failed>(events[^1]).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableEndpointIsATransientFailureRatherThanAThrow()
    {
        using var provider = new ResponsesLlmProvider("sk-test", "http://127.0.0.1:1/v1");

        var failure = Assert.IsType<LlmStreamEvent.Failed>(
            Assert.Single(await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token)));

        Assert.True(failure.Transient);
    }

    [Fact]
    public void TheToolSchemaReachesTheWireAsTheBytesItWasWrittenAs()
    {
        const string schema =
            "{\"type\":\"object\",\"properties\":{\"zulu\":{\"type\":\"string\"},"
            + "\"alpha\":{\"type\":\"integer\"}},\"required\":[\"zulu\"]}";

        using var provider = new ResponsesLlmProvider("sk-test", endpoint: null);

        var body = Body(provider, OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly
            {
                Tools = [new ToolAdvertisement("set_route", "Plot a course.", schema)],
                History = [new ConversationMessage(ConversationRole.User, "Route me somewhere.")],
            },
        });

        Assert.Contains("\"parameters\":" + schema, body, StringComparison.Ordinal);

        // Flat, not nested.
        Assert.Contains("{\"type\":\"function\",\"name\":\"set_route\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConversationIsNeverHandedToTheProviderToKeep()
    {
        using var provider = new ResponsesLlmProvider("sk-test", endpoint: null);

        var body = Body(provider, OpenAiRecordings.Ask());

        Assert.Contains("\"store\":false", body, StringComparison.Ordinal);
        Assert.DoesNotContain("previous_response_id", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Live game state goes below everything cached, under the role that carries operator authority —
    /// which is this protocol's <c>developer</c> rather than <c>system</c>, the same property under a
    /// different name.
    /// </summary>
    [Fact]
    public void LiveGameStateIsTheLastThingAndCarriesOperatorAuthority()
    {
        using var provider = new ResponsesLlmProvider("sk-test", endpoint: null);

        var body = Body(provider, OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly
            {
                History = [new ConversationMessage(ConversationRole.User, "Where am I?")],
                LiveGameState = "Docked at Jameson Memorial.",
            },
        });

        var question = body.IndexOf("Where am I?", StringComparison.Ordinal);
        var state = body.IndexOf("Docked at Jameson Memorial.", StringComparison.Ordinal);

        Assert.True(state > question, "game state belongs after the history it describes the moment of");
        Assert.Contains("\"role\":\"developer\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("system-reminder", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIsOfferedAtTheOwnEndpointAndNotAtAGateway()
    {
        using var own = new ResponsesLlmProvider("sk-test", endpoint: null);
        using var gateway = new ResponsesLlmProvider("sk-test", "https://openrouter.ai/api/v1");

        Assert.True(own.CapabilitiesFor("gpt-5.6-terra").SupportsWebSearch);
        Assert.False(gateway.CapabilitiesFor("gpt-5.6-terra").SupportsWebSearch);

        Assert.Contains(
            "{\"type\":\"web_search\"}",
            Body(own, OpenAiRecordings.Ask() with { WebSearch = true }),
            StringComparison.Ordinal);
    }

    private static string Body(ResponsesLlmProvider provider, LlmRequest request) =>
        System.Text.Encoding.UTF8.GetString(provider.BuildBody(request).Span);
}
