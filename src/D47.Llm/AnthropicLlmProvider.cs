using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Anthropic.Models.Models;
using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using CoreConversation = D47.Core.Conversation;

namespace D47.Llm;

/// <summary>The Anthropic implementation of <see cref="ILlmProvider"/>.</summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    /// <summary>The failure when the endpoint refuses tool search.</summary>
    internal const string ToolSearchRefused =
        "Anthropic refused tool search for this model, so it will be sent the mode's tool list.";

    // ---- No temperature goes to Anthropic, and that is the finding rather than an omission ---- #98 opened
    // by saying "Anthropic's path takes temperature too, but interacts with effort/thinking, so the omission
    // rules Phase 54 already wrote apply here".

    /// <summary>The ceiling on searches in one turn.</summary>
    private const long MaxWebSearchesPerTurn = 3;

    /// <summary>How many content blocks may pass before an intermediate cache breakpoint is spent.</summary>
    private const int BlocksPerBreakpoint = 15;

    /// <summary>The per-request breakpoint budget.</summary>
    private const int MaxBreakpoints = 4;

    private readonly AnthropicClient _client;

    private readonly ModelCatalogSource _catalog;

    /// <summary>Whether this is Anthropic's own endpoint rather than a gateway.</summary>
    private readonly bool _ownEndpoint;

    /// <summary>The address demotions are recorded against.</summary>
    private readonly string _endpoint;

    /// <summary>
    /// <paramref name="baseUrl"/> is null for Anthropic's own endpoint; <paramref name="catalog"/> is null for
    /// <see cref="ModelCatalogSource.Shared"/>.
    /// </summary>
    public AnthropicLlmProvider(string apiKey, string? baseUrl = null, ModelCatalogSource? catalog = null)
    {
        _catalog = catalog ?? ModelCatalogSource.Shared;
        _ownEndpoint = string.IsNullOrWhiteSpace(baseUrl);
        _endpoint = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.anthropic.com" : baseUrl;

        // Still testing baseUrl rather than the field just set from it: the null analysis follows
        // IsNullOrWhiteSpace and does not follow a bool that happens to mean the same.
        _client = string.IsNullOrWhiteSpace(baseUrl)
            ? new AnthropicClient { ApiKey = apiKey }
            : new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl };
    }

    public string Id => "anthropic";

    public string DisplayName => "Anthropic";

    public string DefaultModel => _catalog.Current.DefaultFor(Id) ?? string.Empty;

    public LlmProviderCapabilities CapabilitiesFor(string model)
    {
        var traits = Traits(model);

        return new()
        {
            SupportsPromptCaching = true,

            // Both halves, and in this order: what is known, then what has been learned.
            SupportsThinkingEffort =
                !traits.LegacyThinking
                && EndpointDemotions.Allows(_endpoint, Demotable.AdaptiveThinking, model),

            SupportsOperatorSystemMessages = traits.OperatorSystemMessages,
            MinimumCacheablePrefixTokens = traits.MinimumCacheablePrefix,
            SupportsToolCalls = true,
            SupportsWebSearch = _ownEndpoint,
            SupportsToolSearch =
                _ownEndpoint
                && traits.ToolSearch
                && EndpointDemotions.Allows(_endpoint, Demotable.ToolSearch, model),
        };
    }

    private ModelTraits Traits(string model) => _catalog.TraitsFor(Id, model);

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;

        // A tool call arrives in pieces: content_block_start names it, a run of input_json_delta carries its
        // arguments as JSON fragments, and content_block_stop ends it.
        var building = new Dictionary<long, PendingToolCall>();

        // Tool search arrives the same way: a server_tool_use assembled from its input deltas, then a
        // tool_search_tool_result whole in its start event. Both go back as they came.
        var searching = new Dictionary<long, PendingSearch>();
        var searchResults = new Dictionary<long, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>();
        var queries = new Dictionary<string, string>(StringComparer.Ordinal);

        // A thinking block arrives as a start, a run of thinking deltas and a signature delta; a redacted one
        // whole in its start event. Both go back as they came.
        var thinkingBlocks = new Dictionary<long, PendingThinking>();
        var redactedBlocks = new Dictionary<long, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>();

        var stream = _client.Messages.CreateStreaming(BuildParameters(request), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        // How many events have come off the stream, and whether this turn has already spent its one demotion.
        var advanced = 0;
        var demoted = false;

        while (true)
        {
            // The enumerator is advanced by hand: C# forbids yielding from a catch block, and every transport
            // and API failure has to leave here as a Failed event rather than as an exception crossing the
            // seam.
            var finished = false;
            RawMessageStreamEvent streamEvent = default!;
            string? failureMessage = null;
            var failureTransient = false;
            Demotable? refused = null;

            try
            {
                finished = !await stream.MoveNextAsync().ConfigureAwait(false);
                if (!finished)
                {
                    streamEvent = stream.Current;
                }

                advanced++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureMessage = Describe(ex);
                failureTransient = IsTransient(ex);

                // Read from the exception's own text rather than from Describe's, which replaces the body
                // with one of d47's sentences for the cases it recognises.
                refused = ex is AnthropicBadRequestException or AnthropicUnprocessableEntityException
                    ? WhatWasRejected(ex.Message)
                    : null;
            }

            if (failureMessage is not null)
            {
                // Not retried here: the provider holds only the deferred list, and the mode's list is TurnLoop's
                // to build on the next turn.
                if (refused == Demotable.ToolSearch)
                {
                    if (advanced == 0 && request.Prompt.Tools.Any(tool => tool.Deferred))
                    {
                        EndpointDemotions.Demote(_endpoint, Demotable.ToolSearch, request.Model);
                        failureMessage = ToolSearchRefused;
                    }
                }

                // Advertise, then demote (Phase 29, ported here by Phase 54).
                else if (advanced == 0 && !demoted && refused is { } rejected
                    && EndpointDemotions.Demote(_endpoint, rejected, request.Model))
                {
                    demoted = true;

                    await stream.DisposeAsync().ConfigureAwait(false);

                    // Rebuilt rather than amended: CapabilitiesFor reads the demotion just recorded, so the
                    // second request is assembled by the same code that will assemble every request after it.
                    stream = _client.Messages.CreateStreaming(BuildParameters(request), cancellationToken)
                        .GetAsyncEnumerator(cancellationToken);

                    continue;
                }

                yield return new LlmStreamEvent.Failed(failureMessage, failureTransient);
                yield break;
            }

            if (finished)
            {
                break;
            }

            if (streamEvent.TryPickContentBlockDelta(out var blockDelta))
            {
                if (blockDelta!.Delta.TryPickText(out var text))
                {
                    yield return new LlmStreamEvent.TextDelta(text!.Text);
                }
                else if (blockDelta.Delta.TryPickThinking(out var thinking))
                {
                    if (thinkingBlocks.TryGetValue(blockDelta.Index, out var reasoning))
                    {
                        reasoning.Thinking.Append(thinking!.Thinking);
                    }

                    yield return new LlmStreamEvent.ThinkingDelta(thinking!.Thinking);
                }
                else if (blockDelta.Delta.TryPickSignature(out var signature))
                {
                    if (thinkingBlocks.TryGetValue(blockDelta.Index, out var reasoning))
                    {
                        reasoning.Signature.Append(signature!.Signature);
                    }
                }
                else if (blockDelta.Delta.TryPickInputJson(out var inputJson))
                {
                    if (building.TryGetValue(blockDelta.Index, out var pending))
                    {
                        pending.Input.Append(inputJson!.PartialJson);
                    }
                    else if (searching.TryGetValue(blockDelta.Index, out var search))
                    {
                        search.Input.Append(inputJson!.PartialJson);
                    }
                }
            }
            else if (streamEvent.TryPickContentBlockStart(out var blockStart))
            {
                if (blockStart!.ContentBlock.TryPickToolUse(out var toolUse))
                {
                    building[blockStart.Index] = new PendingToolCall(toolUse!.ID, toolUse.Name);
                }
                else if (blockStart.ContentBlock.TryPickServerToolUse(out var serverCall)
                         && IsToolSearch(serverCall!.RawData))
                {
                    searching[blockStart.Index] = new PendingSearch(serverCall.RawData);
                }
                else if (blockStart.ContentBlock.TryPickToolSearchToolResult(out var searchResult))
                {
                    searchResults[blockStart.Index] = searchResult!.RawData;
                }
                else if (blockStart.ContentBlock.TryPickThinking(out var thinkingStart))
                {
                    thinkingBlocks[blockStart.Index] = new PendingThinking(thinkingStart!.Thinking, thinkingStart.Signature);
                }
                else if (blockStart.ContentBlock.TryPickRedactedThinking(out var redactedStart))
                {
                    redactedBlocks[blockStart.Index] = redactedStart!.RawData;
                }
            }
            else if (streamEvent.TryPickContentBlockStop(out var blockStop))
            {
                if (building.Remove(blockStop!.Index, out var call))
                {
                    // Empty input is "{}", not "".
                    var input = call.Input.Length == 0 ? "{}" : call.Input.ToString();

                    yield return new LlmStreamEvent.ToolUse(call.Id, call.Name, input);
                }
                else if (searching.Remove(blockStop.Index, out var search))
                {
                    var input = search.Input.Length == 0 ? "{}" : search.Input.ToString();

                    if (Text(search.Raw, "id") is { } id)
                    {
                        queries[id] = QueryIn(input);
                    }

                    yield return new LlmStreamEvent.Opaque(Serialize(search.Raw, input));
                }
                else if (searchResults.Remove(blockStop.Index, out var found))
                {
                    yield return new LlmStreamEvent.Opaque(Serialize(found));

                    var query = Text(found, "tool_use_id") is { } callId
                        ? queries.GetValueOrDefault(callId, string.Empty)
                        : string.Empty;

                    yield return new LlmStreamEvent.ToolSearched(query, ToolsFound(found));
                }
                else if (thinkingBlocks.Remove(blockStop.Index, out var reasoning))
                {
                    yield return new LlmStreamEvent.ThinkingBlock(reasoning.Serialize());
                }
                else if (redactedBlocks.Remove(blockStop.Index, out var redacted))
                {
                    yield return new LlmStreamEvent.ThinkingBlock(Serialize(redacted));
                }
            }
            else if (streamEvent.TryPickStart(out var start))
            {
                usage = Merge(usage, start!.Message.Usage);
            }
            else if (streamEvent.TryPickDelta(out var messageDelta))
            {
                usage = Merge(usage, messageDelta!.Usage);
                stopReason = messageDelta.Delta.StopReason is { } reason ? Translate(reason) : LlmStopReason.Completed;
            }
        }

        await stream.DisposeAsync().ConfigureAwait(false);

        yield return new LlmStreamEvent.Completed(usage, stopReason);
    }

    internal MessageCreateParams BuildParameters(LlmRequest request)
    {
        var prompt = request.Prompt;
        var capabilities = CapabilitiesFor(request.Model);
        var deferring = prompt.Tools.Any(tool => tool.Deferred);

        var messages = new List<MessageParam>();

        // Each cache breakpoint walks back at most 20 content blocks looking for a prior entry, and one
        // agentic turn produces a tool_use and a tool_result per call — so a multi-tool exchange can push the
        // last breakpoint out of reach, after which the next turn silently re-bills the entire prefix
        //.
        var blocksSinceBreakpoint = 0;
        var breakpointsSpent = 1;

        // The blocks the last message was built from, or null when it was built from a plain string.
        List<ContentBlockParam>? lastBlocks = null;
        var lastText = string.Empty;

        foreach (var turn in prompt.History)
        {
            var role = turn.Role == ConversationRole.Assistant ? Role.Assistant : Role.User;

            // Another provider's blocks are not sent, and nor are this provider's tool search blocks on a request
            // that defers no tool: a replayed tool_reference to a tool the request does not declare is a 400.
            var parts = turn.Content
                .Where(part => part switch
                {
                    ConversationContent.Opaque opaque => opaque.ProviderId == Id && deferring,
                    ConversationContent.ThinkingBlock thinking => thinking.ProviderId == Id,
                    ConversationContent.TrailingState => false,
                    _ => true,
                })
                .ToList();

            if (parts.Count == 0)
            {
                continue;
            }

            // An ordinary text turn stays an ordinary string on the wire.
            if (parts is [ConversationContent.Text only])
            {
                messages.Add(new MessageParam { Role = role, Content = only.Value });
                blocksSinceBreakpoint++;
                lastBlocks = null;
                lastText = only.Value;
                AttachSentState(turn);
                continue;
            }

            var blocks = new List<ContentBlockParam>();

            foreach (var part in parts)
            {
                blocksSinceBreakpoint++;

                switch (part)
                {
                    case ConversationContent.Text text:
                        blocks.Add(new TextBlockParam { Text = text.Value });
                        break;

                    case ConversationContent.ToolUse call:
                        blocks.Add(new ToolUseBlockParam
                        {
                            ID = call.Id,
                            Name = call.Name,
                            Input = Parse(call.InputJson),
                        });
                        break;

                    case ConversationContent.ToolResult result:
                        // The breakpoint goes on a tool result rather than anywhere else, because tool
                        // results are what make a turn long enough to need one.
                        var spendHere = blocksSinceBreakpoint >= BlocksPerBreakpoint
                                        && breakpointsSpent < MaxBreakpoints
                                        && ReferenceEquals(part, parts[^1]);

                        blocks.Add(new ToolResultBlockParam
                        {
                            ToolUseID = result.ToolUseId,
                            Content = result.Content,
                            IsError = result.IsError,
                            CacheControl = spendHere ? new CacheControlEphemeral() : null,
                        });

                        if (spendHere)
                        {
                            breakpointsSpent++;
                            blocksSinceBreakpoint = 0;
                        }

                        break;

                    case ConversationContent.Opaque opaque:
                        blocks.Add(Block(opaque.Json));
                        break;

                    case ConversationContent.ThinkingBlock thinking:
                        blocks.Add(Block(thinking.Json));
                        break;
                }
            }

            messages.Add(new MessageParam { Role = role, Content = blocks });
            lastBlocks = blocks;
            AttachSentState(turn);
        }

        // Live game state goes after the cached history either way, and state an earlier round of this turn sent
        // goes where that round put it, so that each round's request begins with the whole of the last one.
        void AttachSentState(ConversationMessage turn)
        {
            foreach (var sent in turn.Content.OfType<ConversationContent.TrailingState>())
            {
                AttachState(sent.Value);
            }
        }

        void AttachState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return;
            }

            if (capabilities.SupportsOperatorSystemMessages)
            {
                messages.Add(new MessageParam { Role = Role.System, Content = state });
            }
            else if (messages.Count > 0)
            {
                var last = messages[^1];
                var reminder = $"<system-reminder>\n{state}\n</system-reminder>";

                // Folded into the last message rather than added after it: a message of its own would be a
                // second user turn in a row, and in the middle of a tool round it would stand between a
                // tool_use and the result answering it.
                if (lastBlocks is null)
                {
                    lastText = $"{reminder}\n\n{lastText}";
                    messages[^1] = new MessageParam { Role = last.Role, Content = lastText };
                }
                else
                {
                    // After the blocks rather than before them, because a user message carrying tool results
                    // has to open with them.
                    lastBlocks = [.. lastBlocks, new TextBlockParam { Text = reminder }];
                    messages[^1] = new MessageParam { Role = last.Role, Content = lastBlocks };
                }
            }
        }

        AttachState(prompt.TrailingState);

        // Position 1, serialised before everything else.
        // The search tool first when any tool is deferred, because it is the one that loads them.
        List<ToolUnion> tools =
        [
            .. deferring ? [new ToolUnion(ToolSearchTool())] : Array.Empty<ToolUnion>(),
            .. prompt.Tools.Select(Translate),
            .. WebSearchTool(request),
        ];

        return new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = request.MaxOutputTokens,
            Tools = tools,

            // The tools stay declared when calls are not allowed, so the prefix does not change.
            ToolChoice = !request.ToolCallsAllowed && tools.Count > 0 ? new ToolChoice(new ToolChoiceNone()) : null,

            // The cache breakpoint.
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = prompt.RenderCachedSystemBlock(),
                    CacheControl = new CacheControlEphemeral(),
                },
            },

            // Adaptive rather than a token budget: budget_tokens is removed on Opus 5 and returns a 400.
            Thinking = capabilities.SupportsThinkingEffort
                ? new ThinkingConfigAdaptive { Display = Display.Summarized }
                : (ThinkingConfigParam?)null,
            OutputConfig = capabilities.SupportsThinkingEffort
                ? new OutputConfig { Effort = Translate(request.Effort) }
                : null,
            Messages = messages,
        };
    }

    /// <summary>A block kept as JSON, as the SDK sends it.</summary>
    private static ContentBlockParam Block(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);

        return new ContentBlockParam(document.RootElement.Clone());
    }

    /// <summary>The web search declaration, or nothing.</summary>
    private IEnumerable<ToolUnion> WebSearchTool(LlmRequest request)
    {
        if (!request.WebSearch)
        {
            yield break;
        }

        yield return Traits(request.Model).BasicWebSearchOnly
            ? new ToolUnion(new WebSearchTool20250305 { MaxUses = MaxWebSearchesPerTurn })
            : new ToolUnion(new WebSearchTool20260318 { MaxUses = MaxWebSearchesPerTurn });
    }

    /// <summary>A tool call being assembled from the stream.</summary>
    private sealed class PendingToolCall(string id, string name)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public System.Text.StringBuilder Input { get; } = new();
    }

    /// <summary>A thinking block being assembled from the stream.</summary>
    private sealed class PendingThinking(string thinking, string signature)
    {
        public System.Text.StringBuilder Thinking { get; } = new(thinking);

        public System.Text.StringBuilder Signature { get; } = new(signature);

        public string Serialize()
        {
            using var buffer = new MemoryStream();

            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("type", "thinking");
                writer.WriteString("thinking", Thinking.ToString());
                writer.WriteString("signature", Signature.ToString());
                writer.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    /// <summary>The BM25 tool search declaration.</summary>
    private static ToolSearchToolBm25_20251119 ToolSearchTool() =>
        ToolSearchToolBm25_20251119.FromRawUnchecked(Parse(
            """{"type":"tool_search_tool_bm25_20251119","name":"tool_search_tool_bm25"}"""));

    /// <summary>A tool search call being assembled from the stream.</summary>
    private sealed class PendingSearch(IReadOnlyDictionary<string, System.Text.Json.JsonElement> raw)
    {
        public IReadOnlyDictionary<string, System.Text.Json.JsonElement> Raw { get; } = raw;

        public System.Text.StringBuilder Input { get; } = new();
    }

    /// <summary>The advertisement as the API wants it. A deferred tool carries no <c>cache_control</c>.</summary>
    private static ToolUnion Translate(ToolAdvertisement tool) => new Tool
    {
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = InputSchema.FromRawUnchecked(Parse(tool.InputSchemaJson)),
        DeferLoading = tool.Deferred ? true : null,
    };

    /// <summary>Whether a server_tool_use is a tool search rather than another server tool.</summary>
    private static bool IsToolSearch(IReadOnlyDictionary<string, System.Text.Json.JsonElement> raw) =>
        Text(raw, "name")?.StartsWith("tool_search_tool", StringComparison.Ordinal) == true;

    private static string? Text(IReadOnlyDictionary<string, System.Text.Json.JsonElement> raw, string name) =>
        raw.TryGetValue(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A block as JSON in its original key order, with <paramref name="input"/> in place of its input.</summary>
    private static string Serialize(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> raw,
        string? input = null)
    {
        using var buffer = new MemoryStream();

        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var (name, value) in raw)
            {
                writer.WritePropertyName(name);

                if (input is not null && name == "input")
                {
                    using var parsed = System.Text.Json.JsonDocument.Parse(input);
                    parsed.RootElement.WriteTo(writer);
                }
                else
                {
                    value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string QueryIn(string inputJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(inputJson);

            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                   && document.RootElement.TryGetProperty("query", out var query)
                   && query.ValueKind == System.Text.Json.JsonValueKind.String
                ? query.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>The tool names a search result references.</summary>
    private static List<string> ToolsFound(IReadOnlyDictionary<string, System.Text.Json.JsonElement> raw)
    {
        if (!raw.TryGetValue("content", out var content)
            || content.ValueKind != System.Text.Json.JsonValueKind.Object
            || !content.TryGetProperty("tool_references", out var references)
            || references.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. references.EnumerateArray()
                .Where(reference => reference.ValueKind == System.Text.Json.JsonValueKind.Object)
                .Select(reference => reference.TryGetProperty("tool_name", out var name) ? name.GetString() : null)
                .OfType<string>(),
        ];
    }

    /// <summary>A JSON object as the SDK's raw property bag, preserving the order it was written in.</summary>
    private static Dictionary<string, System.Text.Json.JsonElement> Parse(string json)
    {
        var properties = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);

        using var document = System.Text.Json.JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Cloned because the document is disposed on the way out of this method, and an un-cloned
                // JsonElement is a window onto buffers that go with it.
                properties[property.Name] = property.Value.Clone();
            }
        }

        return properties;
    }

    /// <summary>Which optional field the endpoint refused, if it named one (Phase 54).</summary>
    internal static Demotable? WhatWasRejected(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var said = detail.ToLowerInvariant();

        if (said.Contains("tool_search", StringComparison.Ordinal)
            || said.Contains("tool search", StringComparison.Ordinal)
            || said.Contains("defer_loading", StringComparison.Ordinal)
            || said.Contains("tool_reference", StringComparison.Ordinal))
        {
            return Demotable.ToolSearch;
        }

        return said.Contains("output_config", StringComparison.Ordinal)
               || said.Contains("thinking", StringComparison.Ordinal)
               || said.Contains("effort", StringComparison.Ordinal)
            ? Demotable.AdaptiveThinking
            : null;
    }

    private static Effort Translate(CoreConversation.ThinkingEffort effort) => effort switch
    {
        CoreConversation.ThinkingEffort.Low => Effort.Low,
        CoreConversation.ThinkingEffort.Medium => Effort.Medium,
        CoreConversation.ThinkingEffort.High => Effort.High,

        // Straight through: the pinned SDK's Effort is { Low, Medium, High, Xhigh, Max }.
        CoreConversation.ThinkingEffort.Xhigh => Effort.Xhigh,
        CoreConversation.ThinkingEffort.Max => Effort.Max,
        _ => Effort.High,
    };

    /// <summary><c>pause_turn</c> is here because it was silently absent.</summary>
    private static LlmStopReason Translate(StopReason stopReason) => stopReason switch
    {
        StopReason.Refusal => LlmStopReason.Refusal,
        StopReason.MaxTokens => LlmStopReason.MaxTokens,
        StopReason.ToolUse => LlmStopReason.ToolUse,
        StopReason.PauseTurn => LlmStopReason.Paused,

        // The context window filling is a truncation like any other.
        StopReason.ModelContextWindowExceeded => LlmStopReason.MaxTokens,

        // EndTurn, StopSequence, and anything a later SDK adds.
        _ => LlmStopReason.Completed,
    };

    // Usage arrives split across message_start and message_delta, and the two events carry different types
    // for it.

    private static LlmUsage Merge(LlmUsage current, Usage? incoming) =>
        incoming is null
            ? current
            : new LlmUsage(
                Math.Max(current.InputTokens, (int)incoming.InputTokens),
                Math.Max(current.OutputTokens, (int)incoming.OutputTokens),
                Math.Max(current.CacheCreationInputTokens, (int)(incoming.CacheCreationInputTokens ?? 0)),
                Math.Max(current.CacheReadInputTokens, (int)(incoming.CacheReadInputTokens ?? 0)))
            {
                WebSearchRequests = Math.Max(
                    current.WebSearchRequests,
                    (int)(incoming.ServerToolUse?.WebSearchRequests ?? 0)),
            };

    private static LlmUsage Merge(LlmUsage current, MessageDeltaUsage? incoming) =>
        incoming is null
            ? current
            : new LlmUsage(
                Math.Max(current.InputTokens, (int)(incoming.InputTokens ?? 0)),
                Math.Max(current.OutputTokens, (int)incoming.OutputTokens),
                Math.Max(current.CacheCreationInputTokens, (int)(incoming.CacheCreationInputTokens ?? 0)),
                Math.Max(current.CacheReadInputTokens, (int)(incoming.CacheReadInputTokens ?? 0)))
            {
                WebSearchRequests = Math.Max(
                    current.WebSearchRequests,
                    (int)(incoming.ServerToolUse?.WebSearchRequests ?? 0)),
            };

    /// <summary>Asks the endpoint for its Claude models, every page. Free: it runs no completion.</summary>
    public async Task<EndpointModels> ListModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.Models.List(new ModelListParams(), cancellationToken).ConfigureAwait(false);
            var listed = new List<ListedModel>();

            await foreach (var model in page.Paginate(cancellationToken).ConfigureAwait(false))
            {
                if (model.ID.StartsWith("claude-", StringComparison.Ordinal))
                {
                    listed.Add(new ListedModel(model.ID, model.DisplayName, model.CreatedAt)
                    {
                        AdaptiveThinking = model.Capabilities?.Thinking?.Types?.Adaptive?.Supported,
                    });
                }
            }

            return new EndpointModels(
                EndpointReach.Answered,
                [.. listed.Select(model => model.Id).Order(StringComparer.Ordinal)],
                null)
            {
                Listed = listed,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return IsTransient(ex) || ex is not AnthropicApiException
                ? EndpointModels.Unreachable(Describe(ex))
                : EndpointModels.Refused(Describe(ex));
        }
    }

    /// <summary>Transient means "the same request may work shortly".</summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        AnthropicRateLimitException => true,
        Anthropic5xxException => true,
        AnthropicIOException => true,
        TimeoutException => true,
        _ => false,
    };

    private static string Describe(Exception ex) => ex switch
    {
        AnthropicRateLimitException => "Rate limited by Anthropic; it should clear shortly.",
        Anthropic5xxException => "Anthropic reported a server error; it should clear shortly.",
        AnthropicIOException => "Could not reach Anthropic — check the network connection.",
        AnthropicUnauthorizedException => "The Anthropic API key was rejected. Check it in settings.",
        AnthropicForbiddenException => "This Anthropic key is not permitted to use that model.",
        AnthropicNotFoundException => "Anthropic does not recognise that model name.",
        _ => ex.Message,
    };
}
