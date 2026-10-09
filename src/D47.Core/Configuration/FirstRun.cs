using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Listening;

namespace D47.Core.Configuration;

/// <summary>The three provider choices the setup wizard makes.</summary>
public enum SetupSlot
{
    Conversation,
    Voice,
    Listening,
}

/// <summary>What the setup wizard has chosen so far. Nothing is saved until <see cref="FirstRun.Apply"/>.</summary>
public sealed record SetupChoices(
    string Conversation,
    string Voice,
    string Listening,
    string? TalkKey,
    string? TalkButton,
    string TalkMode)
{
    /// <summary>The saved settings, with a provider id no catalog knows read as that catalog's fallback.</summary>
    public static SetupChoices From(D47Settings settings) => new(
        LlmProviderCatalog.Selected(settings.Llm.Provider).Id,
        TtsProviderCatalog.Selected(settings.Speech.Provider).Id,
        SttProviderCatalog.Selected(settings.Listening.Provider).Id,
        settings.Listening.PushToTalkKey,
        settings.Listening.PushToTalkButton,
        settings.Listening.Mode);

    public string For(SetupSlot slot) => slot switch
    {
        SetupSlot.Conversation => Conversation,
        SetupSlot.Voice => Voice,
        _ => Listening,
    };

    public SetupChoices With(SetupSlot slot, string provider) => slot switch
    {
        SetupSlot.Conversation => this with { Conversation = provider },
        SetupSlot.Voice => this with { Voice = provider },
        _ => this with { Listening = provider },
    };
}

/// <summary>One secret the choices need, asked for once however many of them use it.</summary>
/// <param name="Row">The registry's key row for the first slot that uses the secret.</param>
/// <param name="Serves">The slots that use it, in wizard order.</param>
/// <param name="Egress">What each of those slots sends, computed from <see cref="EgressDisclosure"/>.</param>
/// <param name="KeyPage">Where the provider issues the key, or null where there is no such page.</param>
public sealed record SetupKey(
    string SecretName,
    SettingRow Row,
    IReadOnlyList<SetupSlot> Serves,
    IReadOnlyList<EgressEntry> Egress,
    string? KeyPage);

/// <summary>The first-run setup: the providers to choose from, the keys they need, and what is saved.</summary>
public static class FirstRun
{
    public static IReadOnlyList<SetupSlot> Slots { get; } =
        [SetupSlot.Conversation, SetupSlot.Voice, SetupSlot.Listening];

    /// <summary>Where each provider issues its key, by secret name.</summary>
    public static IReadOnlyDictionary<string, string> KeyPages { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["anthropic.apiKey"] = "https://console.anthropic.com/settings/keys",
            ["openai.apiKey"] = "https://platform.openai.com/api-keys",
            [TtsProviderCatalog.ElevenLabsKeySecretName] = "https://elevenlabs.io/app/settings/api-keys",
            ["cartesia.apiKey"] = "https://play.cartesia.ai/keys",
            ["groq.apiKey"] = "https://console.groq.com/keys",
            ["deepgram.apiKey"] = "https://console.deepgram.com/",
        };

    /// <summary>Whether the guided path should open by itself.</summary>
    /// <param name="provider">
    /// The selected provider's descriptor, or null when the setting names one that no longer exists.
    /// </param>
    public static bool IsNeeded(LlmProviderInfo? provider, Func<string, bool> hasSecret)
    {
        if (provider is null)
        {
            return true;
        }

        if (!provider.NeedsKey)
        {
            return false;
        }

        return provider.KeySecretName is not { } name || !hasSecret(name);
    }

    /// <summary>The value a slot falls back to when its key was skipped.</summary>
    public static string Free(SetupSlot slot) => slot switch
    {
        SetupSlot.Conversation => LlmProviderCatalog.NoneId,
        SetupSlot.Voice => TtsProviderCatalog.EdgeId,
        _ => SttProviderCatalog.LocalId,
    };

    /// <summary>The secret a slot's chosen provider cannot work without, or null.</summary>
    public static string? SecretFor(SetupSlot slot, SetupChoices choices) => slot switch
    {
        SetupSlot.Conversation => LlmProviderCatalog.Find(choices.Conversation) is { NeedsKey: true } llm
            ? llm.KeySecretName
            : null,
        SetupSlot.Voice => TtsProviderCatalog.Selected(choices.Voice).KeySecretName,
        _ => SttProviderCatalog.Selected(choices.Listening).KeySecretName,
    };

    /// <summary>The registry row a slot's chosen provider keeps its key in.</summary>
    public static string KeyRowFor(SetupSlot slot, SetupChoices choices) => slot switch
    {
        SetupSlot.Conversation => ConversationCapability.KeyRowFor(LlmProviderCatalog.Selected(choices.Conversation)),
        SetupSlot.Voice => SpeechCapability.KeyRowFor(TtsProviderCatalog.Selected(choices.Voice)),
        _ => ListeningCapability.KeyRowFor(SttProviderCatalog.Selected(choices.Listening)),
    };

    /// <summary>The keys the choices need, each secret once, in the order of the slots that use them.</summary>
    public static IReadOnlyList<SetupKey> Keys(SettingsService settings, SetupChoices choices)
    {
        var draft = Draft(settings, choices);
        var keys = new List<SetupKey>();

        foreach (var slot in Slots)
        {
            if (SecretFor(slot, choices) is not { } secret)
            {
                continue;
            }

            var index = keys.FindIndex(key => key.SecretName == secret);

            if (index >= 0)
            {
                var known = keys[index];

                keys[index] = known with
                {
                    Serves = [.. known.Serves, slot],
                    Egress = [.. known.Egress, Egress(slot, draft)],
                };

                continue;
            }

            if (settings.Find(KeyRowFor(slot, choices)) is not { } row)
            {
                continue;
            }

            keys.Add(new SetupKey(secret, row, [slot], [Egress(slot, draft)], KeyPages.GetValueOrDefault(secret)));
        }

        return keys;
    }

    /// <summary>The choices as they will be saved: a slot whose key is not stored takes its free choice.</summary>
    public static SetupChoices Effective(SetupChoices choices, Func<string, bool> hasSecret)
    {
        var effective = choices;

        foreach (var slot in Slots)
        {
            if (SecretFor(slot, choices) is { } secret && !hasSecret(secret))
            {
                effective = effective.With(slot, Free(slot));
            }
        }

        return effective;
    }

    /// <summary>
    /// The settings as they would be with these choices saved: each row's own write, in the order
    /// <see cref="Apply"/> saves them, skipping a value already held as <see cref="SettingsService.Apply"/> does.
    /// </summary>
    public static D47Settings Draft(SettingsService settings, SetupChoices choices)
    {
        var draft = settings.Current;

        foreach (var (key, value) in Changes(choices))
        {
            if (settings.Find(key)?.Binding is { Write: { } write } binding
                && !string.Equals(binding.Read(draft), value, StringComparison.Ordinal))
            {
                draft = write(draft, value);
            }
        }

        return draft;
    }

    private static (string Key, string? Value)[] Changes(SetupChoices choices) =>
    [
        (ConversationCapability.ProviderKey, choices.Conversation),
        (SpeechCapability.ProviderKey, choices.Voice),
        (ListeningCapability.ProviderKey, choices.Listening),
        (ListeningCapability.PushToTalkKeyKey, choices.TalkKey),
        (ListeningCapability.PushToTalkButtonKey, choices.TalkButton),
        (ListeningCapability.ModeKey, choices.TalkMode),
    ];

    /// <summary>What one slot's chosen provider sends, as if its key were stored.</summary>
    public static EgressEntry Egress(SetupSlot slot, D47Settings draft) => slot switch
    {
        SetupSlot.Conversation => EgressDisclosure.Entry(EgressDisclosure.LanguageModel, draft, llmKeyPresent: true),
        SetupSlot.Voice => EgressDisclosure.TextToSpeechFor(TtsProviderCatalog.Selected(draft.Speech.Provider)),
        _ => EgressDisclosure.SpeechRecognitionFor(SttProviderCatalog.Selected(draft.Listening.Provider)),
    };

    /// <summary>Every destination that would be active once the choices are saved.</summary>
    public static IReadOnlyList<EgressEntry> Destinations(
        SettingsService settings,
        SetupChoices choices,
        Func<string, bool>? hasSecret = null)
    {
        hasSecret ??= name => settings.HasSecret(name);

        var effective = Effective(choices, hasSecret);
        var draft = Draft(settings, effective);
        var llmKey = SecretFor(SetupSlot.Conversation, effective) is { } secret && hasSecret(secret);

        return [.. EgressDisclosure
            .For(draft, llmKey, hasSecret(CommunityGoalCapability.KeySecretName))
            .Where(entry => entry.Active)];
    }

    /// <summary>Saves the effective choices, providers first, and returns any setting that refused.</summary>
    public static IReadOnlyList<SettingApplyResult> Apply(SettingsService settings, SetupChoices choices)
    {
        var effective = Effective(choices, name => settings.HasSecret(name));

        return [.. Changes(effective)
            .Select(change => settings.Apply(change.Key, change.Value, SettingsCaller.Panel))
            .Where(result => !result.Ok)];
    }
}
