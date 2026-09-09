using D47.Core.Conversation;

namespace D47.Llm;

/// <summary>The one place a catalog entry becomes a client (Phase 29).</summary>
public static class LlmProviderFactory
{
    /// <summary>A client for <paramref name="provider"/>, or null if d47 has no implementation for it.</summary>
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

    /// <summary>Why <see cref="Create"/> returned null, in the Commander's terms.</summary>
    public static string ReasonForNoClient(LlmProviderInfo provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider.NeedsKey
            ? $"No {provider.Name} API key is stored. Add one in Settings."
            : $"D47 has no client for {provider.Name} yet.";
    }
}
