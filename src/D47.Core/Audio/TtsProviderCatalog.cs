namespace D47.Core.Audio;

/// <summary>What one voice provider offers and what talking to it costs in privacy.</summary>
public sealed record TtsProviderInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>How the provider row labels it.</summary>
    public required string Label { get; init; }

    /// <summary>The secret store name for this provider's key, or null if it needs none.</summary>
    public string? KeySecretName { get; init; }

    /// <summary>Exactly what leaves the machine when this provider speaks.</summary>
    public required string Egress { get; init; }

    /// <summary>Where it goes, for the disclosure's one-line form.</summary>
    public required string Destination { get; init; }

    /// <summary>
    /// The narrowest and widest speaking rate this provider will accept, in d47's normalised units
    /// where 1.0 is the voice's natural pace.
    /// </summary>
    public double MinimumRate { get; init; } = 0.5;

    public double MaximumRate { get; init; } = 2.0;

    /// <summary>Whether speaking through this provider costs the Commander money at all.</summary>
    public bool Billed { get; init; }

    /// <summary>
    /// The published list price in US dollars per thousand characters, or null where the provider does
    /// not publish one.
    /// </summary>
    public decimal? ListDollarsPerThousandCharacters { get; init; }

    /// <summary>
    /// The same thing per minute of audio, for a provider whose bill is not a function of the
    /// characters handed over.
    /// </summary>
    public decimal? ListDollarsPerMinute { get; init; }

    /// <summary>Which measure this provider's bill is a function of.</summary>
    public bool BilledByMinute => ListDollarsPerMinute is not null;

    /// <summary>
    /// Whether this provider's voice ids mean nothing to a person, so one must never be shown in place
    /// of a voice's name.
    /// </summary>
    public bool VoiceIdsAreOpaque { get; init; }

    /// <summary>Whether this provider can be told which language to speak (Phase 58).</summary>
    public bool LanguageCanBePinned { get; init; } = true;

    /// <summary>
    /// Whether the voice list is known without asking, so listing it proves nothing about a key (Phase
    /// 58).
    /// </summary>
    public bool VoicesAreStatic { get; init; }

    /// <summary>
    /// Whether telling this provider to speak faster or slower actually changes the audio (Phase 60).
    /// </summary>
    public bool RateCanBeSet { get; init; } = true;

    /// <summary>Why d47 quotes no rate for a provider that charges, or null where it quotes one.</summary>
    public string? NoRateBecause { get; init; }

    public bool NeedsKey => KeySecretName is not null;

    /// <summary>Whether this provider can say anything right now.</summary>
    public bool Speaks => Id != TtsProviderCatalog.NoneId;
}

/// <summary>The voice providers d47 ships.</summary>
public static class TtsProviderCatalog
{
    public const string NoneId = "none";

    public const string EdgeId = "edge";

    public const string ElevenLabsId = "elevenlabs";

    public const string OpenAiId = "openai";

    public const string CartesiaId = "cartesia";

    public const string KokoroId = "kokoro";

    public static TtsProviderInfo None { get; } = new()
    {
        Id = NoneId,
        Name = "None",
        Label = "None — do not speak",
        Destination = "nothing sent",
        Egress = "No voice provider is selected, so no text is sent anywhere to be spoken. "
                 + "Audio cues and the thinking bed still play; they are files on this machine.",
    };

    public static TtsProviderInfo Edge { get; } = new()
    {
        Id = EdgeId,
        Name = "Edge Neural",
        Label = "Edge Neural (free)",
        Destination = "speech.platform.bing.com",
        Egress = "The text of every line D47 speaks is sent to Microsoft's Edge Read Aloud service to "
                 + "be turned into audio. That includes re-voiced in-game messages when you have "
                 + "turned those on, which are written by other players. No game state, no journal "
                 + "content and no keys are sent, and no account is involved.",
    };

    public static TtsProviderInfo ElevenLabs { get; } = new()
    {
        Id = ElevenLabsId,
        Name = "ElevenLabs",
        Label = "ElevenLabs (paid — needs a key)",
        KeySecretName = "elevenlabs.apiKey",
        Destination = "api.elevenlabs.io",

        // "JBFqnCBsd6RMkjVDRZzb" is a real one, and it is what the Voice row showed.
        VoiceIdsAreOpaque = true,
        Egress = "The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, "
                 + "along with your API key. That includes re-voiced in-game messages when you have "
                 + "turned those on, which are written by other players. No journal content, game "
                 + "state or other keys are sent.",

        // ElevenLabs rejects a speed outside this outright rather than clamping, so the range is declared
        // here and the settings row narrows to it while this provider is selected.
        MinimumRate = 0.7,
        MaximumRate = 1.2,

        Billed = true,

        // The published API list price for eleven_flash_v2_5, which is the model d47 pins — $0.05 per 1,000
        // characters, half the Multilingual 2 rate this used to read.
        ListDollarsPerThousandCharacters = 0.05m,
    };

    public static TtsProviderInfo OpenAi { get; } = new()
    {
        Id = OpenAiId,
        Name = "OpenAI",
        Label = "OpenAI (paid — needs a key)",

        // The same secret the language-model provider uses.
        KeySecretName = "openai.apiKey",
        Destination = "api.openai.com",
        Egress = "The text of every line D47 speaks through this slot is sent to OpenAI to be turned "
                 + "into audio, along with your API key — the same key the language model uses if you "
                 + "have set one. No journal content, game state or other keys are sent.",

        // It cannot be told a language, so it is never offered for a slot carrying somebody else's words.
        LanguageCanBePinned = false,

        // Thirteen built-ins and no voices endpoint, so the list needs no key and proves nothing about one.
        VoicesAreStatic = true,

        // Measured 2026-08-26 across the documented range, and honoured
        // (docs/spikes/openai-tts-language-and-speed.md §3).
        MinimumRate = 0.25,
        MaximumRate = 4.0,

        Billed = true,

        // Not priced by the character, and that is still the finding rather than an omission (Phase 58).
        ListDollarsPerThousandCharacters = null,

        // $0.015 per minute of audio. **This is a proxy and is recorded as one**, which is the half of #63
        // worth reading before trusting the figure.
        ListDollarsPerMinute = 0.015m,
    };

    public static TtsProviderInfo Cartesia { get; } = new()
    {
        Id = CartesiaId,
        Name = "Cartesia",
        Label = "Cartesia (paid — needs a key)",
        KeySecretName = "cartesia.apiKey",
        Destination = "api.cartesia.ai",
        Egress = "The text of every line D47 speaks through this slot is sent to Cartesia to be "
                 + "turned into audio, along with your API key. That includes re-voiced in-game "
                 + "messages when you have turned those on, which are written by other players. "
                 + "No journal content, game state or other keys are sent.",

        // Opaque, the way ElevenLabs' are: measured 2026-08-26 across all 924
        // (docs/spikes/cartesia-voices-and-speed.md §1).
        VoiceIdsAreOpaque = true,

        // It takes a `language` field and holds it, so unlike OpenAI it is eligible for the four slots
        // carrying other people's words — the second provider after ElevenLabs that can be.
        LanguageCanBePinned = true,

        // Measured, not read: `speed` validates to [-1.0, 1.0] inside `voice.__experimental_controls` and
        // moves nothing beyond the instrument's own noise.
        RateCanBeSet = false,

        Billed = true,

        // Not published, and not for the reason OpenAI's is not.
        ListDollarsPerThousandCharacters = null,
        NoRateBecause =
            "Cartesia's API publishes neither a rate nor a balance — /balance, /usage, "
            + "/subscriptions/current and /account are all 404 — and the price page has not been "
            + "read. Set the rate in Settings once it has.",
    };

    /// <summary>The local voice (Phase 59), and the only entry here that sends nothing anywhere.</summary>
    public static TtsProviderInfo Kokoro { get; } = new()
    {
        Id = KokoroId,
        Name = "Kokoro",
        Label = "Kokoro (free — runs on this machine)",
        Destination = "nothing sent",
        Egress =
            "Nothing is sent anywhere. The voice runs on this computer, so the text D47 speaks — "
            + "including re-voiced in-game messages written by other players — never leaves it. The "
            + "model is downloaded once, from huggingface.co, and after that this slot needs no "
            + "network at all.",

        // Free, and free in the way None is rather than the way Edge is: there is no service.
        Billed = false,

        // The voices are files on disk, so listing them proves nothing about a credential — there is no
        // credential.
        VoicesAreStatic = true,

        // The ids say who they are — af_heart, bm_george — so they are not the opaque kind that must never be
        // shown in place of a name.
        VoiceIdsAreOpaque = false,

        // Eligible for the slots carrying other people's words, and the flag needs its reasoning stated
        // rather than copied. The rule exists because a provider that cannot be told a language FOLLOWS
        // the text's language, so a French message would be read as French in a voice chosen for English.
        LanguageCanBePinned = true,

        // The model takes a speed input and the spike measured it moving the audio, unlike Cartesia's.
        RateCanBeSet = true,
    };

    /// <summary>Every provider, in the order the row offers them. "None" last, like the LLM row.</summary>
    public static IReadOnlyList<TtsProviderInfo> All { get; } =
        [Edge, Kokoro, ElevenLabs, OpenAi, Cartesia, None];

    /// <summary>The providers that may speak for one slot.</summary>
    public static IReadOnlyList<TtsProviderInfo> For(VoiceGroupInfo slot) =>
        slot.OtherPeoplesWords ? [.. All.Where(provider => provider.LanguageCanBePinned)] : All;

    /// <summary>The provider this id names, or <see cref="Edge"/> if it names none.</summary>
    public static TtsProviderInfo Selected(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Edge;
}
