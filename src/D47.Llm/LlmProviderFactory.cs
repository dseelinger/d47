using D47.Core.Conversation;

namespace D47.Llm;

/// <summary>
/// The one place a catalog entry becomes a client (Phase 29).
/// <para>
/// There were two, and they had already drifted into being identical by accident: both switched
/// on the provider id, both fell through to <c>null</c>, and both then said <em>"D47 has no
/// client for {Name} yet."</em> in their own copy of the sentence. Two is a coincidence. Three
/// providers would have made it a bug — the kind where a provider works when d47 starts and is
/// unreachable when the Commander verifies its key, or the reverse.
/// </para>
/// <para>
/// It lives here rather than in the app because constructing a client means naming one, and
/// naming one means referencing this assembly. Core defines the seam and knows none of these
/// types, which is what <c>CoreDependencyTests</c> asserts.
/// </para>
/// </summary>
public static class LlmProviderFactory
{
    /// <summary>
    /// A client for <paramref name="provider"/>, or null if d47 has no implementation for it.
    /// <para>
    /// <paramref name="apiKey"/> is null when nothing is stored, which is a complete
    /// configuration for a provider whose key is optional and a non-starter for one whose key is
    /// not — and that judgement is the caller's, because the caller is the one that can say why.
    /// <see cref="ReasonForNoClient"/> supplies the sentence either way.
    /// </para>
    /// </summary>
    public static ILlmProvider? Create(LlmProviderInfo provider, string? apiKey, string? endpoint)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.NeedsKey && string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return provider.Id switch
        {
            LlmProviderCatalog.AnthropicId => new AnthropicLlmProvider(apiKey ?? string.Empty, endpoint),
            LlmProviderCatalog.OpenAiId => new OpenAi.ResponsesLlmProvider(apiKey, endpoint),
            LlmProviderCatalog.OpenAiCompatibleId => new OpenAi.ChatCompletionsLlmProvider(apiKey, endpoint),
            _ => null,
        };
    }

    /// <summary>
    /// Why <see cref="Create"/> returned null, in the Commander's terms. Kept beside the switch
    /// it explains, so the two cannot disagree about which case a provider is in.
    /// </summary>
    public static string ReasonForNoClient(LlmProviderInfo provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider.NeedsKey
            ? $"No {provider.Name} API key is stored. Add one in Settings."
            : $"D47 has no client for {provider.Name} yet.";
    }
}
