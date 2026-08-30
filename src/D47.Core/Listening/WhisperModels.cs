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
        new("medium.en", "Medium (English only) — the most accurate, and wants a GPU", 1463,
            "cc37e93478338ec7700281a7ac30a10128929eb8f427dda2e865faa8f6da4356"),
    ];

    /// <summary>
    /// The multilingual models, retired on 2026-08-30, and what a Commander who had one selected
    /// gets instead (<a href="https://github.com/dseelinger/d47/issues/187">#187</a>'s corpus).
    /// <para>
    /// <b>They could never be multilingual here.</b> <c>WhisperTranscriber</c> pins Whisper to
    /// English on every load, so a multilingual model cost the same download as its <c>.en</c>
    /// twin and gave back a model handicapped at the one language d47 asks it for.
    /// </para>
    /// <para>
    /// <b>And the corpus showed the pin does not silence them.</b> Measured over 37 clips, a
    /// multilingual model asked for English answered eight of eight no-speech clips with
    /// confident foreign sentences — <i>"Grazie a tutti!"</i> for a held key over a quiet room,
    /// for mouse clicks, for a sigh. Nothing filters those: they are not bracketed, so
    /// <c>Clean</c> keeps them and <c>SpeechNoise</c> keeps them, and they reach the turn loop as
    /// something the Commander said. The English-only models answered the same clips with
    /// silence, or with <i>"(keyboard clicking)"</i>, which d47 already discards.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> Retired = new(StringComparer.Ordinal)
    {
        ["tiny"] = "tiny.en",
        ["base"] = "base.en",
        ["small"] = "small.en",
        ["medium"] = "medium.en",
    };

    /// <summary>
    /// What a stored model id becomes now, which is itself for everything this build still
    /// offers. A retired id adopts its English twin rather than falling through to "none":
    /// <see cref="Find"/> answers null for an id it does not know, and a null there means
    /// <see cref="SpeechModelAction.Unload"/> — so without this a Commander who had
    /// <c>small</c> selected would silently lose speech-to-text on upgrade.
    /// </summary>
    public static string? AdoptedId(string? id) =>
        id is not null && Retired.TryGetValue(id, out var replacement) ? replacement : id;

    /// <summary>
    /// What a fresh install selects. <b>Base rather than Tiny since 2026-08-30</b>, on the
    /// corpus recorded for <a href="https://github.com/dseelinger/d47/issues/187">#187</a>.
    /// <para>
    /// Tiny is the cheapest and it mis-hears the words it can least afford to. Over 37 clips it
    /// answered <i>"Cancel that"</i> with <b><i>"Cancer that"</i></b> — and "cancel that" is a
    /// declared interrupt phrase, so the keyword router simply does not match it and the barge-in
    /// fails. It also gave <i>"Plata Rout"</i> for "Plot a route", <i>"Shinrata"</i> for
    /// Shinrarta Dezhra, and <i>"Halfar is it to DC at?"</i> for "How far is it to Deciat?".
    /// Base got the first three right and was the <b>cleanest of all five models on non-speech</b>,
    /// answering held keys and mouse clicks with nothing, or with a bracketed note d47 discards.
    /// </para>
    /// <para>
    /// The download it commits a fresh install to goes from 75 MB to 142 MB, which is the reason
    /// this was Tiny and is still a defensible thing to spend on somebody's behalf — where the
    /// 1,463 MB of Medium would not be.
    /// </para>
    /// </summary>
    public const string DefaultId = "base.en";

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
