using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// Asking an endpoint what it is, and answering it when it says no (list.md Phase 29, "What the
/// endpoint can actually do").
/// <para>
/// A model list says what an endpoint <em>serves</em> and nothing about what it <em>accepts</em>.
/// The rest — tool calls, reasoning effort, usage — is learned from the first failure, once per
/// capability per endpoint, and never written down.
/// </para>
/// </summary>
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

    /// <summary>
    /// An empty list is a success. A gateway is entitled to keep its catalogue to itself, and the
    /// model row already accepts free text — which is the degradation that was designed in from
    /// the beginning rather than a failure to report.
    /// </summary>
    [Fact]
    public void AnEndpointThatListsNothingHasStillAnswered()
    {
        var models = EndpointHandshake.Read("{\"object\":\"list\",\"data\":[]}");

        Assert.Equal(EndpointReach.Answered, models.Reach);
        Assert.Empty(models.Ids);
    }

    /// <summary>
    /// Something is at that address and it is not speaking this protocol. Worth saying plainly:
    /// it is the one failure the address itself explains.
    /// </summary>
    [Fact]
    public void SomethingThatIsNotAModelListIsARefusalRatherThanAnEmptyPicker()
    {
        var models = EndpointHandshake.Read("<html><body>404 not found</body></html>");

        Assert.Equal(EndpointReach.Refused, models.Reach);
        Assert.NotNull(models.Detail);
    }

    /// <summary>
    /// Unreachable and refused are different answers and are kept apart, for the reason the key
    /// check gives: telling a Commander their address is wrong when the server is simply not
    /// started yet sends them to edit a setting that was already correct.
    /// </summary>
    [Fact]
    public async Task NotStartedYetIsNotTheSameAsWrong()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:1/v1");

        Assert.Equal(EndpointReach.Unreachable, (await provider.ListModelsAsync(Token)).Reach);

        using var refusing = RecordedEndpoint.Failing(401, OpenAiRecordings.Refusal("no key", null));
        using var toRefusing = new ChatCompletionsLlmProvider(apiKey: null, refusing.BaseUrl);

        Assert.Equal(EndpointReach.Refused, (await toRefusing.ListModelsAsync(Token)).Reach);
    }

    /// <summary>
    /// The demotion, end to end. The endpoint refuses <c>reasoning_effort</c> by name, the turn is
    /// attempted once more without it, and it succeeds — which is what "advertise, then demote"
    /// buys over either offering nothing or failing the turn.
    /// </summary>
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

        // The Commander sees an answer, not a failure and not a retry.
        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());

        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("reasoning_effort", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning_effort", endpoint.Requests[1], StringComparison.Ordinal);

        // And the capability is off for this address from now on, which is what stops the next
        // turn paying for the same discovery.
        Assert.False(provider.CapabilitiesFor("any").SupportsThinkingEffort);
    }

    /// <summary>
    /// <b>Once, and once only.</b> A second refusal of something already demoted is a failure
    /// rather than another retry — a policy that searches for a working request shape is
    /// indistinguishable from an outage from the Commander's seat.
    /// </summary>
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

        // A second turn against the same address does not probe again: the field is already gone
        // from the request, so there is one attempt and one failure.
        var second = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);
        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(second));
        Assert.Equal(3, endpoint.Requests.Count);
    }

    /// <summary>
    /// A demotion is per address. Two endpoints refusing different things must not teach each
    /// other, or a working server loses a capability because a different one could not do it.
    /// </summary>
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

    /// <summary>
    /// <b>Nothing is demoted on a guess.</b> A rejection that names no field leaves every
    /// capability exactly where it was — turning one off because an unrelated request failed is
    /// how a working feature disappears for a session with nothing to point at.
    /// </summary>
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

    /// <summary>
    /// An ordinary tool error quotes the tool's own name, and reading that as the endpoint
    /// disowning the whole capability would turn tools off for a session because one call was
    /// malformed. The word "tool" alone is not evidence.
    /// </summary>
    [Theory]
    [InlineData("Invalid schema for function 'set_route': missing 'type'.", null)]
    [InlineData("tool_choice is not supported by this model", Demotable.Tools)]
    [InlineData("Unrecognized request argument supplied: reasoning_effort", Demotable.ReasoningEffort)]
    [InlineData("stream_options is not supported", Demotable.StreamUsage)]
    [InlineData("Unsupported parameter: max_completion_tokens", Demotable.ModernTokenLimit)]
    [InlineData("Rate limit reached for requests", null)]
    internal void OnlyANamedFieldCountsAsARefusal(string message, Demotable? expected) =>
        Assert.Equal(expected, ChatCompletionsLlmProvider.WhatWasRejected(message));

    /// <summary>
    /// Dropping <c>include_usage</c> costs the price of the turn rather than the turn, which is
    /// the honest side of that trade: unpriced beats a paid session reported as free.
    /// </summary>
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

    /// <summary>
    /// The older token-limit field is the fallback rather than no ceiling at all. A cockpit reply
    /// has to stop, and an unbounded one on a paid gateway is expensive as well as wrong.
    /// </summary>
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
}
