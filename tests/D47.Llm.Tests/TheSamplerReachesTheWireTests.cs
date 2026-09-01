using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// The call class chosen in Core arrives as a field in the request body
/// (<a href="https://github.com/dseelinger/d47/issues/98">#98</a>), and a server that will not
/// take it costs a field rather than a turn.
/// <para>
/// <b>Asserted against the bytes that left</b>, for the reason <c>PromptOnTheWireTests</c> gives:
/// what d47 asked for and what the endpoint receives can disagree, and the whole of #98 was that
/// nothing reached the wire at all.
/// </para>
/// </summary>
[Collection(nameof(EndpointDemotionCollection))]
public class TheSamplerReachesTheWireTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public TheSamplerReachesTheWireTests() => EndpointDemotions.Clear();

    private static JsonElement Body(RecordedEndpoint endpoint) =>
        JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement.Clone();

    private static double? TemperatureIn(JsonElement body) =>
        body.TryGetProperty("temperature", out var temperature) ? temperature.GetDouble() : null;

    /// <summary>
    /// <b>A warm call and a cold call do not arrive the same.</b> This is #98's first acceptance
    /// criterion, and before it every one of these bodies was byte-identical in this respect:
    /// no <c>temperature</c> at all, whatever the call was for.
    /// </summary>
    [Theory]
    [InlineData(false, 0.9)]
    [InlineData(true, 0.0)]
    public async Task ChatCompletionsCarriesWhatTheCallClassAskedFor(bool mechanical, double expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var sampling = mechanical ? LlmSampling.VoiceCasting : LlmSampling.Conversation;

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(sampling: sampling), Token);

        Assert.Equal(expected, TemperatureIn(Body(endpoint)));
    }

    /// <summary>The same, on the other OpenAI-shaped path — one seam, two dialects.</summary>
    [Theory]
    [InlineData(false, 0.9)]
    [InlineData(true, 0.0)]
    public async Task ResponsesCarriesWhatTheCallClassAskedFor(bool mechanical, double expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Responses.TextDelta("."),
            OpenAiRecordings.Responses.Completed(input: 10, cached: 0, written: 0, output: 1));

        using var provider = new ResponsesLlmProvider("sk-test", endpoint.BaseUrl);

        var sampling = mechanical ? LlmSampling.Log : LlmSampling.InCharacter;

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(sampling: sampling), Token);

        Assert.Equal(expected, TemperatureIn(Body(endpoint)));
    }

    /// <summary>
    /// <b>Saying nothing writes nothing.</b> The key check is the one call that wants the
    /// endpoint's own default — a field a gateway validates and rejects there reads as a rejected
    /// key, and sends a Commander to their account page for another one that will fail the same
    /// way. A zero written for "unstated" would be the bug this test exists to stop.
    /// </summary>
    [Fact]
    public async Task UnstatedWritesNoFieldRatherThanAZero()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            OpenAiRecordings.Chat.TextDelta("OK"),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        await OpenAiRecordings.DrainAsync(
            provider, OpenAiRecordings.Ask(sampling: LlmSampling.Unstated), Token);

        Assert.False(Body(endpoint).TryGetProperty("temperature", out _));
    }

    /// <summary>
    /// <b>An endpoint that refuses the field loses the field, not the turn</b> — #98's second
    /// acceptance criterion, through the demotion path every other optional field already uses.
    /// Reasoning models reject sampling outright on several servers, so this is the common case
    /// rather than a corner.
    /// </summary>
    [Fact]
    public async Task ARefusedTemperatureIsDroppedAndTheTurnStillAnswers()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal(
                "Unsupported parameter: 'temperature' is not supported with this model.",
                "temperature"),
            OpenAiRecordings.Chat.TextDelta("Half full."),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        // The Commander sees an answer, not a failure and not a retry.
        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());

        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("temperature", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("temperature", endpoint.Requests[1], StringComparison.Ordinal);

        // And the effort router keeps its own lever, which is the mistake a looser reading of the
        // refusal would have made: the message names both fields.
        Assert.Contains("reasoning_effort", endpoint.Requests[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Sampling is read before effort, and that ordering is the test.</b> A server refusing
    /// temperature on a reasoning model tends to name both fields in one sentence; taking the
    /// wrong one off would drop the effort router's lever and leave the refused field on the
    /// retry, which then fails for the same reason with nothing left to demote.
    /// </summary>
    [Theory]
    [InlineData("Unsupported parameter: 'temperature' is not supported with this model.", Demotable.Sampling)]
    [InlineData("temperature is not supported with reasoning_effort", Demotable.Sampling)]
    [InlineData("top_p may not be set", Demotable.Sampling)]
    [InlineData("Unrecognized request argument supplied: reasoning_effort", Demotable.ReasoningEffort)]
    [InlineData("The model produced invalid output.", null)]
    internal void ARefusalNamingSamplingIsReadAsSampling(string message, Demotable? expected) =>
        Assert.Equal(expected, ChatCompletionsLlmProvider.WhatWasRejected(message));

    /// <summary>
    /// <b>Nothing goes to Anthropic, whichever class asks</b>, and that is a decision rather than
    /// the silence #98 was about. Sampling was removed with the 4.7 generation — the pinned SDK
    /// marks the property obsolete saying so — so a temperature on this path is a 400 on every
    /// model a Commander would choose. Pinned here so that sending one again is deliberate.
    /// </summary>
    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-haiku-4-5")]
    public async Task NoTemperatureGoesToAnthropic(string model)
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(
            endpoint,
            Recordings.Request(model) with { Sampling = LlmSampling.InCharacter },
            Token);

        var body = JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement;

        Assert.False(body.TryGetProperty("temperature", out _));
        Assert.False(body.TryGetProperty("top_p", out _));
    }
}
