using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>The call class chosen in Core arrives as a field in the request body; a server that refuses it costs a field, not a turn.</summary>
[Collection(nameof(EndpointDemotionCollection))]
public class TheSamplerReachesTheWireTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public TheSamplerReachesTheWireTests() => EndpointDemotions.Clear();

    private static JsonElement Body(RecordedEndpoint endpoint) =>
        JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement.Clone();

    private static double? TemperatureIn(JsonElement body) =>
        body.TryGetProperty("temperature", out var temperature) ? temperature.GetDouble() : null;

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

        Assert.Equal("Half full.", Assert.Single(events.OfType<LlmStreamEvent.TextDelta>()).Text);
        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());

        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("temperature", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("temperature", endpoint.Requests[1], StringComparison.Ordinal);

        // The refusal message names both fields, so the effort router keeps its own lever.
        Assert.Contains("reasoning_effort", endpoint.Requests[1], StringComparison.Ordinal);
    }

    /// <summary>Sampling is read before effort, and that ordering is the test.</summary>
    [Theory]
    [InlineData("Unsupported parameter: 'temperature' is not supported with this model.", Demotable.Sampling)]
    [InlineData("temperature is not supported with reasoning_effort", Demotable.Sampling)]
    [InlineData("top_p may not be set", Demotable.Sampling)]
    [InlineData("Unrecognized request argument supplied: reasoning_effort", Demotable.ReasoningEffort)]
    [InlineData("The model produced invalid output.", null)]
    internal void ARefusalNamingSamplingIsReadAsSampling(string message, Demotable? expected) =>
        Assert.Equal(expected, ChatCompletionsLlmProvider.WhatWasRejected(message));

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
