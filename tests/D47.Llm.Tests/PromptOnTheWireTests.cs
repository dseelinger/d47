using System.Text.Json;
using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

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

    [Fact]
    public async Task ATextOnlyTurnIsAPlainStringRatherThanABlockList()
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "fuel?")]));

        var content = sent.GetProperty("messages")[0].GetProperty("content");

        Assert.Equal(JsonValueKind.String, content.ValueKind);
        Assert.Equal("fuel?", content.GetString());
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

    /// <summary>Where it cannot, it folds into the last user turn as a <c>&lt;system-reminder&gt;</c>.</summary>
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

        Assert.EndsWith("\n\nwhere am I?", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude-opus-5")]
    [InlineData("claude-sonnet-5")]
    public async Task NoLiveGameStateAddsNothing(string model)
    {
        var sent = await SentAsync(With([new ConversationMessage(ConversationRole.User, "hello")], model: model));

        Assert.Equal(1, sent.GetProperty("messages").GetArrayLength());
        Assert.Equal("hello", sent.GetProperty("messages")[0].GetProperty("content").GetString());
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

        var marked = sent.GetProperty("messages")
            .EnumerateArray()
            .Where(message => message.GetProperty("content").ValueKind == JsonValueKind.Array)
            .SelectMany(message => message.GetProperty("content").EnumerateArray())
            .Count(block => block.TryGetProperty("cache_control", out var control)
                            && control.ValueKind == JsonValueKind.Object);

        Assert.InRange(marked, 1, 3);
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

        var block = sent.GetProperty("messages")[1].GetProperty("content")[0];

        Assert.True(
            !block.TryGetProperty("cache_control", out var control) || control.ValueKind == JsonValueKind.Null,
            $"a breakpoint was spent on a two-block exchange: {block}");
    }

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
    [InlineData("claude-opus-4-8")]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-fable-5")]
    public async Task CurrentModelsAreSentBothOfThem(string model)
    {
        var sent = await SentAsync(Recordings.Request(model));

        Assert.Equal("adaptive", sent.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("medium", sent.GetProperty("output_config").GetProperty("effort").GetString());
    }
}
