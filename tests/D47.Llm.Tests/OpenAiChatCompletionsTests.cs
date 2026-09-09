using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class OpenAiChatCompletionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public OpenAiChatCompletionsTests() => EndpointDemotions.Clear();

    [Fact]
    public async Task AOneWordTurnArrivesAsTextThenCompleted()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("Acknowledged"),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Chat.Usage(prompt: 120, cached: 0, completion: 3),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Acknowledged", Assert.IsType<LlmStreamEvent.TextDelta>(events[0]).Text);

        var done = Assert.IsType<LlmStreamEvent.Completed>(events[^1]);
        Assert.Equal(LlmStopReason.Completed, done.StopReason);
        Assert.Equal(120, done.Usage.InputTokens);
        Assert.Equal(3, done.Usage.OutputTokens);
    }

    /// <summary>
    /// The convention conversion, seen end to end rather than in the arithmetic alone: what the
    /// endpoint calls 2,600 prompt tokens with 2,000 cached must reach the seam as 600 uncached and
    /// 2,000 read, or every cached turn is billed for 4,600 input tokens it never used.
    /// </summary>
    [Fact]
    public async Task CachedPromptTokensAreNotCountedTwice()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("Fine."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Chat.Usage(prompt: 2_600, cached: 2_000, completion: 40),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.Equal(600, done.Usage.InputTokens);
        Assert.Equal(2_000, done.Usage.CacheReadInputTokens);
        Assert.Equal(0, done.Usage.CacheCreationInputTokens);
        Assert.Equal(2_600, done.Usage.TotalInputTokens);
    }

    [Fact]
    public async Task AServerThatSendsNoUsageLeavesTheTurnUnpricedRatherThanFree()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("Docked."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.False(done.Usage.Reported);
    }

    /// <summary>
    /// Arguments arrive as fragments that are not parseable until the last one lands, and the later
    /// fragments name only the index — no id, which is how several servers stream them.
    /// </summary>
    [Fact]
    public async Task AToolCallIsAssembledWholeAcrossFragments()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.ToolCallStart(0, "call_abc", "set_route"),
            OpenAiRecordings.Chat.ToolCallArguments(0, "{\"system\":\"Sola"),
            OpenAiRecordings.Chat.ToolCallArguments(0, "ria\",\"jumps\":3}"),
            OpenAiRecordings.Chat.Finish("tool_calls"),
            OpenAiRecordings.Chat.Usage(prompt: 900, cached: 0, completion: 30),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);
        var call = Assert.Single(events.OfType<LlmStreamEvent.ToolUse>());

        Assert.Equal("call_abc", call.Id);
        Assert.Equal("set_route", call.Name);

        using var parsed = JsonDocument.Parse(call.InputJson);
        Assert.Equal("Solaria", parsed.RootElement.GetProperty("system").GetString());
        Assert.Equal(3, parsed.RootElement.GetProperty("jumps").GetInt32());

        Assert.Equal(LlmStopReason.ToolUse, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).StopReason);
    }

    /// <summary>A tool with no parameters produces no argument fragments at all.</summary>
    [Fact]
    public async Task AToolWithNoArgumentsArrivesAsAnEmptyObject()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.ToolCallStart(0, "call_1", "current_system"),
            OpenAiRecordings.Chat.Finish("tool_calls"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("{}", Assert.Single(events.OfType<LlmStreamEvent.ToolUse>()).InputJson);
    }

    /// <summary>Two tools at once, interleaved: the index keeps them apart where the id alone would merge them.</summary>
    [Fact]
    public async Task TwoInterleavedToolCallsStayApart()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.ToolCallStart(0, "call_a", "fuel"),
            OpenAiRecordings.Chat.ToolCallStart(1, "call_b", "cargo"),
            OpenAiRecordings.Chat.ToolCallArguments(0, """{"tank":"""),
            OpenAiRecordings.Chat.ToolCallArguments(1, """{"hold":"""),
            OpenAiRecordings.Chat.ToolCallArguments(0, "\"main\"}"),
            OpenAiRecordings.Chat.ToolCallArguments(1, "\"aft\"}"),
            OpenAiRecordings.Chat.Finish("tool_calls"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var calls = (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))
            .OfType<LlmStreamEvent.ToolUse>()
            .ToArray();

        Assert.Equal(2, calls.Length);
        Assert.Equal(("call_a", "fuel", """{"tank":"main"}"""), (calls[0].Id, calls[0].Name, calls[0].InputJson));
        Assert.Equal(("call_b", "cargo", """{"hold":"aft"}"""), (calls[1].Id, calls[1].Name, calls[1].InputJson));
    }

    [Fact]
    public async Task ReasoningContentIsKeptApartFromTheAnswer()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.ReasoningDelta("Checking the tank."),
            OpenAiRecordings.Chat.TextDelta("Half full."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Checking the tank.", Assert.Single(events.OfType<LlmStreamEvent.ThinkingDelta>()).Text);
        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
    }

    [Theory]
    [InlineData("length", LlmStopReason.MaxTokens)]
    [InlineData("content_filter", LlmStopReason.Refusal)]
    [InlineData("tool_calls", LlmStopReason.ToolUse)]
    [InlineData("stop", LlmStopReason.Completed)]
    public async Task StopReasonsAreTranslatedRatherThanFallingThrough(string finish, LlmStopReason expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("."),
            OpenAiRecordings.Chat.Finish(finish),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.Equal(expected, done.StopReason);
    }

    [Fact]
    public async Task AToolCallWithNoFinishReasonStillEndsTheTurnForTools()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.ToolCallStart(0, "call_1", "fuel"),
            OpenAiRecordings.Chat.ToolCallArguments(0, "{}"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Single(events.OfType<LlmStreamEvent.ToolUse>());
        Assert.Equal(LlmStopReason.ToolUse, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).StopReason);
    }

    [Fact]
    public async Task AnUnreachableEndpointIsATransientFailureRatherThanAThrow()
    {
        // A port nothing is listening on.
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:1/v1");

        var failure = Assert.IsType<LlmStreamEvent.Failed>(
            Assert.Single(await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token)));

        Assert.True(failure.Transient);
    }

    [Fact]
    public async Task ARejectedKeyIsPermanentAndAnOverloadIsNot()
    {
        using var refused = RecordedEndpoint.Failing(401, OpenAiRecordings.Refusal("Invalid API key.", null));
        using var overloaded = RecordedEndpoint.Failing(503, OpenAiRecordings.Refusal("Overloaded.", null));

        using var toRefused = new ChatCompletionsLlmProvider("sk-wrong", refused.BaseUrl);
        using var toOverloaded = new ChatCompletionsLlmProvider("sk-fine", overloaded.BaseUrl);

        var rejected = Assert.IsType<LlmStreamEvent.Failed>(
            Assert.Single(await OpenAiRecordings.DrainAsync(toRefused, OpenAiRecordings.Ask(), Token)));

        var busy = Assert.IsType<LlmStreamEvent.Failed>(
            Assert.Single(await OpenAiRecordings.DrainAsync(toOverloaded, OpenAiRecordings.Ask(), Token)));

        Assert.False(rejected.Transient);
        Assert.True(busy.Transient);
    }

    /// <summary>
    /// An error delivered inside the stream, after the endpoint has already sent 200 and started the
    /// body.
    /// </summary>
    [Fact]
    public async Task AnErrorInsideTheStreamEndsTheTurnAsAFailure()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("Loading"),
            OpenAiRecordings.Data("{\"error\":{\"message\":\"the model was unloaded\"}}"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Contains("unloaded", Assert.IsType<LlmStreamEvent.Failed>(events[^1]).Message, StringComparison.Ordinal);
    }

    /// <summary>A keep-alive comment, a blank frame, a line that is not JSON.</summary>
    [Fact]
    public async Task AFrameThatIsNotJsonIsSkippedRatherThanEndingTheTurn()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            ": keep-alive\n\n",
            OpenAiRecordings.Data("not json at all"),
            OpenAiRecordings.Chat.TextDelta("Still here."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Still here.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.IsType<LlmStreamEvent.Completed>(events[^1]);
    }

    [Fact]
    public void TheToolSchemaReachesTheWireAsTheBytesItWasWrittenAs()
    {
        const string schema =
            "{\"type\":\"object\",\"properties\":{\"zulu\":{\"type\":\"string\"},"
            + "\"alpha\":{\"type\":\"integer\"}},\"required\":[\"zulu\"]}";

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        var body = System.Text.Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly
            {
                Tools = [new ToolAdvertisement("set_route", "Plot a course.", schema)],
                History = [new ConversationMessage(ConversationRole.User, "Route me somewhere.")],
            },
        }).Span);

        // Verbatim, including the unsorted key order, which proves nothing re-serialised it.
        Assert.Contains("\"parameters\":" + schema, body, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageIsAskedForOnEveryRequest()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        var body = System.Text.Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask()).Span);

        Assert.Contains("\"stream_options\":{\"include_usage\":true}", body, StringComparison.Ordinal);
        Assert.Contains("\"stream\":true", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tool results are their own messages on this protocol rather than blocks in the next user turn,
    /// and they must sit immediately after the assistant message that asked for them.
    /// </summary>
    [Fact]
    public void ToolResultsBecomeTheirOwnMessagesInTheRightOrder()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        var body = System.Text.Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask() with
        {
            Prompt = new PromptAssembly
            {
                History =
                [
                    new ConversationMessage(ConversationRole.User, "Where am I?"),
                    new ConversationMessage(
                        ConversationRole.Assistant,
                        [new ConversationContent.ToolUse("call_1", "current_system", "{}")]),
                    new ConversationMessage(
                        ConversationRole.User,
                        [new ConversationContent.ToolResult("call_1", "Shinrarta Dezhra", IsError: false)]),
                ],
                LiveGameState = "Docked at Jameson Memorial.",
            },
        }).Span);

        var assistant = body.IndexOf(
            "{\"role\":\"assistant\",\"content\":null,\"tool_calls\":",
            StringComparison.Ordinal);

        var result = body.IndexOf("{\"role\":\"tool\",\"tool_call_id\":\"call_1\"", StringComparison.Ordinal);

        Assert.True(assistant > 0, "the assistant turn carrying the call should be on the wire");
        Assert.True(result > assistant, "the result must follow the call that asked for it");

        // This provider does not claim operator system messages, so game state is a plain user turn.
        var reminder = body.IndexOf("system-reminder", StringComparison.Ordinal);
        Assert.True(reminder > result, "live game state belongs below everything cached");
    }

    [Fact]
    public async Task TextArrivesBeforeTheTurnHasFinished()
    {
        var release = new TaskCompletionSource();

        using var endpoint = RecordedEndpoint.StreamingUntilReleased(
            release.Task,
            OpenAiRecordings.Chat.TextDelta("Fuel "),
            OpenAiRecordings.Chat.TextDelta("is "),
            OpenAiRecordings.Chat.TextDelta("low."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Chat.Usage(prompt: 100, cached: 0, completion: 3),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(Token);
        budget.CancelAfter(TimeSpan.FromSeconds(15));

        var seen = new List<LlmStreamEvent>();

        await foreach (var step in provider.StreamAsync(OpenAiRecordings.Ask(), budget.Token))
        {
            seen.Add(step);

            // The rest of the body does not exist yet: a decoder that waits for it never gets here.
            if (seen.Count == 1)
            {
                Assert.Equal("Fuel ", Assert.IsType<LlmStreamEvent.TextDelta>(step).Text);
                release.SetResult();
            }
        }

        Assert.Equal("Fuel is low.", string.Concat(seen.OfType<LlmStreamEvent.TextDelta>().Select(d => d.Text)));
        Assert.IsType<LlmStreamEvent.Completed>(seen[^1]);
    }

    [Fact]
    public void ThereIsNoDefaultModelBecauseThereIsNothingToDefaultTo()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        Assert.Equal(string.Empty, provider.DefaultModel);
        Assert.True(provider.RunsOnThisMachine);
        Assert.False(provider.CapabilitiesFor("anything").SupportsWebSearch);
    }
}

/// <summary>Demotion state is keyed statically by address, so these tests must not run beside each other.</summary>
[CollectionDefinition(nameof(EndpointDemotionCollection), DisableParallelization = true)]
public class EndpointDemotionCollection;
