namespace D47.Core.Conversation;

/// <summary>What one provider offers and what talking to it costs in privacy.</summary>
public sealed record LlmProviderInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Summary { get; init; }

    /// <summary>The secret store name for this provider's key, or null if it needs none.</summary>
    public string? KeySecretName { get; init; }

    /// <summary>Null when the provider has no endpoint to point somewhere else.</summary>
    public string? DefaultEndpoint { get; init; }

    public string? DefaultModel { get; init; }

    /// <summary>The models d47 knows about at <see cref="DefaultEndpoint"/>.</summary>
    public IReadOnlyList<string> Models { get; init; } = [];

    /// <summary>Exactly what leaves the machine when this provider answers a turn.</summary>
    public required string Egress { get; init; }

    public bool HasEndpoint => DefaultEndpoint is not null;

    /// <summary>Whether pointing this provider somewhere else is a thing anyone would do.</summary>
    public bool AcceptsCustomEndpoint { get; init; }

    /// <summary>
    /// Whether the key row exists but the endpoint may be reached without filling it in (Phase 29).
    /// </summary>
    public bool KeyOptional { get; init; }

    /// <summary>Whether there is a key row at all, filled in or not.</summary>
    public bool AcceptsKey => KeySecretName is not null;

    public bool NeedsKey => KeySecretName is not null && !KeyOptional;

    /// <summary>The model list for an endpoint.</summary>
    public IReadOnlyList<string> ModelsFor(string? endpoint) =>
        endpoint is null || string.Equals(endpoint, DefaultEndpoint, StringComparison.OrdinalIgnoreCase)
            ? Models
            : [];
}

/// <summary>
/// The providers d47 can be pointed at. "none" is a first-class member, not an absence: the keyword
/// router answers every input path with no model at all, so local-only operation is a configuration
/// rather than a degraded state (Phase 3, Phase 4).
/// </summary>
public static class LlmProviderCatalog
{
    public const string NoneId = "none";

    public const string AnthropicId = "anthropic";

    /// <summary>OpenAI's own endpoint, over the Responses API (Phase 29).</summary>
    public const string OpenAiId = "openai";

    /// <summary>
    /// Anything else speaking OpenAI's older protocol at its own address — Ollama, LM Studio, vLLM,
    /// llama.cpp, or a gateway.
    /// </summary>
    public const string OpenAiCompatibleId = "openaiCompatible";

    /// <summary>Ollama on its usual port, which is the local server most Commanders will have.</summary>
    public const string CompatibleDefaultEndpoint = "http://127.0.0.1:11434/v1";

    public static IReadOnlyList<LlmProviderInfo> All { get; } =
    [
        new LlmProviderInfo
        {
            Id = NoneId,
            Name = "None (local only)",
            Summary = "No language model. The keyword router answers what it recognises and says so when it cannot.",
            Egress = "Nothing. No turn text, journal content or game state leaves this machine.",
        },
        new LlmProviderInfo
        {
            Id = AnthropicId,
            Name = "Anthropic",
            Summary = "Claude models, over the Anthropic Messages API.",
            KeySecretName = "anthropic.apiKey",
            DefaultEndpoint = "https://api.anthropic.com",
            // The highest Sonnet, not the highest model.
            DefaultModel = "claude-sonnet-5",
            Models = ["claude-sonnet-5", "claude-opus-5", "claude-opus-4-8", "claude-haiku-4-5", "claude-fable-5"],
            Egress =
                "Your question, D47's reply so far, the guardrails, the persona, your About Me text, any standing " +
                "directions you have adopted, and the few " +
                "facts D47 remembers about you that are relevant right now, and the game state D47 assembled from " +
                "your journal — system, body, station and docking state — are sent to the endpoint below on every " +
                "turn the model answers. Journal files themselves are never uploaded.",
        },
        new LlmProviderInfo
        {
            Id = OpenAiId,
            Name = "OpenAI",
            Summary = "GPT models, over the OpenAI Responses API.",
            KeySecretName = "openai.apiKey",
            DefaultEndpoint = "https://api.openai.com/v1",

            // The middle tier, for the reason the Anthropic row gives: a companion answers short questions
            // about a game while the Commander is flying, and the top tier is priced for work that is harder
            // than that.
            DefaultModel = "gpt-5.6-terra",

            // Every id here is one the price table can quote, which is what the field's contract requires.
            Models = ["gpt-5.6-terra", "gpt-5.6-sol", "gpt-5.6-luna", "gpt-5.5", "gpt-5.4-mini", "gpt-5.4-nano"],

            // Responses is spoken at its own address by xAI and OpenRouter as well, so reaching Grok is a
            // base URL and a model name rather than a third implementation.
            AcceptsCustomEndpoint = true,
            Egress =
                "Your question, D47's reply so far, the guardrails, the persona, your About Me text, any standing " +
                "directions you have adopted, and the few " +
                "facts D47 remembers about you that are relevant right now, and the game state D47 assembled from " +
                "your journal — system, body, station and docking state — are sent to the endpoint below on every " +
                "turn the model answers. Journal files themselves are never uploaded.",
        },
        new LlmProviderInfo
        {
            Id = OpenAiCompatibleId,
            Name = "OpenAI-compatible endpoint",
            Summary =
                "A model you run yourself — Ollama, LM Studio, vLLM, llama.cpp — or any gateway speaking the " +
                "OpenAI Chat Completions protocol. Point it at the address and say which model.",
            KeySecretName = "openaiCompatible.apiKey",

            // The change this entry exists for.
            KeyOptional = true,
            DefaultEndpoint = CompatibleDefaultEndpoint,

            // No default model and no list: this endpoint serves whatever was loaded into it, and a guess
            // would fail at the first turn.
            AcceptsCustomEndpoint = true,
            Egress =
                "Your question, D47's reply so far, the guardrails, the persona, your About Me text, any standing " +
                "directions you have adopted, and the few " +
                "facts D47 remembers about you that are relevant right now, and the game state D47 assembled from " +
                "your journal — system, body, station and docking state — are sent to the endpoint below on every " +
                "turn the model answers. Journal files themselves are never uploaded. Where that endpoint is on " +
                "this machine, none of it leaves the machine at all; where it is not, it goes to whoever runs that " +
                "address. This endpoint has no server-side web search, so D47 never asks it to fetch a page.",
        },
    ];

    public static IReadOnlyList<string> Ids { get; } = [.. All.Select(p => p.Id)];

    public static LlmProviderInfo? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The selected provider, falling back to "none" rather than to a default that sends.</summary>
    public static LlmProviderInfo Selected(string? id) => Find(id) ?? All[0];
}
