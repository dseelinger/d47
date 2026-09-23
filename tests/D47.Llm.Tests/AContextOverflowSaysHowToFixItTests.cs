using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// LM Studio answers 200 and sends llama.cpp's error body, wrapped in its own prefix, as an
/// <c>event: error</c> frame. The Commander hears the numbers and the fix, not the JSON.
/// </summary>
[Collection(nameof(EndpointDemotionCollection))]
public class AContextOverflowSaysHowToFixItTests
{
    /// <summary>Captured from LM Studio 0.4.25 on 2026-09-23.</summary>
    private const string LmStudioOverflow =
        "event: error\n"
        + """data: {"error":{"message":"Engine protocol predict request returned 400: {\"error\":{\"code\":400,\"message\":\"request (20016 tokens) exceeds the available context size (16384 tokens), try increasing it\",\"type\":\"exceed_context_size_error\",\"n_prompt_tokens\":20016,\"n_ctx\":16384}}"},"message":"Engine protocol predict request returned 400: ..."}"""
        + "\n\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public AContextOverflowSaysHowToFixItTests() => EndpointDemotions.Clear();

    private static async Task<LlmStreamEvent.Failed> FailureFor(params string[] frames)
    {
        using var endpoint = RecordedEndpoint.Streaming(frames);
        using var provider = new ChatCompletionsLlmProvider(apiKey: null, endpoint.BaseUrl);

        var events = await OpenAiRecordings.DrainAsync(provider, OpenAiRecordings.Ask(), Token);

        return Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
    }

    [Fact]
    public async Task TheCapturedFrameGivesBothNumbersAndTheFix()
    {
        var failed = await FailureFor(LmStudioOverflow);

        Assert.False(failed.Transient);
        Assert.Contains("16,384", failed.Message, StringComparison.Ordinal);
        Assert.Contains("20,016", failed.Message, StringComparison.Ordinal);
        Assert.Contains("32,768 or more", failed.Message, StringComparison.Ordinal);
        Assert.Contains("context length", failed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANestedErrorOfAnotherKindIsReducedToItsInnermostMessage()
    {
        var failed = await FailureFor(
            """data: {"error":{"message":"Engine protocol predict request returned 500: {\"error\":{\"code\":500,\"message\":\"the model crashed\",\"type\":\"server_error\"}}"}}"""
            + "\n\n");

        Assert.Equal("the model crashed", failed.Message);
    }

    [Fact]
    public async Task APlainMessageIsPassedThrough()
    {
        var failed = await FailureFor(OpenAiRecordings.Data("""{"error":{"message":"the model was unloaded"}}"""));

        Assert.Equal("the model was unloaded", failed.Message);
    }
}
