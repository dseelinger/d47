using D47.Core.Capabilities;
using D47.Core.Conversation;

namespace D47.Core.Configuration;

/// <summary>
/// One step of the guided key setup: which row to show, and what that key sends where.
/// </summary>
/// <param name="Row">
/// The real descriptor row, looked up from the registry rather than re-authored. The settings
/// surface is descriptor-driven, and a parallel copy of these fields is a second thing to keep in
/// step — and the one that drifts.
/// </param>
/// <param name="Required">
/// Whether skipping it leaves d47 unable to hold a conversation. Exactly one step is required and
/// it is still skippable: capabilities are state rather than guards (list.md Phase 3), so
/// declining every key leaves d47 running and typed rather than a dead app behind a form.
/// </param>
/// <param name="Egress">
/// What this key causes to leave the machine, computed from <see cref="EgressDisclosure"/> rather
/// than written out here.
/// </param>
public sealed record FirstRunStep(SettingRow Row, bool Required, EgressEntry? Egress)
{
    /// <summary>Whether the key behind this step is already stored.</summary>
    public bool Satisfied { get; init; }
}

/// <summary>
/// The guided path a fresh install needs before d47 can answer anything (list.md Phase 16, "Ask
/// for the keys on the first run that needs them").
/// <para>
/// <b>Driven by state, never by a "have we done this?" flag.</b> The condition is <em>there is no
/// usable language-model key</em>, and that is deliberately also true for a Commander who restored
/// a <c>data\</c> folder onto a new machine: <c>secrets.json</c> is DPAPI-scoped, and
/// <see cref="SecretStore"/> treats a value it cannot decrypt as absent and says so in the log. A
/// flag would show this once on a machine that could not read its own secrets, and never again —
/// which is the machine that needs it most.
/// </para>
/// <para>
/// Nothing here is a wall, and nothing here is stored. This type answers "what should be asked
/// for, and is it still needed" from the state that already exists.
/// </para>
/// </summary>
public static class FirstRun
{
    /// <summary>
    /// Whether the guided path should open by itself. The one condition, asked of live state.
    /// </summary>
    /// <param name="provider">
    /// The selected provider's descriptor, or null when the setting names one that no longer
    /// exists — which is itself a reason to guide, since nothing will work until it is changed.
    /// </param>
    /// <param name="hasSecret">Whether a named secret has a value that decrypted.</param>
    public static bool IsNeeded(LlmProviderInfo? provider, Func<string, bool> hasSecret)
    {
        if (provider is null)
        {
            return true;
        }

        // A provider that needs no key is a complete configuration on its own — a local model, or
        // one that authenticates some other way. Guiding somebody to paste a key they do not need
        // is how a first run teaches that d47 asks for things it does not use.
        if (!provider.NeedsKey)
        {
            return false;
        }

        return provider.KeySecretName is not { } name || !hasSecret(name);
    }

    /// <summary>
    /// The steps to walk, in order: the language-model key first because it is the one that
    /// decides whether d47 can answer at all, then the optional ones.
    /// <para>
    /// Rows are matched by key against the registry, and a key that matches nothing is skipped
    /// rather than throwing. A capability can be absent — Inara's arrives in a later phase — and
    /// a guided path that crashes because an optional row has not been written yet is worse than
    /// one that guides through what exists.
    /// </para>
    /// </summary>
    /// <param name="languageModelKeyRow">
    /// The row key for the selected provider's API key. Passed in rather than derived, so this
    /// type needs no reference to the capability that happens to declare it — the same reason
    /// <paramref name="optionalKeys"/> is a parameter.
    /// </param>
    /// <param name="optionalKeys">
    /// Row keys for the keys worth offering but not needing, in the order to offer them. Passed in
    /// rather than hardcoded, so the phase that adds Inara adds it at the call site with the row.
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

        // Worked out once, here, and handed to every step. EgressDisclosure asks "is there a
        // language-model key" to decide whether the model destination is live, and the answer is
        // a property of the selected provider rather than of any one row.
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
        // Computed, never written beside the row. The privacy section already derives its lines
        // this way and for the same reason: hand-written copy about what leaves the machine goes
        // stale, and this is the one screen where a Commander is deciding whether to trust it.
        //
        // Computed as though the key were already stored, which is the whole difference between
        // this screen and the privacy section. There, the question is "what is leaving right
        // now", and with no key the honest answer is "nothing". Here the question is "what will
        // leave if I paste this in", and answering it with "nothing is sent" is true, useless,
        // and answers a question nobody asked — the Commander is deciding precisely whether to
        // create the condition that makes it send.
        var egress = row.EgressId is { } id
            ? EgressDisclosure.Entry(id, settings, llmKeyPresent: true)
            : null;

        return new FirstRunStep(row, required, egress) { Satisfied = satisfied };
    }
}
