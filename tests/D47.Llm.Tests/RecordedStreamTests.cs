using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// The streaming decoder, driven against recorded responses (Phase 2, Phase 14).
/// <para>
/// Everything here is d47's half of the exchange. The provider seam's contract is that a turn
/// arrives as a sequence of <see cref="LlmStreamEvent"/> and that <b>nothing ever leaves it as an
/// exception</b> — the turn loop above has no handler for one, and a transport failure that
/// escaped would take the tick with it.
/// </para>
/// </summary>
public class RecordedStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AOneWordTurnArrivesAsTextThenCompleted()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord("Acknowledged"));

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        var text = Assert.IsType<LlmStreamEvent.TextDelta>(events[0]);
        Assert.Equal("Acknowledged", text.Text);

        var done = Assert.IsType<LlmStreamEvent.Completed>(events[^1]);
        Assert.Equal(LlmStopReason.Completed, done.StopReason);
    }

    [Fact]
    public async Task TextArrivesInTheOrderItWasSent()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.TextBlockStart(),
            Recordings.TextDelta("Fuel "),
            Recordings.TextDelta("is "),
            Recordings.TextDelta("low."),
            Recordings.BlockStop(),
            Recordings.MessageDelta("end_turn"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.Equal(
            "Fuel is low.",
            string.Concat(events.OfType<LlmStreamEvent.TextDelta>().Select(delta => delta.Text)));
    }

    /// <summary>
    /// Thinking is its own event rather than text. It reaches the panel and never the voice —
    /// a companion reading its own reasoning aloud is not the product.
    /// </summary>
    [Fact]
    public async Task ThinkingIsKeptApartFromTheAnswer()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.ThinkingBlockStart(),
            Recordings.ThinkingDelta("Checking the tank."),
            Recordings.BlockStop(),
            Recordings.TextBlockStart(1),
            Recordings.TextDelta("Half full.", 1),
            Recordings.BlockStop(1),
            Recordings.MessageDelta("end_turn"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.Equal(
            "Checking the tank.",
            string.Concat(events.OfType<LlmStreamEvent.ThinkingDelta>().Select(delta => delta.Text)));

        Assert.Equal(
            "Half full.",
            string.Concat(events.OfType<LlmStreamEvent.TextDelta>().Select(delta => delta.Text)));
    }

    /// <summary>
    /// A tool call is delivered as fragments that are each unparseable, and is emitted once —
    /// whole. Running a tool on a half-built argument object is the one mistake this must not
    /// make possible.
    /// </summary>
    [Fact]
    public async Task AToolCallIsAssembledFromItsFragmentsAndEmittedOnce()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.ToolUseBlockStart("toolu_1", "set_gear"),
            Recordings.InputJsonDelta("{\"do"),
            Recordings.InputJsonDelta("wn\":t"),
            Recordings.InputJsonDelta("rue}"),
            Recordings.BlockStop(),
            Recordings.MessageDelta("tool_use"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        var call = Assert.Single(events.OfType<LlmStreamEvent.ToolUse>());

        Assert.Equal("toolu_1", call.Id);
        Assert.Equal("set_gear", call.Name);
        Assert.Equal("""{"down":true}""", call.InputJson);
    }

    /// <summary>
    /// A tool with no parameters produces no <c>input_json_delta</c> at all, and an empty string
    /// is not a JSON object — the argument parser would read it as malformed and drop a call that
    /// was fine.
    /// </summary>
    [Fact]
    public async Task AToolCallWithNoArgumentsArrivesAsAnEmptyObject()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.ToolUseBlockStart("toolu_2", "honk"),
            Recordings.BlockStop(),
            Recordings.MessageDelta("tool_use"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.Equal("{}", Assert.Single(events.OfType<LlmStreamEvent.ToolUse>()).InputJson);
    }

    [Fact]
    public async Task TwoToolCallsInOneTurnKeepTheirOwnArguments()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.ToolUseBlockStart("toolu_a", "set_gear"),
            Recordings.ToolUseBlockStart("toolu_b", "set_lights", 1),
            Recordings.InputJsonDelta("{\"on\":true}", 1),
            Recordings.InputJsonDelta("{\"down\":false}"),
            Recordings.BlockStop(1),
            Recordings.BlockStop(),
            Recordings.MessageDelta("tool_use"),
            Recordings.MessageStop());

        var calls = (await Recordings.DrainAsync(endpoint, cancellationToken: Token))
            .OfType<LlmStreamEvent.ToolUse>()
            .ToDictionary(call => call.Id, call => call.InputJson, StringComparer.Ordinal);

        Assert.Equal("""{"down":false}""", calls["toolu_a"]);
        Assert.Equal("""{"on":true}""", calls["toolu_b"]);
    }

    /// <summary>
    /// Every stop reason d47 distinguishes, and it distinguishes them because they mean different
    /// things to the turn loop. <c>pause_turn</c> is the one that was silently absent: it arrives
    /// when the provider stops a long server-side turn part-way, and falling through to Completed
    /// reported truncation as a finished answer.
    /// </summary>
    [Theory]
    [InlineData("end_turn", LlmStopReason.Completed)]
    [InlineData("max_tokens", LlmStopReason.MaxTokens)]
    [InlineData("tool_use", LlmStopReason.ToolUse)]
    [InlineData("refusal", LlmStopReason.Refusal)]
    [InlineData("pause_turn", LlmStopReason.Paused)]
    [InlineData("something_new", LlmStopReason.Completed)]
    public async Task TheStopReasonIsTranslated(string wire, LlmStopReason expected)
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.TextBlockStart(),
            Recordings.TextDelta("."),
            Recordings.BlockStop(),
            Recordings.MessageDelta(wire),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.Equal(expected, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).StopReason);
    }

    /// <summary>
    /// Usage arrives split across two events that carry it as different types, and the merge
    /// takes the larger of each field — so a later event that omits one does not zero what an
    /// earlier one already reported. This is what the spend tracker bills against.
    /// </summary>
    [Fact]
    public async Task UsageIsMergedAcrossTheStartAndTheDelta()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(input: 1200, output: 1, cacheCreation: 300, cacheRead: 900),
            Recordings.TextBlockStart(),
            Recordings.TextDelta("."),
            Recordings.BlockStop(),

            // The delta reports only the final output count. Input and the two cache figures
            // arrived on message_start and must survive.
            Recordings.MessageDelta("end_turn", output: 64),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);
        var usage = Assert.IsType<LlmStreamEvent.Completed>(events[^1]).Usage;

        Assert.Equal(1200, usage.InputTokens);
        Assert.Equal(64, usage.OutputTokens);
        Assert.Equal(300, usage.CacheCreationInputTokens);
        Assert.Equal(900, usage.CacheReadInputTokens);
    }

    /// <summary>
    /// Web searches are billed separately and are counted the same way. Three is the ceiling the
    /// provider declares, and the count is what proves the ceiling is worth having.
    /// </summary>
    [Fact]
    public async Task WebSearchRequestsAreCounted()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.TextBlockStart(),
            Recordings.TextDelta("."),
            Recordings.BlockStop(),
            Recordings.MessageDelta("end_turn", webSearches: 2),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.Equal(2, Assert.IsType<LlmStreamEvent.Completed>(events[^1]).Usage.WebSearchRequests);
    }

    /// <summary>
    /// A turn that says nothing still completes. The turn loop distinguishes "answered with
    /// nothing" from "failed", and an empty stream that produced no Completed event would leave
    /// it waiting.
    /// </summary>
    [Fact]
    public async Task ATurnWithNoContentStillCompletes()
    {
        using var endpoint = RecordedEndpoint.Streaming(
            Recordings.MessageStart(),
            Recordings.MessageDelta("end_turn"),
            Recordings.MessageStop());

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.IsType<LlmStreamEvent.Completed>(Assert.Single(events));
    }

    /// <summary>The request really did go to the endpoint, carrying the model that was asked for.</summary>
    [Fact]
    public async Task TheRecordedEndpointSeesTheRequest()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(endpoint, Recordings.Request("claude-opus-5", "how much fuel?"), Token);

        var body = Assert.Single(endpoint.Requests);

        Assert.Contains("claude-opus-5", body, StringComparison.Ordinal);
        Assert.Contains("how much fuel?", body, StringComparison.Ordinal);
    }
}
