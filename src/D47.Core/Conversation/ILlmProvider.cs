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

    /// <summary>
    /// Whether this endpoint can run a web search on the model's behalf and hand the results
    /// back inside the same turn.
    /// <para>
    /// Both provider- and model-gated, and the two gates fail for different reasons. The
    /// <em>provider</em> gate is the endpoint: a value in <c>llm.endpoint</c> points at a
    /// gateway or a proxy, and whether that thing offers a server-side tool is not something
    /// d47 can know — Bedrock does not, Google Cloud offers only the basic variant, and a
    /// request that declares an unsupported tool fails outright rather than degrading. The
    /// <em>model</em> gate is the variant: dynamic filtering needs Claude 4.6 or later, so
    /// <c>claude-haiku-4-5</c> gets the basic tool instead of nothing.
    /// </para>
    /// <para>
    /// False is a capability that is off, not a failure — the settings row says so and the turn
    /// carries on, which is the same treatment every other absent capability gets
    /// (architecture.md §6).
    /// </para>
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
    /// How many web searches the provider ran for this turn. An <c>init</c> property rather
    /// than a fifth positional field so that every existing construction still reads as the
    /// four token counts it always was.
    /// <para>
    /// Counted because it is <em>billed separately from tokens</em> and is not small: one
    /// search costs more than an entire cheap turn, so a running total that saw only tokens
    /// would understate a searching turn by more than it reported.
    /// </para>
    /// </summary>
    public int WebSearchRequests { get; init; }

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
    /// The provider stopped a long server-side turn part-way and is willing to resume it.
    /// <para>
    /// Distinguished from <see cref="Completed"/> deliberately, because the difference is
    /// invisible in the text: a paused turn is a sentence that simply stops, and reporting it as
    /// an answer is reporting a truncation as a fact. d47 does not resume — resuming means
    /// sending the paused assistant message back <em>unchanged</em>, and its server-side blocks
    /// have no representation in <see cref="ConversationContent"/> — so the turn ends here and
    /// says that it did. Capping searches keeps it rare rather than papering over it.
    /// </para>
    /// </summary>
    Paused,

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

    /// <summary>
    /// Whether the provider may search the web for this turn.
    /// <para>
    /// Set per request rather than read from settings at the seam, because the seam has no
    /// settings — but it is a value that must stay <em>stable across a session</em> all the
    /// same. The declaration serialises with the tools, in prompt position 1, so flipping it
    /// between turns rewrites the cached prefix exactly as adding a tool would
    /// (architecture.md §6). It changes when the Commander changes the setting, which is one
    /// cache miss for one deliberate act, and on the round that offers no tools at all — which
    /// already rewrites that position.
    /// </para>
    /// </summary>
    public bool WebSearch { get; init; }
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
