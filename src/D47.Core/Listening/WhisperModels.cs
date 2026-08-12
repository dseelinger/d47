namespace D47.Core.Listening;

/// <summary>
/// One speech-to-text model the Commander can choose (list.md Phase 6, "STT Model Choice").
/// </summary>
/// <param name="Id">The settings value and the ggml file's stem — "base.en".</param>
/// <param name="ApproximateMegabytes">
/// For the settings row only, so a Commander comparing options sees the order of magnitude
/// before committing to anything. <b>The consent prompt states the size the server actually
/// reports</b>, not this — a figure written here would be a number d47 asserts about a file it
/// has never seen.
/// </param>
public sealed record WhisperModel(string Id, string Label, int ApproximateMegabytes)
{
    /// <summary>English-only models are smaller and better at English than the multilingual pair.</summary>
    public bool EnglishOnly => Id.EndsWith(".en", StringComparison.Ordinal);

    public string FileName => $"ggml-{Id}.bin";

    /// <summary>Where the file lives, relative to the repository root that serves it.</summary>
    public string RepositoryPath => FileName;
}

/// <summary>
/// The models d47 offers, and where they come from.
/// <para>
/// Nothing here is downloaded until the Commander says so. A model arriving at first launch
/// would be a several-hundred-megabyte transfer nobody asked for, from a host they were never
/// told about — which is exactly the kind of thing the egress disclosure exists to make
/// impossible to do quietly.
/// </para>
/// </summary>
public static class WhisperModels
{
    /// <summary>The canonical ggml distribution, which is what whisper.cpp itself downloads from.</summary>
    public const string Host = "huggingface.co";

    public const string Repository = "ggerganov/whisper.cpp";

    public static string DownloadUrl(WhisperModel model) =>
        $"https://{Host}/{Repository}/resolve/main/{model.RepositoryPath}";

    /// <summary>The API endpoint that reports each file's real size and content hash.</summary>
    public static string MetadataUrl() => $"https://{Host}/api/models/{Repository}";

    /// <summary>The value meaning "do not transcribe". A real choice, like "none" everywhere else.</summary>
    public const string NoneId = "none";

    /// <summary>
    /// Smallest first. A short push-to-talk clip on the small English models absorbs CPU
    /// inference fine, which is what makes CPU a sensible default rather than a compromise.
    /// </summary>
    public static IReadOnlyList<WhisperModel> All { get; } =
    [
        new("tiny.en", "Tiny (English only) — fastest, least accurate", 75),
        new("base.en", "Base (English only) — the usual choice", 142),
        new("small.en", "Small (English only) — more accurate, slower", 466),
        new("tiny", "Tiny (multilingual)", 75),
        new("base", "Base (multilingual)", 142),
        new("small", "Small (multilingual)", 466),
        new("medium", "Medium (multilingual) — slow without a GPU", 1500),
    ];

    /// <summary>What a fresh install selects: small, English, and good enough for a short clip.</summary>
    public const string DefaultId = "base.en";

    public static WhisperModel? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(model => model.Id == id);

    public static IReadOnlyList<string> Ids => [NoneId, .. All.Select(model => model.Id)];

    public static string LabelOf(string id) =>
        id == NoneId ? "None — do not transcribe" : Find(id)?.Label ?? id;
}
