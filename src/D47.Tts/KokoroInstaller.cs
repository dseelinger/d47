using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>How far a download has got, as a fraction of the whole set.</summary>
public sealed record KokoroProgress(string File, long Received, long Total)
{
    public double Fraction => Total > 0 ? Math.Clamp(Received / (double)Total, 0, 1) : 0;
}

/// <summary>How it ended.</summary>
public enum KokoroInstall
{
    Installed,
    AlreadyPresent,
    ChecksumMismatch,
    Failed,
}

public sealed record KokoroInstallResult(KokoroInstall Outcome, string? Detail = null);

/// <summary>Fetches what the local voice needs, once (#101).</summary>
public sealed class KokoroInstaller : IDisposable
{
    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly HttpClient _http;
    private readonly ILogger<KokoroInstaller> _logger;

    public KokoroInstaller(IFileSystem files, string folder, ILogger<KokoroInstaller> logger)
    {
        _files = files;
        _folder = folder;
        _logger = logger;

        _http = new HttpClient
        {
            // A 310 MB file on a slow connection is a long transfer, not a hung one.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("D47");
    }

    public string Folder => _folder;

    public bool IsInstalled => KokoroAssets.IsInstalled(_files, _folder);

    /// <summary>Everything, in one go, reporting against the whole set rather than per file.</summary>
    public async Task<KokoroInstallResult> InstallAsync(
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? buildId = null)
    {
        if (IsInstalled)
        {
            return new KokoroInstallResult(KokoroInstall.AlreadyPresent);
        }

        var assets = new List<KokoroAsset>
        {
            KokoroAssets.BuildFor(buildId).Asset,
            KokoroAssets.Tokenizer,
            KokoroAssets.Dictionary,
        };

        assets.AddRange(KokoroAssets.Voices);

        var total = assets.Sum(asset => asset.Bytes);
        long done = 0;

        foreach (var asset in assets)
        {
            var destination = Destination(asset);

            if (File.Exists(destination))
            {
                done += asset.Bytes;
                progress?.Report(new KokoroProgress(asset.Path, done, total));
                continue;
            }

            var result = await FetchAsync(asset, destination, done, total, progress, cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome != KokoroInstall.Installed)
            {
                return result;
            }

            done += asset.Bytes;
            progress?.Report(new KokoroProgress(asset.Path, done, total));
        }

        _logger.LogInformation("The local voice is installed in {Folder}", _folder);
        return new KokoroInstallResult(KokoroInstall.Installed);
    }

    /// <summary>Swaps the model for a different build of it (#139).</summary>
    public async Task<KokoroInstallResult> SwitchAsync(
        string buildId,
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var build = KokoroAssets.BuildFor(buildId);

        if (KokoroAssets.InstalledBuild(_files, _folder)?.Id == build.Id)
        {
            return new KokoroInstallResult(KokoroInstall.AlreadyPresent);
        }

        var result = await FetchAsync(
                build.Asset,
                Destination(build.Asset),
                already: 0,
                total: build.Asset.Bytes,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Outcome == KokoroInstall.Installed)
        {
            _logger.LogInformation(
                "The local voice is now running the {Build} build ({Megabytes:0} MB)",
                build.Id,
                build.Asset.Megabytes);
        }

        return result;
    }

    /// <summary>Where a repository path lands on disk.</summary>
    internal string Destination(KokoroAsset asset)
    {
        var name = Path.GetFileName(asset.Path);

        if (asset.Path.StartsWith("voices/", StringComparison.Ordinal))
        {
            return Path.Combine(_folder, "voices", name);
        }

        return Path.Combine(
            _folder,
            KokoroAssets.Builds.Any(build => build.Asset.Path == asset.Path) ? "model.onnx" : name);
    }

    private Task<KokoroInstallResult> FetchAsync(
        KokoroAsset asset,
        string destination,
        long already,
        long total,
        IProgress<KokoroProgress>? progress,
        CancellationToken cancellationToken) =>
        ModelDownload.FetchAsync(_http, _logger, asset, destination, already, total, progress, cancellationToken);

    public void Dispose() => _http.Dispose();
}
