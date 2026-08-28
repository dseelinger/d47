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
/// <param name="Sha256">
/// <b>The hash this file is expected to have, pinned here rather than taken from whatever the
/// host says on the day</b> (#124).
/// <para>
/// <b>Say what this is and is not.</b> These seven values were read from Hugging Face's own
/// listing on <b>2026-08-28</b> and are immutable from that moment: they are not an
/// independent attestation, and pinning them does not make the first read trustworthy. What it
/// buys is that the file <em>changing</em> becomes visible instead of invisible — before this,
/// the expected hash and the bytes came from the same server, so anything able to serve
/// different bytes could serve the hash for them.
/// </para>
/// <para>
/// That matters more here than it would elsewhere: this file is loaded and executed in-process
/// by the native Whisper runtime, and it arrives over the one network road the listening path
/// has.
/// </para>
/// <para>
/// A model with no pinned hash is allowed — it falls back to the host's value and says so, the
/// existing honest path — but <c>EveryShippedModelIsPinnedTests</c> means a new one cannot
/// arrive unpinned without that being a decision somebody took.
/// </para>
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
    /// <summary>
    /// <b><c>?blobs=true</c>, and without it this call returns nothing worth having.</b> The
    /// plain listing gives one key per file — <c>rfilename</c> — and no <c>lfs</c> block at all,
    /// so the size read from it was always 0 and the hash was always null. A Commander was
    /// offered every model as "0 MB", and because the verification is skipped when there is no
    /// expected hash, <b>no downloaded model was ever checked against anything</b>. Found
    /// 2026-08-28 while pinning the hashes (#124), which had assumed the check ran.
    /// </summary>
    public static string MetadataUrl() => $"https://{Host}/api/models/{Repository}?blobs=true";

    /// <summary>The value meaning "do not transcribe". A real choice, like "none" everywhere else.</summary>
    public const string NoneId = "none";

    /// <summary>
    /// Smallest first. A short push-to-talk clip on the small English models absorbs CPU
    /// inference fine, which is what makes CPU a sensible default rather than a compromise.
    /// </summary>
    public static IReadOnlyList<WhisperModel> All { get; } =
    [
        new("tiny.en", "Tiny (English only) — fastest, least accurate", 75,
            "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f"),
        new("base.en", "Base (English only) — the usual choice", 142,
            "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002"),
        new("small.en", "Small (English only) — more accurate, slower", 466,
            "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d"),
        new("tiny", "Tiny (multilingual)", 75,
            "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21"),
        new("base", "Base (multilingual)", 142,
            "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe"),
        new("small", "Small (multilingual)", 466,
            "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b"),
        new("medium", "Medium (multilingual) — slow without a GPU", 1500,
            "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208"),
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
