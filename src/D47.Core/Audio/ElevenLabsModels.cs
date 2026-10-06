using D47.Core.Catalog;

namespace D47.Core.Audio;

/// <summary>Which ElevenLabs model speaks, and what each one can do, read from the model catalog (#291).</summary>
public static class ElevenLabsModels
{
    public const string V4Turbo = "eleven_v4_turbo";

    public const string V3 = "eleven_v3_conversational";

    public const string Flash = "eleven_flash_v2_5";

    private static ModelCatalog Catalog => ModelCatalogSource.Shared.Current;

    /// <summary>What speaks when nobody has chosen.</summary>
    public static string Default => Catalog.SpeechDefaultFor(TtsProviderCatalog.ElevenLabsId)!;

    /// <summary>The models offered, in the order the row lists them.</summary>
    public static IReadOnlyList<SpeechModel> All => Catalog.OfferedSpeechFor(TtsProviderCatalog.ElevenLabsId);

    /// <summary>
    /// The models ElevenLabs listed for the stored key that the catalog does not name: no tags, no rate,
    /// one sentence at a time.
    /// </summary>
    public static IReadOnlyList<SpeechModel> New =>
    [
        .. ModelCatalogSource.Shared.NewSpeechFor(TtsProviderCatalog.ElevenLabsId)
            .Select(listed => new SpeechModel(listed.Id, listed.Name ?? listed.Id, Offered: false)),
    ];

    /// <summary>
    /// A stored name, or <see cref="Default"/> where it is missing or is neither offered nor
    /// <see cref="New"/>.
    /// </summary>
    public static string Named(string? model) => Resolved(model).Id;

    /// <summary>Whether the model honours <c>voice_settings.speed</c>.</summary>
    public static bool ReadsRate(string? model) => Resolved(model).ReadsRate;

    /// <summary>Whether the model performs bracketed delivery direction rather than reading it aloud.</summary>
    public static bool ReadsTags(string? model) => Resolved(model).ReadsTags;

    /// <summary>
    /// How much text the model would rather be handed at once, or zero for one sentence at a time.
    /// </summary>
    public static int GroupsSentencesUpTo(string? model) => Resolved(model).GroupsSentencesUpTo;

    /// <summary>The catalog's entry for <see cref="Named"/>, or the listed one where it is new.</summary>
    public static SpeechModel Resolved(string? model)
    {
        var catalog = Catalog;
        var offered = catalog.OfferedSpeechFor(TtsProviderCatalog.ElevenLabsId);

        return offered.FirstOrDefault(candidate => candidate.Id == model)
               ?? New.FirstOrDefault(candidate => candidate.Id == model)
               ?? offered.First(candidate => candidate.Id == catalog.SpeechDefaultFor(TtsProviderCatalog.ElevenLabsId));
    }
}
