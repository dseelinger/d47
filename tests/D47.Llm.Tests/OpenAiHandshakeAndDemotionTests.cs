using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class OpenAiHandshakeAndDemotionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public OpenAiHandshakeAndDemotionTests() => EndpointDemotions.Clear();

    [Fact]
    public async Task TheModelListBecomesTheEndpointsOwn()
    {
        using var endpoint = RecordedEndpoint.Json(
            "{\"object\":\"list\",\"data\":["
            + "{\"id\":\"qwen3:30b\",\"object\":\"model\"},"
            + "{\"id\":\"llama3.3:70b\",\"object\":\"model\"}]}");

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var models = await provider.ListModelsAsync(Token);

        Assert.Equal(EndpointReach.Answered, models.Reach);

        // Sorted, so the picker does not reshuffle between visits to the panel.
        Assert.Equal(["llama3.3:70b", "qwen3:30b"], models.Ids);
    }

    [Fact]
    public void AnEndpointThatListsNothingHasStillAnswered()
    {
        var models = EndpointHandshake.Read("{\"object\":\"list\",\"data\":[]}");

        Assert.Equal(EndpointReach.Answered, models.Reach);
        Assert.Empty(models.Ids);
    }

    [Fact]
    public void SomethingThatIsNotAModelListIsARefusalRatherThanAnEmptyPicker()
    {
        var models = EndpointHandshake.Read("<html><body>404 not found</body></html>");

        Assert.Equal(EndpointReach.Refused, models.Reach);
        Assert.NotNull(models.Detail);
    }

    [Fact]
    public async Task NotStartedYetIsNotTheSameAsWrong()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:1/v1");

        Assert.Equal(EndpointReach.Unreachable, (await provider.ListModelsAsync(Token)).Reach);

        using var refusing = RecordedEndpoint.Failing(401, OpenAiRecordings.Refusal("no key", null));
        using var toRefusing = new ChatCompletionsLlmProvider(apiKey: null, refusing.BaseUrl);

        Assert.Equal(EndpointReach.Refused, (await toRefusing.ListModelsAsync(Token)).Reach);
    }

    [Fact]
    public async Task ARefusedFieldIsDroppedAndTheTurnRetriedOnce()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("Unrecognized request argument supplied: reasoning_effort", "reasoning_effort"),
            OpenAiRecordings.Chat.TextDelta("Half full."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Chat.Usage(prompt: 100, cached: 0, completion: 3),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        Assert.True(provider.CapabilitiesFor("any").SupportsThinkingEffort);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());

        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("reasoning_effort", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning_effort", endpoint.Requests[1], StringComparison.Ordinal);

        Assert.False(provider.CapabilitiesFor("any").SupportsThinkingEffort);
    }

    [Fact]
    public async Task TheSameRefusalIsNotRetriedForever()
    {
        using var endpoint = RecordedEndpoint.Failing(
            400,
            OpenAiRecordings.Refusal("Unrecognized request argument supplied: reasoning_effort", "reasoning_effort"));

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var first = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);
        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(first));
        Assert.Equal(2, endpoint.Requests.Count);

        // The field is already gone from the request, so there is one attempt and one failure.
        var second = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);
        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(second));
        Assert.Equal(3, endpoint.Requests.Count);
    }

    [Fact]
    public async Task ADemotionAtOneAddressDoesNotFollowToAnother()
    {
        using var refusing = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("unsupported parameter", "reasoning_effort"),
            OpenAiRecordings.Chat.TextDelta("."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var refusingProvider = new ChatCompletionsLlmProvider(apiKey: null, refusing.BaseUrl);
        await OpenAiRecordings.DrainAsync(refusingProvider, OpenAiRecordings.Ask(), Token);

        Assert.False(refusingProvider.CapabilitiesFor("any").SupportsThinkingEffort);

        using var other = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");
        Assert.True(other.CapabilitiesFor("any").SupportsThinkingEffort);
    }

    [Fact]
    public async Task ARefusalThatNamesNothingDemotesNothing()
    {
        using var endpoint = RecordedEndpoint.Failing(
            400,
            OpenAiRecordings.Refusal("The model produced invalid output.", null));

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        Assert.IsType<LlmStreamEvent.Failed>(
            Assert.Single(await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token)));

        // One attempt, not two, and everything still advertised.
        Assert.Single(endpoint.Requests);
        Assert.True(provider.CapabilitiesFor("any").SupportsThinkingEffort);
        Assert.True(provider.CapabilitiesFor("any").SupportsToolCalls);
    }

    /// <summary>An ordinary tool error quotes the tool's own name; reading that as a refusal would turn tools off for the session.</summary>
    [Theory]
    [InlineData("Invalid schema for function 'set_route': missing 'type'.", null)]
    [InlineData("tool_choice is not supported by this model", Demotable.Tools)]
    [InlineData("Unrecognized request argument supplied: reasoning_effort", Demotable.ReasoningEffort)]
    [InlineData("stream_options is not supported", Demotable.StreamUsage)]
    [InlineData("Unsupported parameter: max_completion_tokens", Demotable.ModernTokenLimit)]
    [InlineData("Rate limit reached for requests", null)]
    internal void OnlyANamedFieldCountsAsARefusal(string message, Demotable? expected) =>
        Assert.Equal(expected, ChatCompletionsLlmProvider.WhatWasRejected(message));

    [Fact]
    public async Task AnEndpointThatRefusesUsageLeavesTheTurnUnpriced()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("stream_options is not supported", "stream_options"),
            OpenAiRecordings.Chat.TextDelta("Understood."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var done = Assert.IsType<LlmStreamEvent.Completed>(
            (await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token))[^1]);

        Assert.False(done.Usage.Reported);
        Assert.DoesNotContain("stream_options", endpoint.Requests[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOlderTokenLimitIsUsedWhenTheNewOneIsRefused()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("Unsupported parameter: max_completion_tokens", "max_completion_tokens"),
            OpenAiRecordings.Chat.TextDelta("."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        Assert.Contains("\"max_tokens\":", endpoint.Requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("max_completion_tokens", endpoint.Requests[1], StringComparison.Ordinal);
    }

    /// <summary>What each rung becomes in the body, on both OpenAI shapes.</summary>
    [Theory]
    [InlineData(ThinkingEffort.Low, "low")]
    [InlineData(ThinkingEffort.Medium, "medium")]
    [InlineData(ThinkingEffort.High, "high")]
    [InlineData(ThinkingEffort.Xhigh, "high")]
    [InlineData(ThinkingEffort.Max, "high")]
    public async Task EachRungReachesTheWireAsItself(ThinkingEffort effort, string expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("Half full."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        await OpenAiRecordings.DrainAsync(
            provider,
            OpenAiRecordings.Ask() with { Effort = effort },
            Token);

        Assert.Contains(
            $"\"reasoning_effort\":\"{expected}\"",
            Assert.Single(endpoint.Requests),
            StringComparison.Ordinal);
    }
}
