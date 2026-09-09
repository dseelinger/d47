using System.Buffers;
using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Llm.OpenAi;

/// <summary>
/// OpenAI and its protocol peers, over the Responses API (Phase 29, "A turn answered by OpenAI").
/// </summary>
public sealed class ResponsesLlmProvider : ILlmProvider, IDisposable
{
    /// <summary>
    /// The ceiling on searches in one turn, for the two reasons the Anthropic path gives: a search is
    /// billed at a penny and a model told to research freely will spend ten of them on one question,
    /// and a turn that cannot search much has little opportunity to run long enough to be cut short.
    /// </summary>
    private const int MaxWebSearchesPerTurn = 3;

    private readonly OpenAiEndpoint _endpoint;
    private readonly bool _ownEndpoint;

    public ResponsesLlmProvider(string? apiKey, string? endpoint, HttpClient? http = null)
    {
        var address = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;

        // Whether this is OpenAI's own address rather than a gateway.
        _ownEndpoint = string.Equals(
            OpenAiEndpoint.Normalise(address),
            DefaultEndpoint,
            StringComparison.OrdinalIgnoreCase);

        _endpoint = new OpenAiEndpoint(address, apiKey, http);
    }

    private const string DefaultEndpoint = "https://api.openai.com/v1";

    public string Id => LlmProviderCatalog.OpenAiId;

    public string DisplayName => "OpenAI";

    public string DefaultModel => "gpt-5.6-terra";

    public bool RunsOnThisMachine => _endpoint.IsLoopback;

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = true,
        SupportsThinkingEffort = EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ReasoningEffort),
        SupportsOperatorSystemMessages = true,
        MinimumCacheablePrefixTokens = 1024,
        SupportsToolCalls = EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.Tools),
        SupportsWebSearch = _ownEndpoint,
    };

    /// <summary>The models this endpoint says it serves, or nothing if it will not say.</summary>
    public Task<EndpointModels> ListModelsAsync(CancellationToken cancellationToken) =>
        EndpointHandshake.ListModelsAsync(_endpoint, cancellationToken);

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage? response = null;

        // At most two attempts, and the second only ever happens because the endpoint named a field it would
        // not accept.
        for (var attempt = 0; ; attempt++)
        {
            var sent = await SendAsync("/responses", BuildBody(request), cancellationToken).ConfigureAwait(false);

            if (sent.Refusal is { } rejected && attempt == 0 && EndpointDemotions.Demote(_endpoint.BaseUrl, rejected))
            {
                continue;
            }

            if (sent.Failure is { } failure)
            {
                yield return failure;
                yield break;
            }

            response = sent.Response;
            break;
        }

        using (response)
        {
            await foreach (var step in DecodeAsync(response!, cancellationToken).ConfigureAwait(false))
            {
                yield return step;
            }
        }
    }

    /// <summary>What one attempt at sending produced.</summary>
    private sealed record Attempt(
        HttpResponseMessage? Response,
        LlmStreamEvent.Failed? Failure,
        Demotable? Refusal);

    private async Task<Attempt> SendAsync(string path, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            using var message = _endpoint.Post(path, body);
            response = await _endpoint.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Attempt(null, new LlmStreamEvent.Failed(_endpoint.DescribeUnreachable(ex), Transient: true), null);
        }

        if (response.IsSuccessStatusCode)
        {
            return new Attempt(response, null, null);
        }

        using (response)
        {
            var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);
            var (message, transient) = _endpoint.Describe(response.StatusCode, detail);

            return new Attempt(
                null,
                new LlmStreamEvent.Failed(message, transient),
                ChatCompletionsLlmProvider.WhatWasRejected(detail));
        }
    }

    /// <summary>The stream, turned into seam events.</summary>
    private async IAsyncEnumerable<LlmStreamEvent> DecodeAsync(
        HttpResponseMessage response,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var building = new Dictionary<string, PendingCall>(StringComparer.Ordinal);
        var usage = LlmUsage.Unreported;
        var stopReason = LlmStopReason.Completed;
        var searches = 0;

        await foreach (var payload in ServerSentEvents.ReadAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            JsonElement frame;

            try
            {
                using var document = JsonDocument.Parse(payload);
                frame = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame.ValueKind != JsonValueKind.Object || Text(frame, "type") is not { } type)
            {
                continue;
            }

            switch (type)
            {
                case "response.output_text.delta":
                    if (Text(frame, "delta") is { Length: > 0 } text)
                    {
                        yield return new LlmStreamEvent.TextDelta(text);
                    }

                    break;

                // Summarised reasoning, where the model produces any.
                case "response.reasoning_summary_text.delta":
                case "response.reasoning_text.delta":
                    if (Text(frame, "delta") is { Length: > 0 } thinking)
                    {
                        yield return new LlmStreamEvent.ThinkingDelta(thinking);
                    }

                    break;

                case "response.output_item.added":
                    if (frame.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
                    {
                        var kind = Text(item, "type");

                        if (kind == "function_call" && Text(item, "id") is { Length: > 0 } itemId)
                        {
                            building[itemId] = new PendingCall
                            {
                                // call_id is what the answer must quote; id is what the argument deltas name.
                                CallId = Text(item, "call_id") ?? itemId,
                                Name = Text(item, "name"),
                            };
                        }

                        // A server-side search, which d47 never sees the result of — the answer arrives as
                        // prose in the same turn.
                        if (kind == "web_search_call")
                        {
                            searches++;
                        }
                    }

                    break;

                case "response.function_call_arguments.delta":
                    if (Text(frame, "item_id") is { } deltaFor
                        && building.TryGetValue(deltaFor, out var pending)
                        && Text(frame, "delta") is { Length: > 0 } fragment)
                    {
                        pending.Arguments.Append(fragment);
                    }

                    break;

                case "response.function_call_arguments.done":
                    if (Text(frame, "item_id") is { } doneFor
                        && building.Remove(doneFor, out var finished)
                        && finished.Name is { Length: > 0 } name)
                    {
                        // The done event carries the whole argument string as well, which is the
                        // authoritative copy — the deltas are believed only when it is absent.
                        var arguments = Text(frame, "arguments") is { Length: > 0 } whole
                            ? whole
                            : finished.Arguments.Length == 0 ? "{}" : finished.Arguments.ToString();

                        stopReason = LlmStopReason.ToolUse;

                        yield return new LlmStreamEvent.ToolUse(finished.CallId, name, arguments);
                    }

                    break;

                case "response.completed":
                case "response.incomplete":
                    if (frame.TryGetProperty("response", out var completed)
                        && completed.ValueKind == JsonValueKind.Object)
                    {
                        if (Usage(completed) is { } reported)
                        {
                            usage = reported with { WebSearchRequests = searches };
                        }

                        // A turn cut short reports why in incomplete_details.reason.
                        if (completed.TryGetProperty("incomplete_details", out var incomplete)
                            && incomplete.ValueKind == JsonValueKind.Object
                            && Text(incomplete, "reason") is { } reason)
                        {
                            stopReason = reason switch
                            {
                                "max_output_tokens" => LlmStopReason.MaxTokens,
                                "content_filter" => LlmStopReason.Refusal,
                                _ => LlmStopReason.Paused,
                            };
                        }
                    }

                    break;

                case "response.refusal.done":
                    stopReason = LlmStopReason.Refusal;
                    break;

                case "response.failed":
                case "error":
                    yield return new LlmStreamEvent.Failed(
                        FailureText(frame) ?? "The endpoint reported an error mid-stream.",
                        Transient: false);

                    yield break;
            }
        }

        // Calls the endpoint opened and never closed.
        foreach (var (_, orphan) in building)
        {
            if (orphan.Name is { Length: > 0 } orphanName)
            {
                stopReason = LlmStopReason.ToolUse;

                yield return new LlmStreamEvent.ToolUse(
                    orphan.CallId,
                    orphanName,
                    orphan.Arguments.Length == 0 ? "{}" : orphan.Arguments.ToString());
            }
        }

        yield return new LlmStreamEvent.Completed(
            usage.Reported ? usage with { WebSearchRequests = searches } : usage,
            stopReason);
    }

    /// <summary>The request body, in a fixed key order, with the tool schemas copied through verbatim.</summary>
    internal ReadOnlyMemory<byte> BuildBody(LlmRequest request)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("model", request.Model);

            // Positions 2 through 4 — guardrails, persona, About Me — in the order the assembly fixed, and in
            // the field this protocol caches from the front of.
            json.WriteString("instructions", request.Prompt.RenderCachedSystemBlock());

            json.WriteStartArray("input");

            var turns = OpenAiPrompt.Flatten(request.Prompt, operatorRoleAvailable: true, out var trailingState);

            foreach (var turn in turns)
            {
                WriteTurn(json, turn);
            }

            if (trailingState is { Length: > 0 })
            {
                // Below everything cached, so attaching it costs no prefix — and under the role that carries
                // operator authority, which is what stops journal content being able to forge it.
                json.WriteStartObject();
                json.WriteString("role", "developer");
                json.WriteString("content", trailingState);
                json.WriteEndObject();
            }

            json.WriteEndArray();

            json.WriteNumber("max_output_tokens", request.MaxOutputTokens);

            if (EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ReasoningEffort))
            {
                json.WriteStartObject("reasoning");
                json.WriteString("effort", Translate(request.Effort));

                // Summarised rather than hidden.
                json.WriteString("summary", "auto");
                json.WriteEndObject();
            }

            // What the call class asked for (#98), on the same terms the Chat Completions path states them.
            if (request.Sampling.Temperature is { } temperature
                && EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.Sampling))
            {
                json.WriteNumber("temperature", temperature);
            }

            json.WriteBoolean("store", false);
            json.WriteBoolean("stream", true);

            var tools = request.Prompt.Tools.Count > 0
                        && EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.Tools);

            if (tools || request.WebSearch)
            {
                json.WriteStartArray("tools");

                if (tools)
                {
                    foreach (var tool in request.Prompt.Tools)
                    {
                        json.WriteStartObject();
                        json.WriteString("type", "function");
                        json.WriteString("name", tool.Name);
                        json.WriteString("description", tool.Description);
                        json.WritePropertyName("parameters");
                        json.WriteRawValue(tool.InputSchemaJson, skipInputValidation: true);
                        json.WriteEndObject();
                    }
                }

                // Appended after the registered tools rather than mixed in, so turning it on leaves every
                // byte of the existing advertisement where it was.
                if (request.WebSearch)
                {
                    json.WriteStartObject();
                    json.WriteString("type", "web_search");
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            }

            if (request.WebSearch)
            {
                json.WriteNumber("max_tool_calls", MaxWebSearchesPerTurn);
            }

            json.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    private static void WriteTurn(Utf8JsonWriter json, WireTurn turn)
    {
        // Results first, for the same reason as everywhere else: each answers a call above it.
        foreach (var result in turn.Results)
        {
            json.WriteStartObject();
            json.WriteString("type", "function_call_output");
            json.WriteString("call_id", result.ToolUseId);
            json.WriteString("output", result.IsError ? $"ERROR: {result.Content}" : result.Content);
            json.WriteEndObject();
        }

        if (turn.Text is { } text)
        {
            json.WriteStartObject();
            json.WriteString("role", turn.IsAssistant ? "assistant" : "user");
            json.WriteString("content", text);
            json.WriteEndObject();
        }

        foreach (var call in turn.Calls)
        {
            json.WriteStartObject();
            json.WriteString("type", "function_call");
            json.WriteString("call_id", call.Id);
            json.WriteString("name", call.Name);
            json.WriteString("arguments", call.InputJson);
            json.WriteEndObject();
        }
    }

    /// <summary>Usage off a completed response.</summary>
    private static LlmUsage? Usage(JsonElement response)
    {
        if (!response.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var cached = 0;
        var written = 0;

        if (usage.TryGetProperty("input_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            cached = Number(details, "cached_tokens");

            // Reported from GPT-5.6 on, and billed at 1.25x uncached input.
            written = Number(details, "cache_write_tokens");
        }

        return LlmUsage.FromInclusiveInput(
            Number(usage, "input_tokens"),
            cached,
            written,
            Number(usage, "output_tokens"));
    }

    private static string? FailureText(JsonElement frame)
    {
        if (Text(frame, "message") is { Length: > 0 } direct)
        {
            return direct;
        }

        if (frame.TryGetProperty("response", out var response)
            && response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("error", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            return Text(nested, "message");
        }

        return frame.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
            ? Text(error, "message")
            : null;
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string raw;

        try
        {
            raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                var message = Text(error, "message");
                var param = Text(error, "param");

                return param is { Length: > 0 } && message is { Length: > 0 }
                    ? $"{message} ({param})"
                    : message ?? param;
            }
        }
        catch (JsonException)
        {
        // Not JSON.
        }

        return raw.Length > 400 ? raw[..400] : raw;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static string Translate(ThinkingEffort effort) => effort switch
    {
        ThinkingEffort.Low => "low",
        ThinkingEffort.Medium => "medium",
        ThinkingEffort.High => "high",

        // Both map down rather than through.
        ThinkingEffort.Xhigh => "high",
        ThinkingEffort.Max => "high",
        _ => "medium",
    };

    private sealed class PendingCall
    {
        public required string CallId { get; init; }

        public string? Name { get; init; }

        public System.Text.StringBuilder Arguments { get; } = new();
    }

    public void Dispose() => _endpoint.Dispose();
}
