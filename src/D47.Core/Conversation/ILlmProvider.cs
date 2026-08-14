namespace D47.Core.Conversation;

/// <summary>
/// What the active endpoint can actually do. Reported into "Capabilities as state" so a
/// feature the provider lacks is a capability that is off rather than a failure to handle
/// (architecture.md §6).
/// </summary>
public sealed record LlmProviderCapabilities
{
    public required bool SupportsPromptCaching { get; init; }

    public required bool SupportsThinkingEffort { get; init; }

    /// <summary>
    /// Whether a <c>{"role":"system"}</c> message can be appended to the message list to carry
    /// live game state with operator authority. Model-gated rather than provider-gated: true on
    /// Claude Opus 5 and Opus 4.8, false on Sonnet 5. False selects the
    /// <c>&lt;system-reminder&gt;</c> fallback, which caches identically but carries a weaker
    /// trust signal.
    /// </summary>
    public required bool SupportsOperatorSystemMessages { get; init; }

    /// <summary>
    /// Below this, a prefix silently will not cache — no error, just no cache entry. Model
    /// dependent: 512 tokens on Claude Opus 5, 1024 on Opus 4.8 and Sonnet 5.
    /// </summary>
    public required int MinimumCacheablePrefixTokens { get; init; }

    /// <summary>
    /// Whether this endpoint can be sent tool definitions <em>and</em> have its
    /// <c>tool_use</c> replies executed and fed back. Both halves, deliberately: advertising a
    /// tool the turn loop would silently drop is worse than not offering it, because the model
    /// then tells the Commander it has done something that never happened.
    /// <para>
    /// True on Anthropic since Phase 14, which built the agentic half — tool_use blocks parsed
    /// out of the stream, run against the registry, and fed back as tool_result. It stays a
    /// per-provider flag rather than becoming an assumption, because the next endpoint to arrive
    /// will need the same two halves and may only have one; it slots into "capabilities as
    /// state" like everything else an endpoint may or may not do.
    /// </para>
    /// </summary>
    public bool SupportsToolCalls { get; init; }
}

public sealed record LlmUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens,
    int CacheReadInputTokens)
{
    public static readonly LlmUsage None = new(0, 0, 0, 0);

    /// <summary>
    /// Uncached input is only part of the prompt — the rest was written to or read from cache.
    /// Reading <see cref="InputTokens"/> alone under-reports a cached turn substantially.
    /// </summary>
    public int TotalInputTokens => InputTokens + CacheCreationInputTokens + CacheReadInputTokens;
}

public enum LlmStopReason
{
    Completed,
    MaxTokens,

    /// <summary>The model declined. Surfaces as an unsure turn, not as an error.</summary>
    Refusal,

    /// <summary>
    /// The model stopped because it wants a tool run. Not an ending: the turn loop executes what
    /// was asked for and calls the provider again with the results, and only the completion after
    /// that is an answer.
    /// </summary>
    ToolUse,
}

/// <summary>
/// One event from a streamed completion. A closed hierarchy — the private constructor means
/// no other assembly can add a case the turn loop has not handled.
/// </summary>
public abstract record LlmStreamEvent
{
    private LlmStreamEvent()
    {
    }

    /// <summary>
    /// A fragment of the reply. Sentence-chunking these is the largest perceived-latency win
    /// available, and it only exists because the reply is streamed (architecture.md §6).
    /// </summary>
    public sealed record TextDelta(string Text) : LlmStreamEvent;

    /// <summary>A fragment of summarised reasoning, when the endpoint returns any.</summary>
    public sealed record ThinkingDelta(string Text) : LlmStreamEvent;

    /// <summary>
    /// The model has asked for a tool, with its arguments fully assembled.
    /// <para>
    /// Emitted once per call, when the block is complete rather than as it arrives. The
    /// arguments stream in as JSON fragments that are not parseable until the last one lands, so
    /// there is nothing a partial event could carry that a caller could act on — and a tool run
    /// half a call early is the one mistake this design must not make possible.
    /// </para>
    /// </summary>
    public sealed record ToolUse(string Id, string Name, string InputJson) : LlmStreamEvent;

    public sealed record Completed(LlmUsage Usage, LlmStopReason StopReason) : LlmStreamEvent;

    /// <summary>
    /// <paramref name="Transient"/> separates "retry later" (rate limited, overloaded, network)
    /// from "this will not work until something changes" (no key, bad key, unknown model). The
    /// first suspends the capability for a few turns; the second until settings change.
    /// </summary>
    public sealed record Failed(string Message, bool Transient) : LlmStreamEvent;
}

public sealed record LlmRequest
{
    public required string Model { get; init; }

    public required PromptAssembly Prompt { get; init; }

    public required ThinkingEffort Effort { get; init; }

    /// <summary>
    /// A cockpit reply is a few sentences. The SDK's own guidance is not to lowball this, but
    /// "deliberately short outputs" is the stated exception and this is one — the reply is
    /// going to be spoken aloud to someone flying a ship.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 8192;
}

/// <summary>
/// The seam. The turn loop talks to this and never to a vendor SDK, which is what keeps Core
/// free of provider references and lets an OpenAI-protocol endpoint be a first-class peer
/// rather than a port (architecture.md §6).
/// </summary>
public interface ILlmProvider
{
    /// <summary>Stable identifier used by the price table — "anthropic", "openai".</summary>
    string Id { get; }

    string DisplayName { get; }

    string DefaultModel { get; }

    LlmProviderCapabilities CapabilitiesFor(string model);

    IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken);
}
