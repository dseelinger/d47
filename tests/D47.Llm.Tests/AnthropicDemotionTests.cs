using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// Answering Anthropic when it says no (Phase 54).
/// <para>
/// The gap this closes was never that Haiku 4.5 is unusual. It is that one provider learned from
/// refusals and the other could not: both OpenAI providers have wrapped every optional field in
/// <see cref="EndpointDemotions"/> since Phase 29, and this one sent <c>thinking</c> and
/// <c>output_config.effort</c> on every request with no condition, because every Anthropic model
/// took them on the day that was written.
/// </para>
/// <para>
/// So there are two mechanisms and each covers what the other cannot. The deny-list on the
/// provider handles what is known, and the case in front of us never costs even one failed turn —
/// that is <see cref="ProviderCapabilityTests.AnEffortIsDeclaredPerModel"/>. These tests are the
/// other half: a model d47 has never heard of that turns out to reject the fields heals itself
/// instead of failing for ever.
/// </para>
/// </summary>
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

    /// <summary>
    /// The demotion, end to end. The endpoint names a field it will not accept, the turn is sent
    /// once more without it, and it succeeds — so the Commander sees an answer rather than a
    /// failure, and never learns there was a first attempt.
    /// </summary>
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

        // Both come off together, because they are one capability rather than two fields that
        // happen to fail at the same time.
        Assert.DoesNotContain("output_config", endpoint.Requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("thinking", endpoint.Requests[1], StringComparison.Ordinal);

        // And it is off for that model from now on, which is what stops the next turn paying for
        // the same discovery.
        Assert.False(provider.CapabilitiesFor("claude-neverheardof-9").SupportsThinkingEffort);
    }

    /// <summary>
    /// <b>The reason the key carries the model, and the test that fails loudly if anyone narrows
    /// it back.</b> Anthropic serves five models from one address and they do not accept the same
    /// fields. Keyed on the address alone — which is right for an OpenAI-compatible endpoint, one
    /// server with one set of accepted fields — a single refusal from the cheap model would
    /// silently switch effort off for Opus 5 for the rest of the session, with nothing anywhere
    /// saying so.
    /// </summary>
    [Fact]
    public async Task ARefusalOnOneModelDoesNotDemoteAnother()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(400, EffortRefusal, Recordings.OneWord());

        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl);

        await DrainAsync(endpoint, "claude-cheap-9");
        await DrainAsync(endpoint, "claude-opus-5");

        Assert.False(provider.CapabilitiesFor("claude-cheap-9").SupportsThinkingEffort);
        Assert.True(provider.CapabilitiesFor("claude-opus-5").SupportsThinkingEffort);

        // Refused, retried without, then a third request for the other model that still carries
        // both fields. Two turns, three requests.
        Assert.Equal(3, endpoint.Requests.Count);
        Assert.Contains("output_config", endpoint.Requests[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Once, and once only.</b> A second refusal of something already demoted is a failure
    /// rather than another retry — a policy that searches for a working request shape is
    /// indistinguishable from an outage from the Commander's seat.
    /// </summary>
    [Fact]
    public async Task TheSameRefusalIsNotRetriedForever()
    {
        using var endpoint = RecordedEndpoint.Failing(400, EffortRefusal);

        var first = await DrainAsync(endpoint, "claude-stubborn-9");

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(first));
        Assert.Equal(2, endpoint.Requests.Count);

        // A second turn does not probe again: the fields are already gone from the request, so
        // there is one attempt and one failure.
        var second = await DrainAsync(endpoint, "claude-stubborn-9");

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(second));
        Assert.Equal(3, endpoint.Requests.Count);
        Assert.DoesNotContain("output_config", endpoint.Requests[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing is inferred from a rejection that names nothing. A demotion made on a guess is how
    /// a working capability gets turned off for a session with no way for the Commander to see
    /// why — and here it would also cost a wasted request on every turn that failed for an
    /// unrelated reason.
    /// </summary>
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

    /// <summary>
    /// The known case never costs even one failed turn, which is what the deny-list buys over the
    /// demotion on its own. Haiku is sent a request it accepts the first time.
    /// </summary>
    [Fact]
    public async Task TheKnownCaseIsNeverProbed()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        var events = await DrainAsync(endpoint, "claude-haiku-4-5");

        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());
        Assert.DoesNotContain("output_config", Assert.Single(endpoint.Requests), StringComparison.Ordinal);
    }

    /// <summary>
    /// The widened key did not move the OpenAI callers, asserted rather than assumed. They pass
    /// no model, so they share one entry per address exactly as they did before — and a demotion
    /// recorded against a named model is invisible to them, which is the same property as
    /// <see cref="ARefusalOnOneModelDoesNotDemoteAnother"/> read from the other end.
    /// </summary>
    [Fact]
    public void AnEndpointWideDemotionAndAModelKeyedOneAreDifferentEntries()
    {
        const string address = "http://127.0.0.1:1/v1";

        Assert.True(EndpointDemotions.Demote(address, Demotable.ReasoningEffort));

        Assert.False(EndpointDemotions.Allows(address, Demotable.ReasoningEffort));
        Assert.True(EndpointDemotions.Allows(address, Demotable.ReasoningEffort, "qwen3:30b"));

        Assert.True(EndpointDemotions.Demote(address, Demotable.ReasoningEffort, "qwen3:30b"));
        Assert.False(EndpointDemotions.Allows(address, Demotable.ReasoningEffort, "qwen3:30b"));

        // Still exactly one thing refused endpoint-wide: the model-keyed entry is beside it, not
        // inside it.
        Assert.Equal([Demotable.ReasoningEffort], EndpointDemotions.RefusedBy(address));
    }
}
