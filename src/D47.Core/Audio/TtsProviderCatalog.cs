namespace D47.Core.Audio;

/// <summary>
/// What one voice provider offers and what talking to it costs in privacy. Declared as data
/// for the same reasons <see cref="Conversation.LlmProviderCatalog"/> is: the settings surface
/// shows the controls the selected provider actually has rather than a hardwired set, and the
/// egress disclosure has one place to read from (list.md Phase 4).
/// </summary>
public sealed record TtsProviderInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>How the provider row labels it.</summary>
    public required string Label { get; init; }

    /// <summary>The secret store name for this provider's key, or null if it needs none.</summary>
    public string? KeySecretName { get; init; }

    /// <summary>
    /// Exactly what leaves the machine when this provider speaks. In the second person and in
    /// full, because a disclosure that summarises is a disclosure that omits.
    /// </summary>
    public required string Egress { get; init; }

    /// <summary>Where it goes, for the disclosure's one-line form.</summary>
    public required string Destination { get; init; }

    /// <summary>
    /// The narrowest and widest speaking rate this provider will accept, in d47's normalised
    /// units where 1.0 is the voice's natural pace. Declared rather than assumed because the
    /// providers disagree sharply — Edge takes a wide percentage offset, ElevenLabs a narrow
    /// multiplier — and this is what lets one settings row mean the same thing on both.
    /// </summary>
    public double MinimumRate { get; init; } = 0.5;

    public double MaximumRate { get; init; } = 2.0;

    /// <summary>
    /// Whether speaking through this provider costs the Commander money at all.
    /// <para>
    /// Declared rather than inferred from <see cref="NeedsKey"/>, because "free" and "billed at a
    /// rate d47 does not know" have to read differently: <c>$0.00</c> from Edge and <c>$0.00</c>
    /// from an ElevenLabs run nobody has priced are the same string for opposite reasons
    /// (list.md Phase 19).
    /// </para>
    /// </summary>
    public bool Billed { get; init; }

    /// <summary>
    /// The published list price in US dollars per thousand characters, or null where the
    /// provider does not publish one. The default for the settings row, never a claim about what
    /// this Commander is actually paying — see <see cref="SpeechSpend"/> for why those differ.
    /// </summary>
    public decimal? ListDollarsPerThousandCharacters { get; init; }

    /// <summary>
    /// Whether this provider's voice ids mean nothing to a person, so one must never be shown in
    /// place of a voice's name.
    /// <para>
    /// Edge names a voice <c>en-US-AndrewMultilingualNeural</c>: not pretty, but it says who it
    /// is, and falling back to it tells the Commander something. ElevenLabs names one
    /// <c>JBFqnCBsd6RMkjVDRZzb</c>, which says nothing whatever — and that string appeared in the
    /// Voice row, under a label promising "which voice the core aboard speaks in". A row that
    /// answers a question about a voice with twenty characters of base62 has not answered it.
    /// </para>
    /// <para>
    /// A property of the provider rather than a guess at the shape of the string, because that is
    /// where the fact lives: the same reason <see cref="Billed"/> is declared rather than inferred
    /// from <see cref="NeedsKey"/>.
    /// </para>
    /// </summary>
    public bool VoiceIdsAreOpaque { get; init; }

    public bool NeedsKey => KeySecretName is not null;

    /// <summary>
    /// Whether this provider can say anything right now. Both halves matter: a provider with no
    /// key is configured but off, which is a capability being off rather than a failure to
    /// handle (list.md Phase 3).
    /// </summary>
    public bool Speaks => Id != TtsProviderCatalog.NoneId;
}

/// <summary>
/// The voice providers d47 ships. One list, read by the provider row, the key row, the rate
/// row, the egress disclosure and the app's provider construction — so a provider cannot exist
/// in one of those and be missing from another.
/// </summary>
public static class TtsProviderCatalog
{
    public const string NoneId = "none";

    public const string EdgeId = "edge";

    public const string ElevenLabsId = "elevenlabs";

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

        // ElevenLabs rejects a speed outside this outright rather than clamping, so the range is
        // declared here and the settings row narrows to it while this provider is selected.
        MinimumRate = 0.7,
        MaximumRate = 1.2,

        Billed = true,

        // The published API list price for eleven_flash_v2_5, which is the model d47 pins —
        // $0.05 per 1,000 characters, half the Multilingual 2 rate this used to read. From
        // elevenlabs.io/pricing/api, read on 2026-08-16 for Turbo 2.5 and again on 2026-08-25
        // when the pin moved to Flash 2.5, which ElevenLabs bills at the same rate. Neither move
        // was made for price: the first was language enforcement and the second was Turbo being
        // deprecated. The figure has not changed through either.
        //
        // A list price and not a bill. A subscription burns bundled credits instead — 121,000 a
        // month for $22 on Creator, so an effective $0.18 per thousand until the bundle runs out
        // and nothing at the margin before that — and the API reports neither the tier nor the
        // arrangement. So this is the row's default and the row is editable, in the same spirit
        // as the model price table declining to model introductory pricing it cannot date.
        ListDollarsPerThousandCharacters = 0.05m,
    };

    /// <summary>Every provider, in the order the row offers them. "None" last, like the LLM row.</summary>
    public static IReadOnlyList<TtsProviderInfo> All { get; } = [Edge, ElevenLabs, None];

    /// <summary>
    /// The provider this id names, or <see cref="Edge"/> if it names none. Unknown rather than
    /// invalid, like the persona row: a settings file naming a provider d47 no longer ships
    /// should start the app with a voice, not fail to start.
    /// </summary>
    public static TtsProviderInfo Selected(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Edge;
}
