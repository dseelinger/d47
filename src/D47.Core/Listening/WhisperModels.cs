namespace D47.Core.Listening;

/// <summary>One speech-to-text model the Commander can choose (Phase 6, "STT Model Choice").</summary>
/// <param name="Id">The settings value and the ggml file's stem — "base.en".</param>
/// <param name="ApproximateMegabytes">
/// For the settings row only, so a Commander comparing options sees the order of magnitude before
/// choosing.
/// </param>
/// <param name="Sha256">
/// The hash this file is expected to have, pinned here rather than taken from whatever the host says on
/// the day (#124).
/// </param>
public sealed record WhisperModel(
    string Id,
    string Label,
    int ApproximateMegabytes,
    string? Sha256 = null)
{
    /// <summary>English-only models are smaller and better at English than the multilingual pair.</summary>
    public bool EnglishOnly => Id.EndsWith(".en", StringComparison.Ordinal);

    public string FileName => $"ggml-{Id}.bin";

    /// <summary>Where the file lives, relative to the repository root that serves it.</summary>
    public string RepositoryPath => FileName;
}

/// <summary>The models d47 offers, and where they come from.</summary>
public static class WhisperModels
{
    /// <summary>The canonical ggml distribution, which is what whisper.cpp itself downloads from.</summary>
    public const string Host = "huggingface.co";

    public const string Repository = "ggerganov/whisper.cpp";

    public static string DownloadUrl(WhisperModel model) =>
        $"https://{Host}/{Repository}/resolve/main/{model.RepositoryPath}";

    /// <summary>The API endpoint that reports each file's real size and content hash.</summary>
    public static string MetadataUrl() => $"https://{Host}/api/models/{Repository}?blobs=true";

    /// <summary>The value meaning "do not transcribe".</summary>
    public const string NoneId = "none";

    /// <summary>Smallest first.</summary>
    public static IReadOnlyList<WhisperModel> All { get; } =
    [
        new("tiny.en", "Tiny (English only) — fastest, least accurate", 75,
            "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f"),
        new("base.en", "Base (English only) — the usual choice", 142,
            "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002"),
        new("small.en", "Small (English only) — more accurate, slower", 466,
            "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d"),
        new("medium.en", "Medium (English only) — the most accurate, and wants a GPU", 1463,
            "cc37e93478338ec7700281a7ac30a10128929eb8f427dda2e865faa8f6da4356"),
    ];

    /// <summary>
    /// The multilingual models, retired on 2026-08-30, and what a Commander who had one selected gets
    /// instead (#187's corpus).
    /// </summary>
    private static readonly Dictionary<string, string> Retired = new(StringComparer.Ordinal)
    {
        ["tiny"] = "tiny.en",
        ["base"] = "base.en",
        ["small"] = "small.en",
        ["medium"] = "medium.en",
    };

    /// <summary>
    /// What a stored model id becomes now, which is itself for everything this build still offers.
    /// </summary>
    public static string? AdoptedId(string? id) =>
        id is not null && Retired.TryGetValue(id, out var replacement) ? replacement : id;

    /// <summary>What a fresh install selects.</summary>
    public const string DefaultId = "base.en";

    /// <summary>
    /// The model the Commander has chosen but has not got, or null when there is nothing outstanding.
    /// </summary>
    public static WhisperModel? AwaitingDownload(string? selected, IModelStore store) =>
        Find(selected) is { } model && !store.IsInstalled(model) ? model : null;

    public static WhisperModel? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(model => model.Id == id);

    public static IReadOnlyList<string> Ids => [NoneId, .. All.Select(model => model.Id)];

    public static string LabelOf(string id) =>
        id == NoneId ? "None — do not transcribe" : Find(id)?.Label ?? id;
}
