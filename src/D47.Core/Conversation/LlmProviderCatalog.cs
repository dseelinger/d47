namespace D47.Core.Conversation;

/// <summary>
/// What one provider offers and what talking to it costs in privacy. Declared as data so the
/// settings surface can show the controls the selected provider actually has rather than a
/// hardwired set, and so the egress disclosure has one place to read from (list.md Phase 4).
/// </summary>
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

    /// <summary>
    /// The models d47 knows about at <see cref="DefaultEndpoint"/>. Deliberately not "the
    /// models that exist": every id here is one the price table can quote, so the running
    /// total stays honest for anything chosen from the list. Anything else is reachable by
    /// typing it, and is priced as unknown rather than as free.
    /// </summary>
    public IReadOnlyList<string> Models { get; init; } = [];

    /// <summary>
    /// Exactly what leaves the machine when this provider answers a turn. Written in the
    /// second person and in full, because a disclosure that summarises is a disclosure that
    /// omits (list.md Phase 4, "Say what each provider receives").
    /// </summary>
    public required string Egress { get; init; }

    public bool HasEndpoint => DefaultEndpoint is not null;

    public bool NeedsKey => KeySecretName is not null;

    /// <summary>
    /// The model list for an endpoint. A custom endpoint gets an empty list rather than this
    /// provider's: model ids belong to the endpoint's namespace, and offering
    /// <c>claude-opus-5</c> for someone else's gateway is a stale selection waiting to fail at
    /// the first turn. Empty is a supported state — the picker still lets you type one.
    /// </summary>
    public IReadOnlyList<string> ModelsFor(string? endpoint) =>
        endpoint is null || string.Equals(endpoint, DefaultEndpoint, StringComparison.OrdinalIgnoreCase)
            ? Models
            : [];
}

/// <summary>
/// The providers d47 can be pointed at. "none" is a first-class member, not an absence: the
/// keyword router answers every input path with no model at all, so local-only operation is a
/// configuration rather than a degraded state (list.md Phase 3, Phase 4).
/// </summary>
public static class LlmProviderCatalog
{
    public const string NoneId = "none";

    public const string AnthropicId = "anthropic";

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
            DefaultModel = "claude-opus-5",
            Models = ["claude-opus-5", "claude-opus-4-8", "claude-sonnet-5", "claude-haiku-4-5", "claude-fable-5"],
            Egress =
                "Your question, d47's reply so far, the guardrails, the persona and your About Me text, and the " +
                "game state d47 assembled from your journal — system, body, station and docking state — are sent " +
                "to the endpoint below on every turn the model answers. Journal files themselves are never uploaded.",
        },
    ];

    public static IReadOnlyList<string> Ids { get; } = [.. All.Select(p => p.Id)];

    public static LlmProviderInfo? Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The selected provider, falling back to "none" rather than to a default that sends.</summary>
    public static LlmProviderInfo Selected(string? id) => Find(id) ?? All[0];
}
