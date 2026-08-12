namespace D47.Core.Listening;

/// <summary>
/// What d47 knows about a model before asking permission to fetch it: the real size and the
/// real content hash, both as reported by the host.
/// </summary>
/// <param name="Bytes">The actual size, so the consent prompt states a fact rather than an estimate.</param>
/// <param name="Sha256">
/// The content hash the host publishes for the file. Null when the host did not supply one, in
/// which case the download proceeds without a hash to check against and says so.
/// </param>
public sealed record ModelOffer(WhisperModel Model, long Bytes, string? Sha256)
{
    public string Url => WhisperModels.DownloadUrl(Model);

    public double Megabytes => Bytes / 1024.0 / 1024.0;

    /// <summary>
    /// Exactly what the Commander is being asked to agree to: what, how big, and from where.
    /// One sentence, because a consent prompt nobody reads is not consent.
    /// </summary>
    public string ConsentPrompt() =>
        $"Download the {Model.Label} speech model? "
        + $"{Megabytes:0.#} MB from {WhisperModels.Host}, saved to your data folder. "
        + (Sha256 is null
            ? "The host published no checksum for it, so d47 can only check that the download completed."
            : "d47 will verify the download against the checksum the host published.");
}

/// <summary>How a download ended.</summary>
public enum ModelInstall
{
    Installed,

    /// <summary>Already present and intact. Nothing was fetched.</summary>
    AlreadyPresent,

    /// <summary>The Commander said no, or never said yes. Nothing was fetched.</summary>
    Declined,

    /// <summary>The bytes arrived but did not match the published hash. Discarded.</summary>
    ChecksumMismatch,

    Failed,
}

public sealed record ModelInstallResult(ModelInstall Outcome, string? Detail = null)
{
    public bool Success => Outcome is ModelInstall.Installed or ModelInstall.AlreadyPresent;
}

/// <summary>Progress during a download, for the panel.</summary>
public sealed record ModelProgress(string ModelId, long BytesReceived, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? (double)BytesReceived / TotalBytes : 0;
}

/// <summary>
/// Fetching a speech model, on demand and with explicit consent (list.md Phase 6; settled
/// before this phase began).
/// <para>
/// The interface lives in Core so the settings rows, the egress disclosure and the capability
/// can be written and tested against it, while the HTTP lives outside Core with the rest of the
/// providers (architecture.md §3).
/// </para>
/// <para>
/// <b>The consent callback is the whole point.</b> No implementation may fetch anything without
/// it returning true, and it is asked <em>after</em> the real size and host are known so the
/// question it puts to the Commander contains facts rather than estimates.
/// </para>
/// </summary>
public interface IModelStore
{
    /// <summary>Where models are kept. Beside the executable, like everything else d47 writes.</summary>
    string Directory { get; }

    /// <summary>Whether a model is already on disk. Answered without touching the network.</summary>
    bool IsInstalled(WhisperModel model);

    /// <summary>The path to an installed model, or null.</summary>
    string? PathOf(WhisperModel model);

    /// <summary>Installed model ids, for the settings row to mark which are ready.</summary>
    IReadOnlyList<string> Installed();

    /// <summary>
    /// Asks the host what the file actually is — size and published hash — without downloading
    /// it. This is a network call and is the only thing that happens before consent; it fetches
    /// metadata, never the model.
    /// </summary>
    Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a model, having first asked <paramref name="consent"/>. Returns
    /// <see cref="ModelInstall.Declined"/> without touching the network beyond the metadata
    /// lookup when consent is refused.
    /// </summary>
    Task<ModelInstallResult> InstallAsync(
        WhisperModel model,
        Func<ModelOffer, Task<bool>> consent,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an installed model. The Commander reclaiming disk is not a fetch.</summary>
    bool Remove(WhisperModel model);
}
