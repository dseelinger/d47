using System.Text.Json;
using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>A request that asks for no thinking sends <c>disabled</c> only to a model that accepts it.</summary>
[Collection(nameof(EndpointDemotionCollection))]
public class ThinkingIsTurnedOffWhereTheModelAllowsItTests
{
    private const string ThinkingRefusal =
        """{"type":"error","error":{"type":"invalid_request_error","message":"thinking.type: disabled is not supported for this model"}}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ThinkingIsTurnedOffWhereTheModelAllowsItTests() => EndpointDemotions.Clear();

    private static LlmRequest Request(string model, bool thinking, ThinkingEffort effort = ThinkingEffort.Low) =>
        Recordings.Request(model) with { Effort = effort, ThinkingAllowed = thinking };

    /// <summary>The <c>thinking</c> and <c>output_config</c> the request is sent with, or null where absent.</summary>
    private static (string? Thinking, string? Effort) Sent(LlmRequest request)
    {
        var parameters = new AnthropicLlmProvider("test-key").BuildParameters(request);

        using var thinking = parameters.Thinking is { } config
            ? JsonDocument.Parse(JsonSerializer.Serialize(config))
            : null;

        using var output = parameters.OutputConfig is { } outputConfig
            ? JsonDocument.Parse(JsonSerializer.Serialize(outputConfig))
            : null;

        return (
            thinking?.RootElement.GetProperty("type").GetString(),
            output?.RootElement.GetProperty("effort").GetString());
    }

    [Fact]
    public void ARequestThatAllowsThinkingIsSentAdaptive()
    {
        Assert.Equal(("adaptive", "low"), Sent(Request("claude-haiku-5-5", thinking: true)));
    }

    [Fact]
    public void AModelThatAcceptsDisabledIsSentDisabledWithItsEffort()
    {
        Assert.Equal(("disabled", "low"), Sent(Request("claude-haiku-5-5", thinking: false)));
    }

    [Fact]
    public void AModelThatRejectsDisabledKeepsAdaptive()
    {
        Assert.Equal(("adaptive", "low"), Sent(Request("claude-opus-5-5", thinking: false)));
        Assert.Equal(("adaptive", "low"), Sent(Request("claude-sonnet-5-5", thinking: false)));
    }

    [Fact]
    public void AnEffortAboveHighKeepsAdaptive()
    {
        Assert.Equal(("adaptive", "xhigh"), Sent(Request("claude-haiku-5-5", thinking: false, ThinkingEffort.Xhigh)));
    }

    [Fact]
    public void AModelWithoutThinkingIsSentNeither()
    {
        Assert.Equal((null, null), Sent(Request("claude-haiku-4-5", thinking: false)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusedDisabledDoesNotDemoteThinking()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(400, ThinkingRefusal, Recordings.OneWord());

        var events = await Recordings.DrainAsync(endpoint, Request("claude-haiku-5-5", thinking: false), Token);

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
        Assert.Contains("\"disabled\"", Assert.Single(endpoint.Requests), StringComparison.Ordinal);

        Assert.True(new AnthropicLlmProvider("test-key", endpoint.BaseUrl)
            .CapabilitiesFor("claude-haiku-5-5").SupportsThinkingEffort);
    }
}
