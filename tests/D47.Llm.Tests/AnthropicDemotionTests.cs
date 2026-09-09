using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class AnthropicDemotionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>What a pre-4.6 model actually says when the two fields reach it.</summary>
    private const string EffortRefusal =
        """{"type":"error","error":{"type":"invalid_request_error","message":"output_config.effort: Extra inputs are not permitted"}}""";

    /// <summary>A refusal that names no field at all, which is most of them.</summary>
    private const string VagueRefusal =
        """{"type":"error","error":{"type":"invalid_request_error","message":"could not process that request"}}""";

    public AnthropicDemotionTests() => EndpointDemotions.Clear();

    private static async Task<List<LlmStreamEvent>> DrainAsync(RecordedEndpoint endpoint, string model) =>
        await Recordings.DrainAsync(endpoint, Recordings.Request(model), Token);

    [Fact]
    public async Task AModelThatRefusesTheEffortIsRetriedOnceWithoutIt()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(400, EffortRefusal, Recordings.OneWord());

        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl);

        Assert.True(provider.CapabilitiesFor("claude-neverheardof-9").SupportsThinkingEffort);

        var events = await DrainAsync(endpoint, "claude-neverheardof-9");

        Assert.Equal("Acknowledged", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());

        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("output_config", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.Contains("thinking", endpoint.Requests[0], StringComparison.Ordinal);

        // Both come off together: they are one capability, not two fields that fail at the same time.
        Assert.DoesNotContain("output_config", endpoint.Requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("thinking", endpoint.Requests[1], StringComparison.Ordinal);

        Assert.False(provider.CapabilitiesFor("claude-neverheardof-9").SupportsThinkingEffort);
    }

    [Fact]
    public async Task ARefusalOnOneModelDoesNotDemoteAnother()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(400, EffortRefusal, Recordings.OneWord());

        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl);

        await DrainAsync(endpoint, "claude-cheap-9");
        await DrainAsync(endpoint, "claude-opus-5");

        Assert.False(provider.CapabilitiesFor("claude-cheap-9").SupportsThinkingEffort);
        Assert.True(provider.CapabilitiesFor("claude-opus-5").SupportsThinkingEffort);

        // Refused, retried without, then a third request for the other model that still carries both fields.
        Assert.Equal(3, endpoint.Requests.Count);
        Assert.Contains("output_config", endpoint.Requests[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameRefusalIsNotRetriedForever()
    {
        using var endpoint = RecordedEndpoint.Failing(400, EffortRefusal);

        var first = await DrainAsync(endpoint, "claude-stubborn-9");

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(first));
        Assert.Equal(2, endpoint.Requests.Count);

        // The fields are already gone from the request, so there is one attempt and one failure.
        var second = await DrainAsync(endpoint, "claude-stubborn-9");

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(second));
        Assert.Equal(3, endpoint.Requests.Count);
        Assert.DoesNotContain("output_config", endpoint.Requests[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalThatNamesNothingIsNotADemotion()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(400, VagueRefusal, Recordings.OneWord());

        var events = await DrainAsync(endpoint, "claude-neverheardof-9");

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
        Assert.Single(endpoint.Requests);

        Assert.True(new AnthropicLlmProvider("test-key", endpoint.BaseUrl)
            .CapabilitiesFor("claude-neverheardof-9").SupportsThinkingEffort);
    }

    [Fact]
    public async Task TheKnownCaseIsNeverProbed()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        var events = await DrainAsync(endpoint, "claude-haiku-4-5");

        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());
        Assert.DoesNotContain("output_config", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndpointWideDemotionAndAModelKeyedOneAreDifferentEntries()
    {
        const string address = "http://127.0.0.1:1/v1";

        Assert.True(EndpointDemotions.Demote(address, Demotable.ReasoningEffort));

        Assert.False(EndpointDemotions.Allows(address, Demotable.ReasoningEffort));
        Assert.True(EndpointDemotions.Allows(address, Demotable.ReasoningEffort, "qwen3:30b"));

        Assert.True(EndpointDemotions.Demote(address, Demotable.ReasoningEffort, "qwen3:30b"));
        Assert.False(EndpointDemotions.Allows(address, Demotable.ReasoningEffort, "qwen3:30b"));

        // The model-keyed entry sits beside the endpoint-wide one, not inside it.
        Assert.Equal([Demotable.ReasoningEffort], EndpointDemotions.RefusedBy(address));
    }
}
