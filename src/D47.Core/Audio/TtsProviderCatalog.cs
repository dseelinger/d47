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
        Egress = "The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, "
                 + "along with your API key. That includes re-voiced in-game messages when you have "
                 + "turned those on, which are written by other players. No journal content, game "
                 + "state or other keys are sent.",

        // ElevenLabs rejects a speed outside this outright rather than clamping, so the range is
        // declared here and the settings row narrows to it while this provider is selected.
        MinimumRate = 0.7,
        MaximumRate = 1.2,
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
