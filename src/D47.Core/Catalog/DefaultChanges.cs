using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Catalog;

/// <summary>A published default the Commander has not been told about, and the one setting write that answers it.</summary>
/// <param name="Key">The <see cref="DefaultChanges"/> key whose told default this notice updates.</param>
/// <param name="Default">The published default now.</param>
/// <param name="ActionLabel">What the action button says.</param>
/// <param name="SettingKey">The setting the action writes.</param>
/// <param name="Value">What the action writes.</param>
public sealed record DefaultChange(
    string Key,
    string Default,
    string Text,
    string ActionLabel,
    string SettingKey,
    string Value);

/// <summary>Finds the default change to tell the Commander about, from the settings, the catalog and what they were told.</summary>
public static class DefaultChanges
{
    private const string LlmPrefix = "llm:";

    private const string SpeechPrefix = "speech:";

    private const string BackgroundPrefix = "background:";

    /// <summary>
    /// The first change to announce, or null. <paramref name="told"/> comes back recorded at the current
    /// default for every provider with nothing to announce, and unchanged for one with a notice pending.
    /// </summary>
    public static DefaultChange? Find(
        D47Settings settings,
        ModelCatalog catalog,
        ref IReadOnlyDictionary<string, string> told)
    {
        var firstRun = told.Count == 0;
        var next = new Dictionary<string, string>(told, StringComparer.Ordinal);

        foreach (var provider in new[] { LlmProviderCatalog.AnthropicId, LlmProviderCatalog.OpenAiId })
        {
            RecordFirst(next, LlmPrefix + provider, catalog.DefaultFor(provider));
        }

        RecordFirst(next, SpeechPrefix + TtsProviderCatalog.ElevenLabsId, catalog.SpeechDefaultFor(TtsProviderCatalog.ElevenLabsId));

        if (firstRun)
        {
            foreach (var provider in new[] { LlmProviderCatalog.AnthropicId, LlmProviderCatalog.OpenAiId })
            {
                RecordFirst(next, BackgroundPrefix + provider, catalog.BackgroundDefaultFor(provider));
            }
        }

        var change = LanguageModel(settings, catalog, next) ?? Speech(settings, catalog, next) ?? Background(settings, catalog, next);

        foreach (var key in next.Keys.ToArray())
        {
            if ((change is null || !Selected(settings, key)) && CurrentDefault(catalog, key) is { } current)
            {
                next[key] = current;
            }
        }

        told = next;

        return change;
    }

    private static DefaultChange? LanguageModel(D47Settings settings, ModelCatalog catalog, Dictionary<string, string> told)
    {
        var provider = settings.Llm.Provider;
        var key = LlmPrefix + provider;

        if (!Selected(settings, key)
            || catalog.DefaultFor(provider) is not { } current
            || !told.TryGetValue(key, out var was)
            || was == current)
        {
            return null;
        }

        return Build(
            key,
            current,
            was,
            settings.Llm.Model,
            id => catalog.LabelFor(provider, id),
            ConversationCapability.ModelKey,
            "D47 now answers with {0}.");
    }

    private static DefaultChange? Speech(D47Settings settings, ModelCatalog catalog, Dictionary<string, string> told)
    {
        var key = SpeechPrefix + TtsProviderCatalog.ElevenLabsId;

        if (!Selected(settings, key)
            || catalog.SpeechDefaultFor(TtsProviderCatalog.ElevenLabsId) is not { } current
            || !told.TryGetValue(key, out var was)
            || was == current)
        {
            return null;
        }

        return Build(
            key,
            current,
            was,
            settings.Speech.ElevenLabsModel,
            id => catalog.SpeechModelFor(TtsProviderCatalog.ElevenLabsId, id)?.Label ?? id,
            SpeechCapability.ElevenLabsModelKey,
            "D47 now speaks with {0}.");
    }

    private static DefaultChange? Background(D47Settings settings, ModelCatalog catalog, Dictionary<string, string> told)
    {
        var provider = settings.Llm.Provider;
        var key = BackgroundPrefix + provider;

        if (!Selected(settings, key)
            || catalog.BackgroundDefaultFor(provider) is not { } current
            || told.TryGetValue(key, out var was) && was == current)
        {
            return null;
        }

        var conversation = settings.Llm.Model ?? catalog.DefaultFor(provider);

        if (conversation is null || conversation == current)
        {
            return null;
        }

        return new DefaultChange(
            key,
            current,
            $"The quiet calls now use {catalog.LabelFor(provider, current)}.",
            $"Keep {catalog.LabelFor(provider, conversation)}",
            ConversationCapability.BackgroundModelKey,
            conversation);
    }

    private static DefaultChange? Build(
        string key,
        string current,
        string was,
        string? chosen,
        Func<string, string> label,
        string settingKey,
        string followText)
    {
        if (chosen is null)
        {
            return new DefaultChange(
                key, current, string.Format(followText, label(current)), $"Keep {label(was)}", settingKey, was);
        }

        return chosen == current
            ? null
            : new DefaultChange(
                key,
                current,
                $"{label(current)} is the new default. You are on {label(chosen)}.",
                "Use it",
                settingKey,
                current);
    }

    private static void RecordFirst(Dictionary<string, string> told, string key, string? current)
    {
        if (current is not null)
        {
            told.TryAdd(key, current);
        }
    }

    private static bool Selected(D47Settings settings, string key) =>
        key.StartsWith(BackgroundPrefix, StringComparison.Ordinal)
            ? key == BackgroundPrefix + settings.Llm.Provider
                && string.IsNullOrEmpty(settings.Llm.Endpoint)
                && settings.Llm.BackgroundModel is null
            : key.StartsWith(LlmPrefix, StringComparison.Ordinal)
            ? key == LlmPrefix + settings.Llm.Provider && string.IsNullOrEmpty(settings.Llm.Endpoint)
            : VoiceGroups.Selected(settings.Speech).Values.Any(id =>
                string.Equals(id, TtsProviderCatalog.ElevenLabsId, StringComparison.OrdinalIgnoreCase));

    private static string? CurrentDefault(ModelCatalog catalog, string key) =>
        key.StartsWith(BackgroundPrefix, StringComparison.Ordinal)
            ? catalog.BackgroundDefaultFor(key[BackgroundPrefix.Length..])
            : key.StartsWith(LlmPrefix, StringComparison.Ordinal)
            ? catalog.DefaultFor(key[LlmPrefix.Length..])
            : catalog.SpeechDefaultFor(key[SpeechPrefix.Length..]);
}
