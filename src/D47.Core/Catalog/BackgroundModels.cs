using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Catalog;

/// <summary>Chooses the model for background calls.</summary>
public static class BackgroundModels
{
    /// <summary>The Commander's choice, else the catalog's background default, else the conversation model.</summary>
    public static string? Resolve(D47Settings settings) =>
        settings.Llm.BackgroundModel ?? DefaultFor(settings) ?? settings.Llm.Model;

    /// <summary>The catalog's background model for the selected provider; null on a custom endpoint or where the catalog names none.</summary>
    public static string? DefaultFor(D47Settings settings) =>
        string.IsNullOrEmpty(settings.Llm.Endpoint)
            ? LlmProviderCatalog.Selected(settings.Llm.Provider).BackgroundDefaultModel
            : null;
}
