using System.Text.Json;
using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

[Trait("Category", "Integration")]
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
        Sampling = LlmSampling.Conversation,
        Prompt = new PromptAssembly { History = history, LiveGameState = liveGameState },
    };

    /// <summary>
    /// A block list rather than a plain string, so the message has the same form on a later request where it
    /// no longer carries the breakpoint.
    /// </summary>
    [Fact]
    public async Task ATextOnlyTurnIsAOneBlockList()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var block = Assert.Single(sent.GetProperty("messages")[0].GetProperty("content").EnumerateArray());

        Assert.Equal("text", block.GetProperty("type").GetString());
        Assert.Equal("fuel?", block.GetProperty("text").GetString());
    }

    [Fact]
    public async Task TheSystemBlockIsMarkedCacheable()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var system = sent.GetProperty("system")[0];

        Assert.Equal("ephemeral", system.GetProperty("cache_control").GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(system.GetProperty("text").GetString()));
    }

    /// <summary>The guardrails sit above the persona, so switching personality off cannot strip them.</summary>
    [Fact]
    public async Task TheGuardrailsAreInTheCachedSystemBlock()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var text = sent.GetProperty("system")[0].GetProperty("text").GetString()!;

        Assert.Contains(Guardrails.Text, text, StringComparison.Ordinal);
    }

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
    /// Where it cannot, it folds into the last user turn as a <c>&lt;system-reminder&gt;</c> block after the
    /// question.
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

        var content = messages[0].GetProperty("content");

        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("where am I?", content[0].GetProperty("text").GetString());
        Assert.Equal(
            "<system-reminder>\nSystem: Shinrarta Dezhra\n</system-reminder>",
            content[1].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-sonnet-5")]
    public async Task NoLiveGameStateAddsNothing(string model)
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "hello")], model: model));

        Assert.Equal(1, sent.GetProperty("messages").GetArrayLength());

        var block = Assert.Single(sent.GetProperty("messages")[0].GetProperty("content").EnumerateArray());
        Assert.Equal("hello", block.GetProperty("text").GetString());
    }

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

    /// <summary>A tool round ends on a message whose content is blocks rather than a string.</summary>
    [Fact]
    public async Task LiveGameStateDoesNotEatTheToolResultItLandsOn()
    {
        var sent = await SentAsync(With(
        [
            new ConversationMessage(ConversationRole.User, "place the VR panel here"),
            new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse("toolu_9", "reanchor_vr_panel", "{}")]),
            new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult("toolu_9", "the panel is where you are looking", false)]),
        ],
            liveGameState: "System: Shinrarta Dezhra",
            model: "claude-sonnet-5"));

        var messages = sent.GetProperty("messages");
        var last = messages[messages.GetArrayLength() - 1];

        var result = last.GetProperty("content")[0];

        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal("toolu_9", result.GetProperty("tool_use_id").GetString());

        Assert.Contains(
            "Shinrarta Dezhra",
            last.GetProperty("content").ToString(),
            StringComparison.Ordinal);
    }

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
    /// A long agentic exchange spends an intermediate breakpoint, because each breakpoint only looks
    /// back twenty content blocks — and past that the next turn silently re-bills the whole prefix.
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

        var blocks = Blocks(sent);

        Assert.True(Marked(blocks[^1]), "the end of the history is not marked");
        Assert.InRange(blocks[..^1].Count(Marked), 1, 2);
    }

    /// <summary>A fifth breakpoint is refused with a 400, however long the exchange.</summary>
    [Fact]
    public async Task NoRequestCarriesMoreThanFourBreakpoints()
    {
        var history = new List<ConversationMessage> { new(ConversationRole.User, "do the thing") };

        for (var i = 0; i < 40; i++)
        {
            history.Add(new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.ToolUse($"toolu_{i}", "step", "{}")]));

            history.Add(new ConversationMessage(
                ConversationRole.User,
                [new ConversationContent.ToolResult($"toolu_{i}", "done", IsError: false)]));
        }

        var sent = await SentAsync(With(history));

        var blocks = Blocks(sent);

        Assert.Equal(1, sent.GetProperty("system").EnumerateArray().Count(Marked));
        Assert.True(Marked(blocks[^1]), "the end of the history is not marked");
        Assert.Equal(2, blocks[..^1].Count(Marked));
    }

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

        var blocks = Blocks(sent);

        Assert.True(Marked(blocks[^1]), "the end of the history is not marked");
        Assert.DoesNotContain(blocks[..^1], Marked);
    }

    /// <summary>
    /// The end of the history is cached ahead of the live game state, which follows as a system message where
    /// the model takes one and as a reminder block after the question where it does not.
    /// </summary>
    [Theory]
    [InlineData("claude-opus-5", true)]
    [InlineData("claude-sonnet-5", false)]
    public async Task TheEndOfTheHistoryIsMarkedAheadOfTheGameState(string model, bool operatorMessages)
    {
        var sent = await SentAsync(With(
            [
                new ConversationMessage(ConversationRole.User, "where am I?"),
                new ConversationMessage(ConversationRole.Assistant, "Shinrarta Dezhra."),
                new ConversationMessage(ConversationRole.User, "how far is Sol?"),
            ],
            liveGameState: "System: Shinrarta Dezhra",
            model: model));

        var messages = sent.GetProperty("messages");
        var last = messages[messages.GetArrayLength() - 1];
        var blocks = Blocks(sent);

        if (operatorMessages)
        {
            Assert.Equal("system", last.GetProperty("role").GetString());
            Assert.Equal(JsonValueKind.String, last.GetProperty("content").ValueKind);
            Assert.Equal("how far is Sol?", blocks[^1].GetProperty("text").GetString());
            Assert.True(Marked(blocks[^1]), "the question is not marked");
        }
        else
        {
            Assert.Equal("user", last.GetProperty("role").GetString());
            Assert.Equal("how far is Sol?", blocks[^2].GetProperty("text").GetString());
            Assert.True(Marked(blocks[^2]), "the question is not marked");
            Assert.StartsWith("<system-reminder>", blocks[^1].GetProperty("text").GetString(), StringComparison.Ordinal);
        }

        Assert.Single(blocks, Marked);
    }

    /// <summary>A thinking block cannot carry a breakpoint, so the last block that can is marked instead.</summary>
    [Theory]
    [InlineData("""{"type":"thinking","thinking":"x","signature":"y"}""")]
    [InlineData("""{"type":"redacted_thinking","data":"ZW5jcnlwdGVk"}""")]
    public async Task TheBreakpointIsNeverOnAThinkingBlock(string thinking)
    {
        var sent = await SentAsync(With(
        [
            new ConversationMessage(ConversationRole.User, "where am I?"),
            new ConversationMessage(
                ConversationRole.Assistant,
                [new ConversationContent.Text("Shinrarta Dezhra."), new ConversationContent.ThinkingBlock("anthropic", thinking)]),
        ]));

        var content = sent.GetProperty("messages")[1].GetProperty("content");

        Assert.True(Marked(content[0]), "the text before the thinking block is not marked");
        Assert.False(Marked(content[1]), "a breakpoint was put on a thinking block");
    }

    /// <summary>Every content block of every message, in order.</summary>
    private static JsonElement[] Blocks(JsonElement sent) =>
    [
        .. sent.GetProperty("messages")
            .EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array)
            .SelectMany(message => message.GetProperty("content").EnumerateArray()),
    ];

    private static bool Marked(JsonElement block) =>
        block.TryGetProperty("cache_control", out var control) && control.ValueKind == JsonValueKind.Object;

    [Theory]
    [InlineData(ThinkingEffort.Low, "low")]
    [InlineData(ThinkingEffort.Medium, "medium")]
    [InlineData(ThinkingEffort.High, "high")]
    [InlineData(ThinkingEffort.Xhigh, "xhigh")]
    [InlineData(ThinkingEffort.Max, "max")]
    public async Task TheThinkingEffortIsSent(ThinkingEffort effort, string expected)
    {
        var request = Recordings.Request() with { Effort = effort };
        var sent = await SentAsync(request);

        Assert.Equal(expected, sent.GetProperty("output_config").GetProperty("effort").GetString());
    }

    /// <summary>
    /// Thinking is adaptive rather than a token budget: <c>budget_tokens</c> is removed on Opus 5 and
    /// returns a 400.
    /// </summary>
    [Fact]
    public async Task ThinkingIsAdaptiveWithNoTokenBudget()
    {
        var sent = await SentAsync(Recordings.Request());
        var thinking = sent.GetProperty("thinking");

        Assert.Equal("adaptive", thinking.GetProperty("type").GetString());
        Assert.False(thinking.TryGetProperty("budget_tokens", out _));
    }

    /// <summary>Haiku 4.5 predates the 4.6 generation and rejects both fields with a 400.</summary>
    [Fact]
    public async Task NeitherThinkingNorEffortReachesAModelThatRejectsThem()
    {
        var sent = await SentAsync(Recordings.Request("claude-haiku-4-5"));

        Assert.False(sent.TryGetProperty("thinking", out _));
        Assert.False(sent.TryGetProperty("output_config", out _));

        Assert.Equal("claude-haiku-4-5", sent.GetProperty("model").GetString());
        Assert.True(sent.GetProperty("max_tokens").GetInt32() > 0);
    }

    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-opus-5-5")]
    [InlineData("claude-sonnet-5-5")]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-fable-5")]
    public async Task CurrentModelsAreSentBothOfThem(string model)
    {
        var sent = await SentAsync(Recordings.Request(model));

        Assert.Equal("adaptive", sent.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("medium", sent.GetProperty("output_config").GetProperty("effort").GetString());
    }
}
