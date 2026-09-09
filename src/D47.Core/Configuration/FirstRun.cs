using D47.Core.Capabilities;
using D47.Core.Conversation;

namespace D47.Core.Configuration;

/// <summary>One step of the guided key setup: which row to show, and what that key sends where.</summary>
/// <param name="Row">
/// The real descriptor row, looked up from the registry rather than re-authored.
/// </param>
/// <param name="Required">Whether skipping it leaves d47 unable to hold a conversation.</param>
/// <param name="Egress">
/// What this key causes to leave the machine, computed from <see cref="EgressDisclosure"/> rather than
/// written out here.
/// </param>
public sealed record FirstRunStep(SettingRow Row, bool Required, EgressEntry? Egress)
{
    /// <summary>Whether the key behind this step is already stored.</summary>
    public bool Satisfied { get; init; }
}

/// <summary>
/// The guided path a fresh install needs before d47 can answer anything (Phase 16, "Ask for the keys on
/// the first run that needs them").
/// </summary>
public static class FirstRun
{
    /// <summary>Whether the guided path should open by itself.</summary>
    /// <param name="provider">
    /// The selected provider's descriptor, or null when the setting names one that no longer exists —
    /// which is itself a reason to guide, since nothing will work until it is changed.
    /// </param>
    /// <param name="hasSecret">Whether a named secret has a value that decrypted.</param>
    public static bool IsNeeded(LlmProviderInfo? provider, Func<string, bool> hasSecret)
    {
        if (provider is null)
        {
            return true;
        }

        // A provider that needs no key is a complete configuration on its own — a local model, or one that
        // authenticates some other way.
        if (!provider.NeedsKey)
        {
            return false;
        }

        return provider.KeySecretName is not { } name || !hasSecret(name);
    }

    /// <summary>
    /// The steps to walk, in order: the language-model key first because it is the one that decides
    /// whether d47 can answer at all, then the optional ones.
    /// </summary>
    /// <param name="languageModelKeyRow">
    /// The row key for the selected provider's API key.
    /// </param>
    /// <param name="optionalKeys">
    /// Row keys for the keys worth offering but not needing, in the order to offer them.
    /// </param>
    public static IReadOnlyList<FirstRunStep> Steps(
        CapabilityRegistry registry,
        D47Settings settings,
        LlmProviderInfo? provider,
        Func<string, bool> hasSecret,
        string? languageModelKeyRow,
        IReadOnlyList<string> optionalKeys)
    {
        var steps = new List<FirstRunStep>();
        var rows = registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        // Worked out once, here, and handed to every step.
        var llmKeyPresent =
            provider is { NeedsKey: true, KeySecretName: { } secret } && hasSecret(secret);

        if (provider is { NeedsKey: true }
            && languageModelKeyRow is not null
            && rows.TryGetValue(languageModelKeyRow, out var keyRow))
        {
            steps.Add(Step(keyRow, required: true, settings, satisfied: llmKeyPresent));
        }

        foreach (var key in optionalKeys)
        {
            if (rows.TryGetValue(key, out var row))
            {
                steps.Add(Step(
                    row,
                    required: false,
                    settings,
                    satisfied: row.SecretName is { } name && hasSecret(name)));
            }
        }

        return steps;
    }

    private static FirstRunStep Step(
        SettingRow row,
        bool required,
        D47Settings settings,
        bool satisfied)
    {
        // Computed, never written beside the row.
        var egress = row.EgressFor is { } own
            ? own(settings)
            : row.EgressId is { } id
                ? EgressDisclosure.Entry(id, settings, llmKeyPresent: true)
                : null;

        return new FirstRunStep(row, required, egress) { Satisfied = satisfied };
    }
}
