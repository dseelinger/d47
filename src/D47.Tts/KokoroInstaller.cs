using System.Security.Cryptography;
using D47.Core.Speech;
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

/// <summary>
/// Fetches what the local voice needs, once (#101).
/// <para>
/// <b>The same road the speech-to-text models take, and deliberately the same shape</b>: the
/// expected hash is pinned in the build rather than asked of the host on the day, a transfer that
/// does not match it is discarded rather than loaded, and every file is written to a sibling and
/// moved into place so a half-downloaded model never appears under its real name.
/// </para>
/// <para>
/// That pinning is what made fp32 affordable. The spike framed 310 MB as multiplying the installer
/// by five; downloaded on request it is a first-run cost on a machine that has already fetched a
/// Whisper model, and it buys the fastest of the three variants — the smallest being four times
/// slower is backwards from the usual assumption and is why the choice went this way.
/// </para>
/// </summary>
public sealed class KokoroInstaller : IDisposable
{
    private const string PendingSuffix = ".part";

    private readonly string _folder;
    private readonly HttpClient _http;
    private readonly ILogger<KokoroInstaller> _logger;

    public KokoroInstaller(string folder, ILogger<KokoroInstaller> logger)
    {
        _folder = folder;
        _logger = logger;

        _http = new HttpClient
        {
            // A 310 MB file on a slow connection is a long transfer, not a hung one. The
            // cancellation token is what actually bounds this.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("D47");
    }

    public string Folder => _folder;

    public bool IsInstalled => KokoroAssets.IsInstalled(_folder);

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

    /// <summary>
    /// <b>Swaps the model for a different build of it</b> (#139).
    /// <para>
    /// Only the model: the tokenizer, the dictionary and the 28 voices are the same files for
    /// every build, which is what makes changing one a 90–310 MB question rather than a 1 GB one.
    /// </para>
    /// <para>
    /// <b>A failed or cancelled fetch leaves the previous build in place and working.</b> That
    /// falls out of the road every file here already takes — written to a sibling <c>.part</c> and
    /// moved into place only after its pinned hash matches — so there is no window in which
    /// <c>model.onnx</c> is neither the old build nor the new one.
    /// </para>
    /// </summary>
    public async Task<KokoroInstallResult> SwitchAsync(
        string buildId,
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var build = KokoroAssets.BuildFor(buildId);

        if (KokoroAssets.InstalledBuild(_folder)?.Id == build.Id)
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

    /// <summary>
    /// Where a repository path lands on disk. Voices keep their folder; nothing else nests.
    /// <para>
    /// <b>Every model build lands as <c>model.onnx</c></b> (#139), whichever of the eight it is.
    /// One model file on disk is what makes a switch leave no orphan behind and stops eight builds
    /// accumulating into 1.4 GB of a Commander's drive — and it is why the provider needs no
    /// telling which build it is loading. Which one is there is read back from its byte count, by
    /// <see cref="KokoroAssets.InstalledBuild"/>.
    /// </para>
    /// </summary>
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

    private async Task<KokoroInstallResult> FetchAsync(
        KokoroAsset asset,
        string destination,
        long already,
        long total,
        IProgress<KokoroProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var pending = destination + PendingSuffix;

        try
        {
            string actual;

            // <b>Scoped, and it has to be.</b> `await using var` disposes at the end of the
            // enclosing block, so a File.Move written after it inside the same try runs while
            // the stream is still open and fails with "used by another process" -- which reads
            // like a download problem and is not one.
            using (var response = await _http
                       .GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Asynchronous and unbuffered, for the reason HttpModelStore records: the four-argument
                // constructor defaults to blocking 4 KB writes, which is the wrong shape on a machine
                // that is busy even where it was measured as neutral on one that is not.
                await using var file = new FileStream(
                    pending, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 1, FileOptions.Asynchronous);

                using var sha = SHA256.Create();

                var buffer = new byte[262144];
                long received = 0;
                int read;
                var lastPercent = -1;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    received += read;

                    // Reported on whole percentage points of the WHOLE set, not of this file: a
                    // Commander watching a bar wants to know how far the download has got, and a bar
                    // that fills twenty-nine times tells them nothing.
                    var percent = total > 0 ? (int)((already + received) * 100 / total) : -1;

                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(new KokoroProgress(asset.Path, already + received, total));
                    }
                }

                sha.TransformFinalBlock([], 0, 0);

                actual = Convert.ToHexStringLower(sha.Hash!);
            }

            // A file with no pinned hash is allowed and says so: the tokenizer is 3 kB and is not
            // stored through LFS, so the listing carries no hash for it to be checked against.
            if (asset.Sha256 is { Length: > 0 } expected &&
                !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(pending);

                _logger.LogError(
                    "{File} did not match the checksum D47 expects, so it was discarded", asset.Path);

                return new KokoroInstallResult(
                    KokoroInstall.ChecksumMismatch,
                    $"{asset.Path} is not the file D47 expects, so it was discarded.");
            }

            File.Move(pending, destination, overwrite: true);
            return new KokoroInstallResult(KokoroInstall.Installed);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            TryDelete(pending);

            _logger.LogWarning(ex, "Could not download {File}", asset.Path);

            return new KokoroInstallResult(
                KokoroInstall.Failed, $"Could not download {asset.Path}: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover .part is harmless: it is never loaded and the next run overwrites it.
        }
    }

    public void Dispose() => _http.Dispose();
}
