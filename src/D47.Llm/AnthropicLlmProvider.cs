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

    public string DefaultModel => "claude-opus-5";

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = true,
        SupportsThinkingEffort = true,
        SupportsOperatorSystemMessages = OperatorSystemMessageModels.Contains(model),
        MinimumCacheablePrefixTokens = MinimumCacheablePrefix.GetValueOrDefault(model, 1024),
    };

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var parameters = BuildParameters(request);

        var usage = LlmUsage.None;
        var stopReason = LlmStopReason.Completed;

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
        foreach (var turn in prompt.History)
        {
            messages.Add(new MessageParam
            {
                Role = turn.Role == ConversationRole.Assistant ? Role.Assistant : Role.User,
                Content = turn.Text,
            });
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
