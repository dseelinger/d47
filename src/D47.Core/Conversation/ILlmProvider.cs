namespace D47.Core.Conversation;

/// <summary>What the active endpoint can actually do.</summary>
public sealed record LlmProviderCapabilities
{
    public required bool SupportsPromptCaching { get; init; }

    public required bool SupportsThinkingEffort { get; init; }

    /// <summary>
    /// Whether a <c>{"role":"system"}</c> message can be appended to the message list to carry live
    /// game state with operator authority.
    /// </summary>
    public required bool SupportsOperatorSystemMessages { get; init; }

    /// <summary>Below this, a prefix silently will not cache — no error, just no cache entry.</summary>
    public required int MinimumCacheablePrefixTokens { get; init; }

    /// <summary>
    /// Whether this endpoint can be sent tool definitions and have its <c>tool_use</c> replies executed
    /// and fed back.
    /// </summary>
    public bool SupportsToolCalls { get; init; }

    /// <summary>
    /// Whether this endpoint can run a web search on the model's behalf and hand the results back
    /// inside the same turn.
    /// </summary>
    public bool SupportsWebSearch { get; init; }
}

public sealed record LlmUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens,
    int CacheReadInputTokens)
{
    public static readonly LlmUsage None = new(0, 0, 0, 0);

    /// <summary>
    /// A turn whose provider reported no usage at all, which is not the same as a turn that used
    /// nothing (Phase 29).
    /// </summary>
    public static readonly LlmUsage Unreported = new(0, 0, 0, 0) { Reported = false };

    /// <summary>Whether these numbers came from the provider.</summary>
    public bool Reported { get; init; } = true;

    /// <summary>How many web searches the provider ran for this turn.</summary>
    public int WebSearchRequests { get; init; }

    /// <summary>Uncached input is only part of the prompt — the rest was written to or read from cache.</summary>
    public int TotalInputTokens => InputTokens + CacheCreationInputTokens + CacheReadInputTokens;

    /// <summary>
    /// Usage from a provider whose input count includes the cached part, converted to the convention
    /// above.
    /// </summary>
    /// <param name="promptTokens">The provider's inclusive input count.</param>
    /// <param name="cachedInputTokens">How much of it was read from cache.</param>
    /// <param name="cacheWriteTokens">How much of it was written to cache.</param>
    /// <param name="outputTokens">
    /// Completion tokens, which no convention disagrees about.
    /// </param>
    public static LlmUsage FromInclusiveInput(
        int promptTokens,
        int cachedInputTokens,
        int cacheWriteTokens,
        int outputTokens)
    {
        var prompt = Math.Max(promptTokens, 0);
        var read = Math.Clamp(cachedInputTokens, 0, prompt);
        var written = Math.Clamp(cacheWriteTokens, 0, prompt - read);

        return new LlmUsage(prompt - read - written, Math.Max(outputTokens, 0), written, read);
    }
}

public enum LlmStopReason
{
    Completed,
    MaxTokens,

    /// <summary>The model declined.</summary>
    Refusal,

    /// <summary>The provider stopped a long server-side turn part-way and is willing to resume it.</summary>
    Paused,

    /// <summary>The model stopped because it wants a tool run.</summary>
    ToolUse,
}

/// <summary>One event from a streamed completion.</summary>
public abstract record LlmStreamEvent
{
    private LlmStreamEvent()
    {
    }

    /// <summary>A fragment of the reply.</summary>
    public sealed record TextDelta(string Text) : LlmStreamEvent;

    /// <summary>A fragment of summarised reasoning, when the endpoint returns any.</summary>
    public sealed record ThinkingDelta(string Text) : LlmStreamEvent;

    /// <summary>The model has asked for a tool, with its arguments fully assembled.</summary>
    public sealed record ToolUse(string Id, string Name, string InputJson) : LlmStreamEvent;

    public sealed record Completed(LlmUsage Usage, LlmStopReason StopReason) : LlmStreamEvent;

    /// <summary>
    /// <paramref name="Transient"/> separates "retry later" (rate limited, overloaded, network) from
    /// "this will not work until something changes" (no key, bad key, unknown model).
    /// </summary>
    public sealed record Failed(string Message, bool Transient) : LlmStreamEvent;
}

public sealed record LlmRequest
{
    public required string Model { get; init; }

    public required PromptAssembly Prompt { get; init; }

    public required ThinkingEffort Effort { get; init; }

    /// <summary>How adventurous the sampler may be for this call (#98).</summary>
    public required LlmSampling Sampling { get; init; }

    /// <summary>A cockpit reply is a few sentences.</summary>
    public int MaxOutputTokens { get; init; } = 8192;

    /// <summary>Whether the provider may search the web for this turn.</summary>
    public bool WebSearch { get; init; }
}

/// <summary>The seam.</summary>
public interface ILlmProvider
{
    /// <summary>Stable identifier used by the price table — "anthropic", "openai".</summary>
    string Id { get; }

    /// <summary>
    /// Whether this provider is pointed at an address on the Commander's own machine (Phase 29).
    /// </summary>
    bool RunsOnThisMachine => false;

    string DisplayName { get; }

    string DefaultModel { get; }

    LlmProviderCapabilities CapabilitiesFor(string model);

    IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken);
}
