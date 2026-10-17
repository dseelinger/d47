using System.Text;
using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class AResponseSchemaReachesChatCompletionsTests
{
    private const string SchemaJson =
        "{\"type\":\"object\",\"properties\":{\"zulu\":{\"type\":\"string\"},\"alpha\":{\"type\":\"integer\"}},"
        + "\"required\":[\"zulu\"],\"additionalProperties\":false}";

    private static readonly LlmResponseSchema Schema = new("story_beat", SchemaJson);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public AResponseSchemaReachesChatCompletionsTests() => EndpointDemotions.Clear();

    [Fact]
    public void ASchemaBecomesAStrictResponseFormatWithTheSchemaUnchanged()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        var body = Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask() with { ResponseSchema = Schema }).Span);

        Assert.Contains(
            "\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"story_beat\",\"strict\":true,\"schema\":"
            + SchemaJson + "}}",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NoSchemaLeavesTheBodyAsItWas()
    {
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, "http://127.0.0.1:11434/v1");

        var body = Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask()).Span);

        Assert.DoesNotContain("response_format", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("'response_format' is not supported by this server", Demotable.ResponseFormat)]
    [InlineData("Unsupported value for json_schema: tool use cannot be combined", Demotable.ResponseFormat)]
    [InlineData("tool_choice is not supported by this model", Demotable.Tools)]
    [InlineData("Invalid schema for function 'set_route': tool schema is unsupported", Demotable.Tools)]
    internal void OnlyTheFormatFieldsCountAsAResponseFormatRefusal(string message, Demotable expected) =>
        Assert.Equal(expected, ChatCompletionsLlmProvider.WhatWasRejected(message));

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusedResponseFormatIsDroppedAndNotSentAgain()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("Unrecognized request argument supplied: response_format", "response_format"),
            OpenAiRecordings.Chat.TextDelta("{}"),
            OpenAiRecordings.Chat.Finish("stop"),
            OpenAiRecordings.Done());

        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);
        var request = OpenAiRecordings.Ask() with { ResponseSchema = Schema };

        var events = await OpenAiRecordings.DrainAsync(provider, request, Token);

        Assert.Empty(events.OfType<LlmStreamEvent.Failed>());
        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("response_format", endpoint.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("response_format", endpoint.Requests[1], StringComparison.Ordinal);

        await OpenAiRecordings.DrainAsync(provider, request, Token);

        Assert.Equal(3, endpoint.Requests.Count);
        Assert.DoesNotContain("response_format", endpoint.Requests[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsesIgnoresTheSchema()
    {
        using var provider = new ResponsesLlmProvider("sk-test", "http://127.0.0.1:11434/v1");

        var plain = Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask()).Span);
        var withSchema = Encoding.UTF8.GetString(provider.BuildBody(OpenAiRecordings.Ask() with { ResponseSchema = Schema }).Span);

        Assert.Equal(plain, withSchema);
    }

    [Fact]
    public void AnthropicIgnoresTheSchema()
    {
        var provider = new AnthropicLlmProvider("test-key");

        var plain = JsonSerializer.Serialize(provider.BuildParameters(OpenAiRecordings.Ask("claude-sonnet-5-5", "Hi")));
        var withSchema = JsonSerializer.Serialize(
            provider.BuildParameters(OpenAiRecordings.Ask("claude-sonnet-5-5", "Hi") with { ResponseSchema = Schema }));

        Assert.Equal(plain, withSchema);
    }
}
