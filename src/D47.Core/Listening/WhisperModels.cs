namespace D47.Core.Listening;

/// <summary>
/// One speech-to-text model the Commander can choose (Phase 6, "STT Model Choice").
/// </summary>
/// <param name="Id">The settings value and the ggml file's stem — "base.en".</param>
/// <param name="ApproximateMegabytes">
/// For the settings row only, so a Commander comparing options sees the order of magnitude
/// before choosing. <b>The size d47 reports while fetching is the one the host actually
/// gave</b>, not this — a figure written here would be a number d47 asserts about a file it has
/// never seen.
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
/// A selected model that is not on disk is fetched — at first launch, or when the Commander
/// picks a different one. The safeguards are that the selection is theirs, the size is on the
/// row they choose from, and this host is named in the egress disclosure for as long as any
/// model is selected. What the disclosure exists to prevent is a transfer nobody can see, not a
/// transfer nobody had to approve twice.
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

    /// <summary>
    /// What a fresh install selects: the smallest English model, and good enough for the short
    /// push-to-talk clips this is actually asked to transcribe.
    /// <para>
    /// Being the shipped default makes this the one model most Commanders will ever download, so
    /// it is the cheapest in the catalogue: the first launch fetches it without being asked, and
    /// 75 MB is a defensible thing to spend on somebody's behalf where 1.5 GB would not be.
    /// </para>
    /// </summary>
    public const string DefaultId = "tiny.en";

    /// <summary>
    /// The model the Commander has chosen but has not got, or null when there is nothing
    /// outstanding. Both halves matter: "none" is a choice rather than a pending question, and a
    /// model already on disk is not one either.
    /// </summary>
    public static WhisperModel? AwaitingDownload(string? selected, IModelStore store) =>
        Find(selected) is { } model && !store.IsInstalled(model) ? model : null;

    public static WhisperModel? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(model => model.Id == id);

    public static IReadOnlyList<string> Ids => [NoneId, .. All.Select(model => model.Id)];

    public static string LabelOf(string id) =>
        id == NoneId ? "None — do not transcribe" : Find(id)?.Label ?? id;
}
