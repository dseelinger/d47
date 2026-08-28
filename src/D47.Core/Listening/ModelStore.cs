namespace D47.Core.Listening;

/// <summary>
/// What d47 knows about a model before asking permission to fetch it: the real size and the
/// real content hash, both as reported by the host.
/// </summary>
/// <param name="Bytes">The actual size, as reported by the host rather than estimated here.</param>
/// <param name="Sha256">
/// The content hash the host publishes for the file. Null when the host did not supply one, in
/// which case the download proceeds without a hash to check against and says so.
/// </param>
public sealed record ModelOffer(WhisperModel Model, long Bytes, string? Sha256)
{
    public string Url => WhisperModels.DownloadUrl(Model);

    public double Megabytes => Bytes / 1024.0 / 1024.0;
}

/// <summary>How a download ended.</summary>
public enum ModelInstall
{
    Installed,

    /// <summary>Already present and intact. Nothing was fetched.</summary>
    AlreadyPresent,

    /// <summary>The transfer was cancelled before it finished. Nothing was kept.</summary>
    Cancelled,

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
/// Fetching a speech model, on demand (Phase 6).
/// <para>
/// The interface lives in Core so the settings rows, the egress disclosure and the capability
/// can be written and tested against it, while the HTTP lives outside Core with the rest of the
/// providers (architecture.md §3).
/// </para>
/// <para>
/// <b>On demand means exactly that.</b> Nothing here runs on a timer or at startup for its own
/// reasons: a fetch happens because a model is selected and missing, and the selection is the
/// Commander's. There was a consent callback here, asked after the size and host were known;
/// it went when the answer became always yes at every caller, and a gate nobody can close is a
/// gate that only makes the code look careful.
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
    /// it. A network call, and the only one that happens before the transfer itself: it fetches
    /// metadata, never the model.
    /// </summary>
    Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a model. The size and published hash are looked up first, so a transfer that
    /// starts is one d47 can check when it lands.
    /// </summary>
    Task<ModelInstallResult> InstallAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an installed model. The Commander reclaiming disk is not a fetch.</summary>
    bool Remove(WhisperModel model);
}
