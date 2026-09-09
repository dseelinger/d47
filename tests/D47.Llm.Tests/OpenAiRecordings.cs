using D47.Core.Conversation;

namespace D47.Llm.Tests;

/// <summary>Recorded frames for the two OpenAI-shaped protocols, and the plumbing to drive them.</summary>
internal static class OpenAiRecordings
{
    /// <summary>
    /// One SSE frame with no <c>event:</c> line, which is how both of these protocols send everything —
    /// the type is inside the payload, not in the field beside it.
    /// </summary>
    public static string Data(string json) => "data: " + json + "\n\n";

    public static string Done() => "data: [DONE]\n\n";

    /// <summary>A prompt with nothing in it but one question, the smallest real turn.</summary>
    public static LlmRequest Ask(
        string model = "test-model",
        string question = "How much fuel?",
        LlmSampling? sampling = null) => new()
    {
        Model = model,
        Prompt = new PromptAssembly
        {
            History = [new ConversationMessage(ConversationRole.User, question)],
        },
        Effort = ThinkingEffort.Low,

        Sampling = sampling ?? LlmSampling.Conversation,
    };

    public static async Task<List<LlmStreamEvent>> DrainAsync(
        ILlmProvider provider,
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        var events = new List<LlmStreamEvent>();

        await foreach (var step in provider.StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            events.Add(step);
        }

        return events;
    }

    /// <summary>The error body an OpenAI-shaped server returns.</summary>
    public static string Refusal(string message, string? param) =>
        "{\"error\":{\"message\":" + Quote(message) + ",\"type\":\"invalid_request_error\""
        + (param is null ? string.Empty : ",\"param\":" + Quote(param))
        + "}}";

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    /// <summary>Chat Completions, as Ollama, LM Studio, vLLM and llama.cpp all speak it.</summary>
    internal static class Chat
    {
        public static string TextDelta(string text) =>
            Delta("{\"content\":" + Quote(text) + "}");

        public static string ReasoningDelta(string text) =>
            Delta("{\"reasoning_content\":" + Quote(text) + "}");

        public static string Finish(string reason) =>
            Data("{\"id\":\"c\",\"object\":\"chat.completion.chunk\",\"choices\":[{\"index\":0,\"delta\":{},"
                 + "\"finish_reason\":" + Quote(reason) + "}]}");

        /// <summary>The first fragment of a tool call: the one carrying the id and the name.</summary>
        public static string ToolCallStart(int index, string id, string name) =>
            Delta("{\"tool_calls\":[{\"index\":" + index + ",\"id\":" + Quote(id) + ",\"type\":\"function\","
                  + "\"function\":{\"name\":" + Quote(name) + ",\"arguments\":\"\"}}]}");

        /// <summary>A later fragment, naming only the index — no id, as several servers send it.</summary>
        public static string ToolCallArguments(int index, string fragment) =>
            Delta("{\"tool_calls\":[{\"index\":" + index + ","
                  + "\"function\":{\"arguments\":" + Quote(fragment) + "}}]}");

        /// <summary>
        /// The final chunk, which only exists because <c>stream_options.include_usage</c> asked for it.
        /// </summary>
        public static string Usage(int prompt, int cached, int completion) =>
            Data("{\"id\":\"c\",\"choices\":[],\"usage\":{"
                 + "\"prompt_tokens\":" + prompt + ",\"completion_tokens\":" + completion + ","
                 + "\"total_tokens\":" + (prompt + completion) + ","
                 + "\"prompt_tokens_details\":{\"cached_tokens\":" + cached + "}}}");

        private static string Delta(string delta) =>
            Data("{\"id\":\"c\",\"object\":\"chat.completion.chunk\","
                 + "\"choices\":[{\"index\":0,\"delta\":" + delta + "}]}");
    }

    /// <summary>Responses, as OpenAI and xAI speak it.</summary>
    internal static class Responses
    {
        public static string TextDelta(string text) =>
            Data("{\"type\":\"response.output_text.delta\",\"item_id\":\"m1\",\"delta\":" + Quote(text) + "}");

        public static string ReasoningDelta(string text) =>
            Data("{\"type\":\"response.reasoning_summary_text.delta\",\"item_id\":\"r1\",\"delta\":"
                 + Quote(text) + "}");

        /// <summary>
        /// Where a tool call's name and call_id arrive — before any of its arguments, and under a
        /// different identifier from the one the argument deltas will quote.
        /// </summary>
        public static string ToolCallAdded(string itemId, string callId, string name) =>
            Data("{\"type\":\"response.output_item.added\",\"output_index\":0,\"item\":{"
                 + "\"id\":" + Quote(itemId) + ",\"type\":\"function_call\","
                 + "\"call_id\":" + Quote(callId) + ",\"name\":" + Quote(name) + ",\"arguments\":\"\"}}");

        public static string ToolCallArguments(string itemId, string fragment) =>
            Data("{\"type\":\"response.function_call_arguments.delta\",\"item_id\":" + Quote(itemId)
                 + ",\"delta\":" + Quote(fragment) + "}");

        public static string ToolCallDone(string itemId, string? arguments) =>
            Data("{\"type\":\"response.function_call_arguments.done\",\"item_id\":" + Quote(itemId)
                 + (arguments is null ? string.Empty : ",\"arguments\":" + Quote(arguments)) + "}");

        /// <summary>
        /// A search the endpoint ran on the model's behalf. d47 never sees its result — the answer
        /// arrives as prose in the same turn — but it is billed separately from tokens, so it has to be
        /// counted.
        /// </summary>
        public static string WebSearchCall(string itemId) =>
            Data("{\"type\":\"response.output_item.added\",\"output_index\":0,\"item\":{"
                 + "\"id\":" + Quote(itemId) + ",\"type\":\"web_search_call\",\"status\":\"completed\"}}");

        /// <summary>The end of a turn, carrying usage on the response object.</summary>
        public static string Completed(int input, int cached, int written, int output) =>
            Data("{\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\",\"status\":\"completed\","
                 + "\"usage\":{\"input_tokens\":" + input + ",\"input_tokens_details\":{"
                 + "\"cached_tokens\":" + cached + ",\"cache_write_tokens\":" + written + "},"
                 + "\"output_tokens\":" + output + ",\"output_tokens_details\":{\"reasoning_tokens\":0},"
                 + "\"total_tokens\":" + (input + output) + "}}}");

        public static string Incomplete(string reason) =>
            Data("{\"type\":\"response.incomplete\",\"response\":{\"id\":\"resp_1\",\"status\":\"incomplete\","
                 + "\"incomplete_details\":{\"reason\":" + Quote(reason) + "},"
                 + "\"usage\":{\"input_tokens\":10,\"input_tokens_details\":{\"cached_tokens\":0},"
                 + "\"output_tokens\":5}}}");

        public static string Failed(string message) =>
            Data("{\"type\":\"response.failed\",\"response\":{\"id\":\"resp_1\",\"status\":\"failed\","
                 + "\"error\":{\"code\":\"server_error\",\"message\":" + Quote(message) + "}}}");
    }
}
