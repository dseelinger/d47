using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Llm.Tests;

/// <summary>The recorded stream frames these tests are built from, and the request that provokes them.</summary>
internal static class Recordings
{
    private static string Fill(string template, params (string Name, object Value)[] values)
    {
        foreach (var (name, value) in values)
        {
            template = template.Replace(
                $"${name}$",
                value as string ?? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        return template;
    }

    public static string MessageStart(
        string model = "claude-opus-5",
        int input = 25,
        int output = 1,
        int cacheCreation = 0,
        int cacheRead = 0,
        int webSearches = 0) =>
        RecordedEndpoint.Event(
            "message_start",
            Fill(
                """
                {"type":"message_start","message":{"id":"msg_recorded","type":"message","role":"assistant","model":"$MODEL$","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":$IN$,"output_tokens":$OUT$,"cache_creation_input_tokens":$CC$,"cache_read_input_tokens":$CR$,"server_tool_use":{"web_search_requests":$WEB$}}}}
                """,
                ("MODEL", model),
                ("IN", input),
                ("OUT", output),
                ("CC", cacheCreation),
                ("CR", cacheRead),
                ("WEB", webSearches)));

    public static string TextBlockStart(int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_start",
            Fill(
                """{"type":"content_block_start","index":$I$,"content_block":{"type":"text","text":""}}""",
                ("I", index)));

    public static string TextDelta(string text, int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_delta",
            Fill(
                """{"type":"content_block_delta","index":$I$,"delta":{"type":"text_delta","text":$T$}}""",
                ("I", index),
                ("T", JsonSerializer.Serialize(text))));

    public static string ThinkingBlockStart(int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_start",
            Fill(
                """{"type":"content_block_start","index":$I$,"content_block":{"type":"thinking","thinking":"","signature":""}}""",
                ("I", index)));

    public static string ThinkingDelta(string text, int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_delta",
            Fill(
                """{"type":"content_block_delta","index":$I$,"delta":{"type":"thinking_delta","thinking":$T$}}""",
                ("I", index),
                ("T", JsonSerializer.Serialize(text))));

    public static string ToolUseBlockStart(string id, string name, int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_start",
            Fill(
                """{"type":"content_block_start","index":$I$,"content_block":{"type":"tool_use","id":"$ID$","name":"$NAME$","input":{}}}""",
                ("I", index),
                ("ID", id),
                ("NAME", name)));

    /// <summary>One fragment of a tool call's arguments.</summary>
    public static string InputJsonDelta(string partial, int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_delta",
            Fill(
                """{"type":"content_block_delta","index":$I$,"delta":{"type":"input_json_delta","partial_json":$P$}}""",
                ("I", index),
                ("P", JsonSerializer.Serialize(partial))));

    public static string BlockStop(int index = 0) =>
        RecordedEndpoint.Event(
            "content_block_stop",
            Fill("""{"type":"content_block_stop","index":$I$}""", ("I", index)));

    public static string MessageDelta(string? stopReason, int output = 15, int webSearches = 0) =>
        RecordedEndpoint.Event(
            "message_delta",
            Fill(
                """{"type":"message_delta","delta":{"stop_reason":$STOP$,"stop_sequence":null},"usage":{"output_tokens":$OUT$,"server_tool_use":{"web_search_requests":$WEB$}}}""",
                ("STOP", stopReason is null ? "null" : JsonSerializer.Serialize(stopReason)),
                ("OUT", output),
                ("WEB", webSearches)));

    public static string MessageStop() =>
        RecordedEndpoint.Event("message_stop", """{"type":"message_stop"}""");

    /// <summary>The shortest complete turn: one word and a clean stop.</summary>
    public static string[] OneWord(string word = "Acknowledged") =>
    [
        MessageStart(),
        TextBlockStart(),
        TextDelta(word),
        BlockStop(),
        MessageDelta("end_turn"),
        MessageStop(),
    ];

    /// <summary>An ordinary request, for driving the endpoint.</summary>
    public static LlmRequest Request(string model = "claude-opus-5", string ask = "status?") => new()
    {
        Model = model,
        Effort = ThinkingEffort.Medium,
        Sampling = LlmSampling.Conversation,
        Prompt = new PromptAssembly
        {
            History = [new ConversationMessage(ConversationRole.User, ask)],
        },
    };

    /// <summary>Everything the provider emitted, drained.</summary>
    public static async Task<List<LlmStreamEvent>> DrainAsync(
        RecordedEndpoint endpoint,
        LlmRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl);
        var events = new List<LlmStreamEvent>();

        await foreach (var step in provider.StreamAsync(request ?? Request(), cancellationToken))
        {
            events.Add(step);
        }

        return events;
    }
}
