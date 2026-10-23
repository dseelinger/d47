using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Catalog;

/// <summary>Chooses the model for calls that reason across game data.</summary>
public static class HighModels
{
    /// <summary>The catalog's high model for the selected provider; the conversation model on a custom endpoint or where the catalog names none.</summary>
    public static string? Resolve(D47Settings settings) =>
        (string.IsNullOrEmpty(settings.Llm.Endpoint)
            ? LlmProviderCatalog.Selected(settings.Llm.Provider).HighDefaultModel
            : null) ?? settings.Llm.Model;
}
