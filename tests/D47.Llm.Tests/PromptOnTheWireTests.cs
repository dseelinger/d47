using System.Text.Json;
using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// What the assembled prompt looks like when it actually leaves the machine
/// (architecture.md §6).
/// <para>
/// Asserted against the <b>bytes the SDK sent</b>, captured by the recorded endpoint, rather than
/// against the parameter object <c>BuildParameters</c> returns. The two can disagree — the object
/// is what d47 asked for and the JSON is what the provider receives — and every property here is
/// about the JSON: whether a text-only turn stays a plain string, where the cache breakpoint
/// lands, and which role live game state arrives under.
/// </para>
/// <para>
/// The prompt is ordered by volatility with a breakpoint after the persona, so everything above
/// it is byte-identical across turns. A change that reorders or re-types any of it does not fail
/// — it silently stops caching, and the bill goes up.
/// </para>
/// </summary>
public class PromptOnTheWireTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Sends one turn and hands back the parsed request body.</summary>
    private static async Task<JsonElement> SentAsync(LlmRequest request)
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());

        await Recordings.DrainAsync(endpoint, request, Token);

        return JsonDocument.Parse(Assert.Single(endpoint.Requests)).RootElement.Clone();
    }

    private static LlmRequest With(
        IReadOnlyList<ConversationMessage> history,
        string? liveGameState = null,
        string model = "claude-opus-5") => new()
    {
        Model = model,
        Effort = ThinkingEffort.Medium,
        Prompt = new PromptAssembly { History = history, LiveGameState = liveGameState },
    };

    /// <summary>
    /// An ordinary text turn stays an ordinary string on the wire. Not an optimisation: a
    /// text-only session must serialise exactly as it did before tools existed, or every cache
    /// entry written by a previous version is invalidated by the upgrade.
    /// </summary>
    [Fact]
    public async Task ATextOnlyTurnIsAPlainStringRatherThanABlockList()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var content = sent.GetProperty("messages")[0].GetProperty("content");

        Assert.Equal(JsonValueKind.String, content.ValueKind);
        Assert.Equal("fuel?", content.GetString());
    }

    /// <summary>
    /// The system block carries the cache breakpoint. It is the one breakpoint that is always
    /// spent, and it is what makes the guardrails, the persona and About Me free after the first
    /// turn of a session.
    /// </summary>
    [Fact]
    public async Task TheSystemBlockIsMarkedCacheable()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var system = sent.GetProperty("system")[0];

        Assert.Equal("ephemeral", system.GetProperty("cache_control").GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(system.GetProperty("text").GetString()));
    }

    /// <summary>
    /// The guardrails are in that block, and they are above the persona so switching personality
    /// off cannot strip them. Asserted on the wire because that is the only place the ordering is
    /// actually realised.
    /// </summary>
    [Fact]
    public async Task TheGuardrailsAreInTheCachedSystemBlock()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var text = sent.GetProperty("system")[0].GetProperty("text").GetString()!;

        Assert.Contains(Guardrails.Text, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// On a model that supports operator system messages, live game state arrives as one. It
    /// cannot be spoofed by journal content that way, which matters because everything in it is
    /// journal-derived and therefore untrusted.
    /// </summary>
    [Fact]
    public async Task LiveGameStateArrivesAsAnOperatorMessageWhereItCan()
    {
        var sent = await SentAsync(With(
            [new ConversationMessage(ConversationRole.User, "where am I?")],
            liveGameState: "System: Shinrarta Dezhra",
            model: "claude-opus-5"));

        var messages = sent.GetProperty("messages");
        var last = messages[messages.GetArrayLength() - 1];

        Assert.Equal("system", last.GetProperty("role").GetString());
        Assert.Contains("Shinrarta Dezhra", last.GetProperty("content").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// On a model that does not, it folds into the last user turn as a <c>&lt;system-reminder&gt;</c>
    /// instead. Sonnet 5 is the case that exists — deliberately absent from the operator list,
    /// which is exactly the sort of gap "capabilities as state" is meant to absorb.
    /// </summary>
    [Fact]
    public async Task LiveGameStateFoldsIntoTheUserTurnWhereItCannot()
    {
        var sent = await SentAsync(With(
            [new ConversationMessage(ConversationRole.User, "where am I?")],
            liveGameState: "System: Shinrarta Dezhra",
            model: "claude-sonnet-5"));

        var messages = sent.GetProperty("messages");

        Assert.Equal(1, messages.GetArrayLength());
        Assert.DoesNotContain(
            messages.EnumerateArray(),
            message => message.GetProperty("role").GetString() == "system");

        var content = messages[0].GetProperty("content").GetString()!;

        Assert.Contains("<system-reminder>", content, StringComparison.Ordinal);
        Assert.Contains("Shinrarta Dezhra", content, StringComparison.Ordinal);
        Assert.Contains("where am I?", content, StringComparison.Ordinal);
    }

    /// <summary>No live game state means no extra message at all, on either kind of model.</summary>
    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-sonnet-5")]
    public async Task NoLiveGameStateAddsNothing(string model)
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "hello")], model: model));

        Assert.Equal(1, sent.GetProperty("messages").GetArrayLength());
        Assert.Equal("hello", sent.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    /// <summary>
    /// A tool round trip keeps its pairing on the wire. The model is shown its own call and what
    /// came back; a result that lost its id is a call the model never got an answer to.
    /// </summary>
    [Fact]
    public async Task AToolRoundTripKeepsItsPairing()
    {
        var sent = await SentAsync(With(
        [
            new ConversationMessage(ConversationRole.User, "gear up"),
            new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse("toolu_9", "set_gear", """{"down":false}""")]),
            new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult("toolu_9", "gear is up", IsError: false)]),
        ]));

        var messages = sent.GetProperty("messages");

        var call = messages[1].GetProperty("content")[0];
        Assert.Equal("tool_use", call.GetProperty("type").GetString());
        Assert.Equal("toolu_9", call.GetProperty("id").GetString());
        Assert.Equal("set_gear", call.GetProperty("name").GetString());
        Assert.False(call.GetProperty("input").GetProperty("down").GetBoolean());

        var result = messages[2].GetProperty("content")[0];
        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal("toolu_9", result.GetProperty("tool_use_id").GetString());
    }

    /// <summary>
    /// A tool result that failed is marked as such rather than passed off as an answer. The model
    /// is being told the call did not work, which is a different thing from it having returned
    /// the word "error".
    /// </summary>
    [Fact]
    public async Task AFailedToolResultIsMarkedAsAnError()
    {
        var sent = await SentAsync(With(
        [
            new ConversationMessage(ConversationRole.User, "gear up"),
            new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult("toolu_9", "Elite is not in the foreground", IsError: true)]),
        ]));

        Assert.True(sent.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("is_error").GetBoolean());
    }

    /// <summary>
    /// A long agentic exchange spends an intermediate breakpoint, because each breakpoint only
    /// looks back twenty content blocks — and past that the next turn silently re-bills the whole
    /// prefix. Sixteen tool rounds is well over the fifteen-block threshold.
    /// </summary>
    [Fact]
    public async Task ALongToolExchangeSpendsAnIntermediateBreakpoint()
    {
        var history = new List<ConversationMessage> { new(ConversationRole.User, "do the thing") };

        for (var i = 0; i < 16; i++)
        {
            history.Add(new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse($"toolu_{i}", "step", "{}")]));

            history.Add(new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult($"toolu_{i}", "done", IsError: false)]));
        }

        var sent = await SentAsync(With(history));

        var marked = sent.GetProperty("messages")
            .EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array)
            .SelectMany(message => message.GetProperty("content").EnumerateArray())
            .Count(block => block.TryGetProperty("cache_control", out var control)
                            && control.ValueKind == JsonValueKind.Object);

        Assert.InRange(marked, 1, 3);
    }

    /// <summary>
    /// A short exchange spends none of them. Every breakpoint is a cache entry written, and
    /// spending one on a conversation that fits inside the lookback buys nothing.
    /// <para>
    /// "Not spent" is the key being absent <em>or</em> explicitly null — the SDK serialises the
    /// unset property rather than omitting it, so a test that only checked for absence would pass
    /// whatever the value was.
    /// </para>
    /// </summary>
    [Fact]
    public async Task AShortExchangeSpendsNoIntermediateBreakpoint()
    {
        var sent = await SentAsync(With(
        [
            new ConversationMessage(ConversationRole.User, "gear up"),
            new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult("toolu_1", "done", IsError: false)]),
        ]));

        var block = sent.GetProperty("messages")[1].GetProperty("content")[0];

        Assert.True(
            !block.TryGetProperty("cache_control", out var control) || control.ValueKind == JsonValueKind.Null,
            $"a breakpoint was spent on a two-block exchange: {block}");
    }

    /// <summary>
    /// The effort setting reaches the wire. It is the difference between a snappy answer and one
    /// that thinks for ten seconds, and it is chosen per turn.
    /// </summary>
    [Theory]
    [InlineData(ThinkingEffort.Low, "low")]
    [InlineData(ThinkingEffort.Medium, "medium")]
    [InlineData(ThinkingEffort.High, "high")]
    [InlineData(ThinkingEffort.Max, "max")]
    public async Task TheThinkingEffortIsSent(ThinkingEffort effort, string expected)
    {
        var request = Recordings.Request() with { Effort = effort };
        var sent = await SentAsync(request);

        Assert.Equal(expected, sent.GetProperty("output_config").GetProperty("effort").GetString());
    }

    /// <summary>
    /// Thinking is adaptive rather than a token budget: <c>budget_tokens</c> is removed on Opus 5
    /// and returns a 400.
    /// </summary>
    [Fact]
    public async Task ThinkingIsAdaptiveWithNoTokenBudget()
    {
        var sent = await SentAsync(Recordings.Request());
        var thinking = sent.GetProperty("thinking");

        Assert.Equal("adaptive", thinking.GetProperty("type").GetString());
        Assert.False(thinking.TryGetProperty("budget_tokens", out _));
    }
}
