using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using D47.Core.Conversation;
using CoreConversation = D47.Core.Conversation;

namespace D47.Llm;

/// <summary>
/// The Anthropic implementation of <see cref="ILlmProvider"/>. Everything vendor-specific lives
/// behind this seam — model capability gating, effort translation, cache-breakpoint placement —
/// so the turn loop above it never learns which endpoint answered.
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    /// <summary>
    /// Models where a <c>{"role":"system"}</c> message can carry live game state with operator
    /// authority. Sonnet 5 is deliberately absent — it does not support them, which is exactly
    /// the sort of gap "capabilities as state" is meant to absorb.
    /// </summary>
    private static readonly HashSet<string> OperatorSystemMessageModels =
        new(StringComparer.Ordinal)
        {
            "claude-opus-5", "claude-opus-4-8", "claude-fable-5", "claude-mythos-5",
        };

    /// <summary>
    /// Minimum cacheable prefix per model. Below it a prefix silently does not cache — no error,
    /// just no entry — so it is worth knowing rather than discovering.
    /// </summary>
    private static readonly Dictionary<string, int> MinimumCacheablePrefix =
        new(StringComparer.Ordinal)
        {
            ["claude-opus-5"] = 512,
            ["claude-fable-5"] = 512,
            ["claude-mythos-5"] = 512,
            ["claude-opus-4-8"] = 1024,
            ["claude-sonnet-5"] = 1024,
            ["claude-opus-4-7"] = 2048,
            ["claude-haiku-4-5"] = 4096,
        };

    /// <summary>
    /// How many content blocks may pass before an intermediate cache breakpoint is spent. Set
    /// below the API's 20-block lookback with room to spare: a breakpoint placed exactly at the
    /// limit is one block of drift away from finding nothing (architecture.md §6).
    /// </summary>
    private const int BlocksPerBreakpoint = 15;

    /// <summary>
    /// The per-request breakpoint budget. One is always spent on the system block, which is why
    /// the count starts at one rather than zero.
    /// </summary>
    private const int MaxBreakpoints = 4;

    private readonly AnthropicClient _client;

    /// <summary>
    /// <paramref name="baseUrl"/> is null for Anthropic's own endpoint. A value points at
    /// something else speaking the same protocol — a gateway or a proxy — which is a setting
    /// the Commander can change without restarting d47 (list.md Phase 4).
    /// </summary>
    public AnthropicLlmProvider(string apiKey, string? baseUrl = null)
    {
        _client = string.IsNullOrWhiteSpace(baseUrl)
            ? new AnthropicClient { ApiKey = apiKey }
            : new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl };
    }

    public string Id => "anthropic";

    public string DisplayName => "Anthropic";

    public string DefaultModel => "claude-sonnet-5";

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = true,
        SupportsThinkingEffort = true,
        SupportsOperatorSystemMessages = OperatorSystemMessageModels.Contains(model),
        MinimumCacheablePrefixTokens = MinimumCacheablePrefix.GetValueOrDefault(model, 1024),
        SupportsToolCalls = true,
    };

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(request);

        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;

        // A tool call arrives in pieces: content_block_start names it, a run of input_json_delta
        // carries its arguments as JSON fragments, and content_block_stop ends it. Nothing is
        // parseable until the last fragment lands, so the call is assembled here and emitted
        // whole — running a tool on a half-built argument object is the one mistake this must
        // not make possible.
        var building = new Dictionary<long, PendingToolCall>();

        var stream = _client.Messages.CreateStreaming(parameters, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            // The enumerator is advanced by hand: C# forbids yielding from a catch block, and
            // every transport and API failure has to leave here as a Failed event rather than as
            // an exception crossing the seam. So the catch records, and the yield happens after.
            var finished = false;
            RawMessageStreamEvent streamEvent = default!;
            string? failureMessage = null;
            var failureTransient = false;

            try
            {
                finished = !await stream.MoveNextAsync().ConfigureAwait(false);
                if (!finished)
                {
                    streamEvent = stream.Current;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureMessage = Describe(ex);
                failureTransient = IsTransient(ex);
            }

            if (failureMessage is not null)
            {
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
                    yield return new LlmStreamEvent.ThinkingDelta(thinking!.Thinking);
                }
                else if (blockDelta.Delta.TryPickInputJson(out var inputJson)
                         && building.TryGetValue(blockDelta.Index, out var pending))
                {
                    pending.Input.Append(inputJson!.PartialJson);
                }
            }
            else if (streamEvent.TryPickContentBlockStart(out var blockStart)
                     && blockStart!.ContentBlock.TryPickToolUse(out var toolUse))
            {
                building[blockStart.Index] = new PendingToolCall(toolUse!.ID, toolUse.Name);
            }
            else if (streamEvent.TryPickContentBlockStop(out var blockStop)
                     && building.Remove(blockStop!.Index, out var call))
            {
                // Empty input is "{}", not "". A tool with no parameters produces no
                // input_json_delta at all, and an empty string is not a JSON object — the
                // argument parser would read it as malformed and drop a call that was fine.
                var input = call.Input.Length == 0 ? "{}" : call.Input.ToString();

                yield return new LlmStreamEvent.ToolUse(call.Id, call.Name, input);
            }
            else if (streamEvent.TryPickStart(out var start))
            {
                usage = Merge(usage, start!.Message.Usage);
            }
            else if (streamEvent.TryPickDelta(out var messageDelta))
            {
                usage = Merge(usage, messageDelta!.Usage);
                stopReason = Translate(messageDelta.Delta.StopReason?.ToString());
            }
        }

        await stream.DisposeAsync().ConfigureAwait(false);

        yield return new LlmStreamEvent.Completed(usage, stopReason);
    }

    private MessageCreateParams BuildParameters(LlmRequest request)
    {
        var prompt = request.Prompt;
        var capabilities = CapabilitiesFor(request.Model);

        var messages = new List<MessageParam>();

        // Each cache breakpoint walks back at most 20 content blocks looking for a prior entry,
        // and one agentic turn produces a tool_use and a tool_result per call — so a multi-tool
        // exchange can push the last breakpoint out of reach, after which the next turn silently
        // re-bills the entire prefix (architecture.md §6). Counting blocks and spending a
        // breakpoint before that happens is the mitigation.
        var blocksSinceBreakpoint = 0;
        var breakpointsSpent = 1;

        foreach (var turn in prompt.History)
        {
            var role = turn.Role == ConversationRole.Assistant ? Role.Assistant : Role.User;

            // An ordinary text turn stays an ordinary string on the wire. Not an optimisation:
            // a text-only session must serialise exactly as it did before tools existed, or
            // every cache entry written by a previous version is invalidated by the upgrade.
            if (turn.Content is [ConversationContent.Text only])
            {
                messages.Add(new MessageParam { Role = role, Content = only.Value });
                blocksSinceBreakpoint++;
                continue;
            }

            var blocks = new List<ContentBlockParam>();

            foreach (var part in turn.Content)
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
                        // The breakpoint goes on a tool result rather than anywhere else,
                        // because tool results are what make a turn long enough to need one.
                        var spendHere = blocksSinceBreakpoint >= BlocksPerBreakpoint
                                        && breakpointsSpent < MaxBreakpoints
                                        && ReferenceEquals(part, turn.Content[^1]);

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
                }
            }

            messages.Add(new MessageParam { Role = role, Content = blocks });
        }

        // Live game state goes after the cached history either way. The role it arrives under is
        // what differs: an operator system message cannot be spoofed by journal content, while a
        // <system-reminder> in the user turn caches identically but is only a convention.
        if (!string.IsNullOrWhiteSpace(prompt.LiveGameState))
        {
            if (capabilities.SupportsOperatorSystemMessages)
            {
                messages.Add(new MessageParam { Role = Role.System, Content = prompt.LiveGameState });
            }
            else if (messages.Count > 0)
            {
                var last = messages[^1];
                messages[^1] = new MessageParam
                {
                    Role = last.Role,
                    Content = $"<system-reminder>\n{prompt.LiveGameState}\n</system-reminder>\n\n{last.Content}",
                };
            }
        }

        return new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = request.MaxOutputTokens,

            // Position 1, serialised before everything else. The advertisement arrives already
            // canonicalised by ToolSchemaWriter, and it is passed through as raw JSON rather
            // than reassembled here — a second serializer with its own opinion about key order
            // is exactly the "non-deterministic serialization" §6 warns breaks byte-identity.
            Tools = [.. prompt.Tools.Select(Translate)],

            // The cache breakpoint. Everything above it — guardrails, persona, About Me — is
            // stable across turns; everything in Messages below it changes every turn.
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = prompt.RenderCachedSystemBlock(),
                    CacheControl = new CacheControlEphemeral(),
                },
            },

            // Adaptive rather than a token budget: budget_tokens is removed on Opus 5 and
            // returns a 400. Summarised display costs nothing extra — thinking is billed the
            // same either way — and it is what lets the panel show progress instead of a pause.
            Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },
            OutputConfig = new OutputConfig { Effort = Translate(request.Effort) },
            Messages = messages,
        };
    }

    /// <summary>
    /// A tool call being assembled from the stream. The id and name arrive first, on
    /// content_block_start; the arguments follow as JSON fragments that mean nothing until the
    /// last one has landed.
    /// </summary>
    private sealed class PendingToolCall(string id, string name)
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public System.Text.StringBuilder Input { get; } = new();
    }

    /// <summary>
    /// The advertisement as the API wants it. The schema is handed over as the exact bytes
    /// <see cref="D47.Core.Capabilities.ToolSchemaWriter"/> produced, which is what keeps a
    /// profile byte-identical every time it ships.
    /// </summary>
    private static ToolUnion Translate(ToolAdvertisement tool) => new Tool
    {
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = InputSchema.FromRawUnchecked(Parse(tool.InputSchemaJson)),
    };

    /// <summary>
    /// A JSON object as the SDK's raw property bag, preserving the order it was written in.
    /// </summary>
    private static Dictionary<string, System.Text.Json.JsonElement> Parse(string json)
    {
        var properties = new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal);

        using var document = System.Text.Json.JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                // Cloned because the document is disposed on the way out of this method, and an
                // un-cloned JsonElement is a window onto buffers that go with it.
                properties[property.Name] = property.Value.Clone();
            }
        }

        return properties;
    }

    private static Effort Translate(CoreConversation.ThinkingEffort effort) => effort switch
    {
        CoreConversation.ThinkingEffort.Low => Effort.Low,
        CoreConversation.ThinkingEffort.Medium => Effort.Medium,
        CoreConversation.ThinkingEffort.High => Effort.High,
        CoreConversation.ThinkingEffort.Max => Effort.Max,
        _ => Effort.High,
    };

    private static LlmStopReason Translate(string? stopReason) => stopReason switch
    {
        "refusal" => LlmStopReason.Refusal,
        "max_tokens" => LlmStopReason.MaxTokens,
        "tool_use" => LlmStopReason.ToolUse,
        _ => LlmStopReason.Completed,
    };

    // Usage arrives split across message_start and message_delta, and the two events carry
    // different types for it. Both merges take the larger of each field so a later event that
    // omits one does not zero what an earlier event already reported.

    private static LlmUsage Merge(LlmUsage current, Usage? incoming) =>
        incoming is null
            ? current
            : new LlmUsage(
                Math.Max(current.InputTokens, (int)incoming.InputTokens),
                Math.Max(current.OutputTokens, (int)incoming.OutputTokens),
                Math.Max(current.CacheCreationInputTokens, (int)(incoming.CacheCreationInputTokens ?? 0)),
                Math.Max(current.CacheReadInputTokens, (int)(incoming.CacheReadInputTokens ?? 0)));

    private static LlmUsage Merge(LlmUsage current, MessageDeltaUsage? incoming) =>
        incoming is null
            ? current
            : new LlmUsage(
                Math.Max(current.InputTokens, (int)(incoming.InputTokens ?? 0)),
                Math.Max(current.OutputTokens, (int)incoming.OutputTokens),
                Math.Max(current.CacheCreationInputTokens, (int)(incoming.CacheCreationInputTokens ?? 0)),
                Math.Max(current.CacheReadInputTokens, (int)(incoming.CacheReadInputTokens ?? 0)));

    /// <summary>
    /// Transient means "the same request may work shortly". Anything else needs a settings change
    /// before it is worth retrying, and the distinction is what stops a bad key from being retried
    /// forever or an overload from disabling the model permanently.
    /// </summary>
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
