using System.Buffers;
using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Llm.OpenAi;

/// <summary>
/// A model on a machine the Commander controls, over Chat Completions (list.md Phase 29,
/// "A turn answered by a machine you own").
/// <para>
/// <b>Chat Completions is where every local server lives.</b> Ollama, LM Studio, vLLM and
/// llama.cpp all speak it and none of them speaks Responses; none of them has a server-side web
/// search either, which is what makes the protocol split land exactly on the catalog split
/// rather than being a preference. The hosted vendors keep search on Responses, and
/// <see cref="ResponsesLlmProvider"/> is where that lives.
/// </para>
/// <para>
/// Everything here is written to be <em>tolerant</em>. An implementation that gets a detail
/// wrong is the expected case rather than the exceptional one, so optional fields are advertised
/// and then dropped when refused (<see cref="EndpointDemotions"/>), an unrecognised stream event
/// is skipped rather than fatal, and a missing usage block leaves the turn unpriced rather than
/// priced at nothing.
/// </para>
/// </summary>
public sealed class ChatCompletionsLlmProvider : ILlmProvider, IDisposable
{
    private readonly OpenAiEndpoint _endpoint;

    /// <summary>
    /// <paramref name="apiKey"/> may be null, and that is the point of this provider rather than
    /// an oversight: a model running on this machine has no account to get a key from. A gateway
    /// speaking the same protocol may still want one, which is why the row exists at all.
    /// </summary>
    public ChatCompletionsLlmProvider(string? apiKey, string? endpoint, HttpClient? http = null)
        => _endpoint = new OpenAiEndpoint(
            string.IsNullOrWhiteSpace(endpoint) ? LlmProviderCatalog.CompatibleDefaultEndpoint : endpoint,
            apiKey,
            http);

    public string Id => LlmProviderCatalog.OpenAiCompatibleId;

    public string DisplayName => "OpenAI-compatible endpoint";

    /// <summary>
    /// Empty, deliberately, and the only provider for which that is right. There is no model this
    /// endpoint is known to serve — it serves whatever was loaded — so a default would be a guess
    /// that fails at the first turn. The handshake asks the endpoint instead.
    /// </summary>
    public string DefaultModel => string.Empty;

    public bool RunsOnThisMachine => _endpoint.IsLoopback;

    /// <summary>
    /// What is offered before anything has been refused. Optimistic on purpose: the flags are an
    /// advertisement, and <see cref="EndpointDemotions"/> turns each one off for this address the
    /// first time the endpoint says no.
    /// <para>
    /// Prompt caching is the exception and is off. It is not a request field a server can refuse
    /// — it is automatic where it exists and absent where it does not — so there is nothing to
    /// advertise and nothing to demote, and claiming it would make the cold-prefix detector
    /// report on an endpoint that has no cache to be cold.
    /// </para>
    /// <para>
    /// Operator system messages are off too, and that one is a judgement rather than a fact. A
    /// trailing system message is legal here and OpenAI honours it; a great many compatible
    /// servers hoist every system message to the front, merge them, or drop all but the first —
    /// silently, in every case. The <c>&lt;system-reminder&gt;</c> fallback caches identically and
    /// is merely a weaker trust signal, which is a much better trade than game state arriving
    /// somewhere other than where it was put.
    /// </para>
    /// </summary>
    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = false,
        SupportsThinkingEffort = EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ReasoningEffort),
        SupportsOperatorSystemMessages = false,
        MinimumCacheablePrefixTokens = int.MaxValue,
        SupportsToolCalls = EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.Tools),

        // No local server has one, and this protocol cannot reach the hosted ones' — xAI moved
        // its search tools to Responses and deprecated the Chat Completions parameter that used
        // to carry them. Off is the truthful answer rather than a limitation of this decoder.
        SupportsWebSearch = false,
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

        // At most two attempts, and the second only ever happens because the endpoint named a
        // field it would not accept. Not a retry policy: nothing here sends the same request
        // twice, and a rejection that has already been answered once is simply a failure.
        for (var attempt = 0; ; attempt++)
        {
            var sent = await SendAsync("/chat/completions", BuildBody(request), cancellationToken).ConfigureAwait(false);

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
            // Straight through from here. Nothing is held back, because the decision that could
            // have needed the events back was made above, before the body was touched.
            await foreach (var step in DecodeAsync(response!, cancellationToken).ConfigureAwait(false))
            {
                yield return step;
            }
        }
    }

    /// <summary>
    /// What one attempt at sending produced: a live response to decode, a failure to report, or a
    /// refusal naming the field the endpoint would not accept.
    /// <para>
    /// Separated from the decoding on purpose, and it is the whole reason this type is shaped this
    /// way. <b>The retry decision has to be made before a single token is yielded</b>, or the
    /// alternative is buffering the turn to keep the option open — which would defeat the thing
    /// streaming exists for, since the largest perceived-latency win in d47 is speech starting at
    /// the first sentence boundary (architecture.md §6). A rejection can only arrive on the
    /// response headers, so there is a clean point to decide at, and this is it.
    /// </para>
    /// </summary>
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
            // Nothing answered, so nothing has an opinion about the request. Always transient.
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
                WhatWasRejected(detail));
        }
    }

    /// <summary>
    /// The stream, turned into seam events.
    /// <para>
    /// A tool call arrives in pieces here exactly as it does on the Anthropic path, and for the
    /// same reason is assembled whole before it is emitted: the arguments are JSON fragments that
    /// mean nothing until the last one lands, and running a tool on half an argument object is
    /// the one mistake this must not make possible. The pieces are keyed by
    /// <c>tool_calls[].index</c> rather than by id, because the id arrives on the first fragment
    /// and several servers omit it from the rest.
    /// </para>
    /// </summary>
    private async IAsyncEnumerable<LlmStreamEvent> DecodeAsync(
        HttpResponseMessage response,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var building = new SortedDictionary<int, PendingCall>();
        var usage = LlmUsage.Unreported;
        var stopReason = LlmStopReason.Completed;
        var sawToolCall = false;

        await foreach (var payload in ServerSentEvents.ReadAsync(stream, cancellationToken).ConfigureAwait(false))
        {
            JsonElement chunk;

            try
            {
                using var document = JsonDocument.Parse(payload);
                chunk = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // A frame that is not JSON. Skipped rather than fatal: it cost nothing, and a
                // server that emits a stray keep-alive should not end a turn that is going fine.
                continue;
            }

            if (chunk.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // An error delivered inside the stream rather than as a status code, which several
            // servers do once they have already sent 200 and started the body.
            if (chunk.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                yield return new LlmStreamEvent.Failed(
                    Text(error, "message") ?? "The endpoint reported an error mid-stream.",
                    Transient: false);

                yield break;
            }

            if (Usage(chunk) is { } reported)
            {
                usage = reported;
            }

            if (!chunk.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var choice in choices.EnumerateArray())
            {
                if (Text(choice, "finish_reason") is { } finish)
                {
                    stopReason = Translate(finish);
                }

                if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (Text(delta, "content") is { Length: > 0 } content)
                {
                    yield return new LlmStreamEvent.TextDelta(content);
                }

                // Not a field OpenAI sends, and one several local servers do — vLLM and Ollama
                // both surface a reasoning model's visible thinking here. Read because it costs
                // one lookup and the panel already has somewhere to put it.
                if (Text(delta, "reasoning_content") is { Length: > 0 } reasoning)
                {
                    yield return new LlmStreamEvent.ThinkingDelta(reasoning);
                }

                if (!delta.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                sawToolCall = true;

                foreach (var call in calls.EnumerateArray())
                {
                    var index = call.TryGetProperty("index", out var at) && at.TryGetInt32(out var i) ? i : 0;

                    if (!building.TryGetValue(index, out var pending))
                    {
                        pending = new PendingCall();
                        building[index] = pending;
                    }

                    pending.Id ??= Text(call, "id");

                    if (call.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.Object)
                    {
                        pending.Name ??= Text(function, "name");

                        if (Text(function, "arguments") is { Length: > 0 } fragment)
                        {
                            pending.Arguments.Append(fragment);
                        }
                    }
                }
            }
        }

        foreach (var (index, pending) in building)
        {
            if (pending.Name is not { Length: > 0 } name)
            {
                continue;
            }

            // Empty input is "{}", not "". A tool with no parameters produces no argument
            // fragments at all, and an empty string is not a JSON object — the argument parser
            // would read it as malformed and drop a call that was fine.
            var arguments = pending.Arguments.Length == 0 ? "{}" : pending.Arguments.ToString();

            // An id is what the answer will be matched on, and a server that streams tool calls
            // without one is common enough to be worth surviving. The index is stable within a
            // turn, so it makes a usable id — and it is the same id the result will quote.
            yield return new LlmStreamEvent.ToolUse(pending.Id ?? $"call_{index}", name, arguments);
        }

        // A server that asked for a tool but never sent a finish_reason. Reporting that as a
        // finished answer strands the calls: the turn loop only runs them when it is told the
        // model stopped for them.
        if (sawToolCall && stopReason == LlmStopReason.Completed)
        {
            stopReason = LlmStopReason.ToolUse;
        }

        yield return new LlmStreamEvent.Completed(usage, stopReason);
    }

    /// <summary>
    /// The request body, written a byte at a time in a fixed key order.
    /// <para>
    /// <b>The tool schema is copied through verbatim.</b> It arrives already canonicalised by
    /// <c>ToolSchemaWriter</c> and is written with <c>WriteRawValue</c> rather than parsed and
    /// re-emitted — a second serializer with its own opinion about key order is exactly the
    /// non-deterministic serialisation that breaks prompt caching, and it breaks it here for the
    /// same reason it would on the Anthropic path. Chat Completions nests the schema one level
    /// deeper, under <c>function</c>; the bytes inside are the same bytes.
    /// </para>
    /// </summary>
    internal ReadOnlyMemory<byte> BuildBody(LlmRequest request)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("model", request.Model);

            json.WriteStartArray("messages");

            // Positions 2 through 4 — guardrails, persona, About Me — as one system message, in
            // the order the assembly fixed. Everything above the breakpoint, and nothing else.
            json.WriteStartObject();
            json.WriteString("role", "system");
            json.WriteString("content", request.Prompt.RenderCachedSystemBlock());
            json.WriteEndObject();

            foreach (var turn in OpenAiPrompt.Flatten(request.Prompt, operatorRoleAvailable: false, out _))
            {
                WriteTurn(json, turn);
            }

            json.WriteEndArray();

            if (EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ModernTokenLimit))
            {
                json.WriteNumber("max_completion_tokens", request.MaxOutputTokens);
            }
            else
            {
                json.WriteNumber("max_tokens", request.MaxOutputTokens);
            }

            if (EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.ReasoningEffort))
            {
                json.WriteString("reasoning_effort", Translate(request.Effort));
            }

            json.WriteBoolean("stream", true);

            if (EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.StreamUsage))
            {
                // Without this there is no usage block at all, and a turn priced at zero reports
                // a paid session as free. Asked for every time; when the endpoint refuses the
                // field the turn goes unpriced instead, which is the honest side of that trade.
                json.WriteStartObject("stream_options");
                json.WriteBoolean("include_usage", true);
                json.WriteEndObject();
            }

            if (request.Prompt.Tools.Count > 0 && EndpointDemotions.Allows(_endpoint.BaseUrl, Demotable.Tools))
            {
                json.WriteStartArray("tools");

                foreach (var tool in request.Prompt.Tools)
                {
                    json.WriteStartObject();
                    json.WriteString("type", "function");
                    json.WriteStartObject("function");
                    json.WriteString("name", tool.Name);
                    json.WriteString("description", tool.Description);
                    json.WritePropertyName("parameters");
                    json.WriteRawValue(tool.InputSchemaJson, skipInputValidation: true);
                    json.WriteEndObject();
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            }

            json.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    private static void WriteTurn(Utf8JsonWriter json, WireTurn turn)
    {
        // Tool results are messages of their own here rather than blocks inside the next user
        // turn, and they have to come first: each answers a call in the assistant message above
        // it, and anything in between is a protocol error.
        foreach (var result in turn.Results)
        {
            json.WriteStartObject();
            json.WriteString("role", "tool");
            json.WriteString("tool_call_id", result.ToolUseId);

            // There is no is_error flag in this protocol. The failure goes in the content where
            // the model will read it, rather than being dropped for want of a field — a tool that
            // failed silently is a tool the model will report as having worked.
            json.WriteString("content", result.IsError ? $"ERROR: {result.Content}" : result.Content);
            json.WriteEndObject();
        }

        if (turn.Text is null && turn.Calls.Count == 0)
        {
            return;
        }

        json.WriteStartObject();
        json.WriteString("role", turn.IsAssistant ? "assistant" : "user");

        // Null rather than omitted when an assistant turn is only tool calls. The field is
        // required on that role, and servers that validate reject a message without one.
        if (turn.Text is { } text)
        {
            json.WriteString("content", text);
        }
        else
        {
            json.WriteNull("content");
        }

        if (turn.Calls.Count > 0)
        {
            json.WriteStartArray("tool_calls");

            foreach (var call in turn.Calls)
            {
                json.WriteStartObject();
                json.WriteString("id", call.Id);
                json.WriteString("type", "function");
                json.WriteStartObject("function");
                json.WriteString("name", call.Name);

                // The arguments are a JSON *string* containing JSON, which is this protocol's one
                // genuinely awkward shape and the reason d47 carries them as text end to end.
                json.WriteString("arguments", call.InputJson);
                json.WriteEndObject();
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        json.WriteEndObject();
    }

    /// <summary>
    /// Which optional field the endpoint refused, if it named one.
    /// <para>
    /// The structured <c>param</c> is read first, because that is the endpoint saying so rather
    /// than d47 reading tea leaves; the message is searched only as a fallback, since plenty of
    /// compatible servers answer with a bare sentence. <b>Nothing is inferred from a rejection
    /// that names nothing</b> — a demotion made on a guess is how a working capability gets
    /// turned off for a session with no way for the Commander to see why.
    /// </para>
    /// </summary>
    internal static Demotable? WhatWasRejected(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var said = detail.ToLowerInvariant();

        return said switch
        {
            _ when said.Contains("reasoning_effort", StringComparison.Ordinal) => Demotable.ReasoningEffort,
            _ when said.Contains("stream_options", StringComparison.Ordinal)
                   || said.Contains("include_usage", StringComparison.Ordinal) => Demotable.StreamUsage,
            _ when said.Contains("max_completion_tokens", StringComparison.Ordinal) => Demotable.ModernTokenLimit,

            // Tools are the one case with no single field name to match on, because a server that
            // cannot call them rejects "tools", "tool_choice" or "function" depending on who wrote
            // it. Paired with a word that means refusal so an ordinary tool error — which quotes
            // the tool's own name — is not read as the endpoint disowning the whole capability.
            _ when said.Contains("tool", StringComparison.Ordinal)
                   && (said.Contains("not supported", StringComparison.Ordinal)
                       || said.Contains("unsupported", StringComparison.Ordinal)
                       || said.Contains("does not support", StringComparison.Ordinal)
                       || said.Contains("unrecognized", StringComparison.Ordinal)
                       || said.Contains("unknown", StringComparison.Ordinal)) => Demotable.Tools,

            _ => null,
        };
    }

    /// <summary>
    /// The error body, reduced to what the endpoint actually said. Both the structured shape and
    /// a bare string are accepted, because both turn up.
    /// </summary>
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
                && document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object)
                {
                    var message = Text(error, "message");
                    var param = Text(error, "param");

                    return param is { Length: > 0 } && message is { Length: > 0 }
                        ? $"{message} ({param})"
                        : message ?? param;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON. The body is still the best account of what happened, trimmed below so a
            // wall of HTML from a misrouted proxy does not become a settings row's help text.
        }

        return raw.Length > 400 ? raw[..400] : raw;
    }

    /// <summary>
    /// Usage, if the endpoint sent any. Null for a chunk without one — which is every chunk but
    /// the last, and every chunk of a turn on a server that ignored <c>include_usage</c>. That
    /// second case is what leaves the turn unpriced rather than free.
    /// </summary>
    private static LlmUsage? Usage(JsonElement chunk)
    {
        if (!chunk.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var cached = 0;

        if (usage.TryGetProperty("prompt_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            cached = Number(details, "cached_tokens");
        }

        // prompt_tokens includes the cached part on this protocol, which is the opposite of the
        // convention the rest of d47 counts in. Converted once, here.
        return LlmUsage.FromInclusiveInput(
            Number(usage, "prompt_tokens"),
            cached,

            // No cache-write count exists in this protocol on any implementation. Zero is the
            // truth rather than a gap: where these servers cache, they do not bill for it.
            cacheWriteTokens: 0,
            Number(usage, "completion_tokens"));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static LlmStopReason Translate(string finishReason) => finishReason switch
    {
        "length" => LlmStopReason.MaxTokens,
        "tool_calls" or "function_call" => LlmStopReason.ToolUse,

        // A filtered completion is the endpoint declining, which surfaces as an unsure turn
        // rather than an error — the treatment a refusal gets on the Anthropic path.
        "content_filter" => LlmStopReason.Refusal,
        _ => LlmStopReason.Completed,
    };

    /// <summary>
    /// d47's four levels onto this protocol's. Max maps to high because there is nothing above it
    /// here, and naming a level the endpoint does not have is a 400 rather than more thinking.
    /// </summary>
    private static string Translate(ThinkingEffort effort) => effort switch
    {
        ThinkingEffort.Low => "low",
        ThinkingEffort.Medium => "medium",
        ThinkingEffort.High => "high",

        // Both map down rather than through. OpenAI's acceptance of "xhigh" is not something
        // d47 has seen a 200 for, and the fallback arm below would send "medium" -- a rung
        // *below* High for the setting that asks for more than it (list.md Phase 54). Raising
        // either of these wants a real response, not a guess.
        ThinkingEffort.Xhigh => "high",
        ThinkingEffort.Max => "high",
        _ => "medium",
    };

    private sealed class PendingCall
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public System.Text.StringBuilder Arguments { get; } = new();
    }

    public void Dispose() => _endpoint.Dispose();
}
