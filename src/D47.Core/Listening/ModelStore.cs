namespace D47.Core.Listening;

/// <summary>
/// What d47 knows about a model before asking permission to fetch it: the real size and the real
/// content hash, both as reported by the host.
/// </summary>
/// <param name="Bytes">The actual size, as reported by the host rather than estimated here.</param>
/// <param name="Sha256">The content hash the host publishes for the file.</param>
public sealed record ModelOffer(WhisperModel Model, long Bytes, string? Sha256)
{
    public string Url => WhisperModels.DownloadUrl(Model);

    public double Megabytes => Bytes / 1024.0 / 1024.0;
}

/// <summary>How a download ended.</summary>
public enum ModelInstall
{
    Installed,

    /// <summary>Already present and intact.</summary>
    AlreadyPresent,

    /// <summary>The transfer was cancelled before it finished.</summary>
    Cancelled,

    /// <summary>The bytes arrived but did not match the published hash.</summary>
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

/// <summary>Fetching a speech model, on demand (Phase 6).</summary>
public interface IModelStore
{
    /// <summary>Where models are kept.</summary>
    string Directory { get; }

    /// <summary>Whether a model is already on disk.</summary>
    bool IsInstalled(WhisperModel model);

    /// <summary>The path to an installed model, or null.</summary>
    string? PathOf(WhisperModel model);

    /// <summary>Installed model ids, for the settings row to mark which are ready.</summary>
    IReadOnlyList<string> Installed();

    /// <summary>
    /// Asks the host what the file actually is — size and published hash — without downloading it.
    /// </summary>
    Task<ModelOffer?> DescribeAsync(WhisperModel model, CancellationToken cancellationToken = default);

    /// <summary>Downloads a model.</summary>
    Task<ModelInstallResult> InstallAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an installed model.</summary>
    bool Remove(WhisperModel model);
}
