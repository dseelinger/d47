namespace D47.Core.Audio;

/// <summary>Which ElevenLabs model speaks, and what each one can do (#291).</summary>
public static class ElevenLabsModels
{
    /// <summary>The expressive model, and the default.</summary>
    public const string V3 = "eleven_v3_conversational";

    /// <summary>The fast model, and what d47 pinned before v3 existed.</summary>
    public const string Flash = "eleven_flash_v2_5";

    /// <summary>What speaks when nobody has chosen.</summary>
    public const string Default = V3;

    /// <summary>The models offered, in the order the row lists them.</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        (V3, "v3 Conversational — more expressive, about 2 seconds"),
        (Flash, "Flash 2.5 — fastest, about 0.3 seconds"),
    ];

    /// <summary>
    /// A stored name, or <see cref="Default"/> where it is missing or is one d47 no longer offers.
    /// </summary>
    public static string Named(string? model) =>
        All.Any(offered => offered.Id == model) ? model! : Default;

    /// <summary>Whether the model honours <c>voice_settings.speed</c>.</summary>
    public static bool ReadsRate(string? model) => Named(model) == Flash;

    /// <summary>Whether the model performs bracketed delivery direction rather than reading it aloud.</summary>
    public static bool ReadsTags(string? model) => Named(model) == V3;

    /// <summary>
    /// How much text the model would rather be handed at once, or zero for one sentence at a time.
    /// </summary>
    public static int GroupsSentencesUpTo(string? model) => ReadsTags(model) ? 300 : 0;
}
