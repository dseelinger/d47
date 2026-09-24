using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// A context overflow names the model's context size. It is kept for the endpoint and model, reported as a
/// capability, and caps the reply length asked for from then on.
/// </summary>
[Collection(nameof(EndpointDemotionCollection))]
public class AnOverflowTeachesTheContextSizeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public AnOverflowTeachesTheContextSizeTests() => EndpointDemotions.Clear();

    [Fact]
    public async Task TheCapturedFrameRecordsTheContextForThatModelOnly()
    {
        using var endpoint = RecordedEndpoint.Streaming(AContextOverflowSaysHowToFixItTests.LmStudioOverflow);
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        Assert.Null(provider.CapabilitiesFor("test-model").ContextTokens);

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask("test-model"), Token);

        Assert.Equal(16384, provider.CapabilitiesFor("test-model").ContextTokens);
        Assert.Null(provider.CapabilitiesFor("another-model").ContextTokens);
    }

    [Fact]
    public async Task AnOverflowReturnedAsAStatusCodeIsRecordedToo()
    {
        using var endpoint = RecordedEndpoint.Failing(
            400,
            """{"error":{"code":400,"message":"request (20016 tokens) exceeds the available context size (8192 tokens), try increasing it","type":"exceed_context_size_error","n_prompt_tokens":20016,"n_ctx":8192}}""");
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask("test-model"), Token);

        Assert.Equal(8192, provider.CapabilitiesFor("test-model").ContextTokens);
    }

    [Fact]
    public async Task ALaterOverflowReplacesTheRecordedSize()
    {
        using var endpoint = RecordedEndpoint.Streaming(AContextOverflowSaysHowToFixItTests.LmStudioOverflow);
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        EndpointDemotions.RecordContext(OpenAiEndpoint.Normalise(endpoint.BaseUrl), "test-model", 65536);
        Assert.Equal(65536, provider.CapabilitiesFor("test-model").ContextTokens);

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask("test-model"), Token);

        Assert.Equal(16384, provider.CapabilitiesFor("test-model").ContextTokens);
    }

    [Fact]
    public async Task TheReplyIsCappedAtAQuarterOfTheContext()
    {
        using var endpoint = RecordedEndpoint.Streaming(AContextOverflowSaysHowToFixItTests.LmStudioOverflow);
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        Assert.Equal(8192, MaxOutput(provider, "test-model"));

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask("test-model"), Token);

        Assert.Equal(4096, MaxOutput(provider, "test-model"));
        Assert.Equal(8192, MaxOutput(provider, "another-model"));
    }

    [Fact]
    public async Task TheCapHoldsWhenTheOlderFieldNameIsSent()
    {
        using var endpoint = RecordedEndpoint.RefusingThenStreaming(
            400,
            OpenAiRecordings.Refusal("Unrecognized request argument supplied: max_completion_tokens", null),
            AContextOverflowSaysHowToFixItTests.LmStudioOverflow);
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask("test-model"), Token);

        Assert.Equal(4096, MaxOutput(provider, "test-model", "max_tokens"));
    }

    [Fact]
    public void TheOtherProvidersDoNotKnowTheirContext()
    {
        using var responses = new ResponsesLlmProvider(apiKey: "test-key", "http://127.0.0.1:1/v1");
        var anthropic = new AnthropicLlmProvider("test-key", "http://127.0.0.1:1");

        Assert.Null(responses.CapabilitiesFor("test-model").ContextTokens);
        Assert.Null(anthropic.CapabilitiesFor("claude-sonnet-5").ContextTokens);
    }

    private static int MaxOutput(ChatCompletionsLlmProvider provider, string model, string field = "max_completion_tokens")
    {
        using var document = JsonDocument.Parse(provider.BuildBody(OpenAiRecordings.Ask(model)));

        return document.RootElement.GetProperty(field).GetInt32();
    }
}
