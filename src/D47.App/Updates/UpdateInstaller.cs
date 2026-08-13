using System.Security.Cryptography;
using D47.Core;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>Why an in-place update did not happen. The Commander is told which one it was.</summary>
public enum UpdateFailure
{
    /// <summary>The release carried no executable, or not one from this repository.</summary>
    NothingToInstall,

    /// <summary>The download did not complete.</summary>
    DownloadFailed,

    /// <summary>The bytes that arrived are not the bytes the release published.</summary>
    ChecksumMismatch,

    /// <summary>The executable could not be replaced — most often a folder needing elevation.</summary>
    CouldNotReplace,
}

/// <summary>
/// Downloads a newer d47 and puts it in place of the running one (list.md Phase 17, "Check for
/// Updates on start" — "the user is given an opportunity to exit, install it, and restart").
/// <para>
/// <b>This downloads a program and then runs it</b>, which is the most dangerous thing d47 does,
/// so the path is narrow on purpose. The URL must be an asset on a release of this repository —
/// checked in <see cref="UpdateChecker"/>, before anything is fetched. The bytes must hash to
/// the checksum the release publishes beside them, or they are deleted rather than run. And the
/// only file ever replaced is d47's own executable.
/// </para>
/// <para>
/// <b>What the checksum does and does not buy.</b> It catches a truncated or corrupted transfer
/// and a mirror serving something else. It is not a signature: the hash and the bytes come from
/// the same server, so it cannot detect a compromised github.com. The same caveat applies to the
/// speech model downloads, and the honest framing is that this is materially better than the
/// Commander downloading the same file by hand from the same place — which is what it replaces —
/// rather than that it is proof of origin.
/// </para>
/// </summary>
public sealed class UpdateInstaller(AppPaths paths, ILogger<UpdateInstaller> logger)
{
    /// <summary>
    /// The suffix the outgoing executable is renamed to. Windows will not let a running image be
    /// overwritten, but it will let it be renamed — which is what makes this possible without a
    /// second helper process to babysit.
    /// </summary>
    internal const string RetiredSuffix = ".old";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Where downloads land: beside the executable, like everything else d47 writes.</summary>
    public string StagingFolder => Path.Combine(paths.Data, "updates");

    /// <summary>
    /// Fetches the new executable and verifies it. Returns the path to the verified file, or the
    /// reason it did not get one. Nothing is replaced here — a failed download leaves the
    /// running d47 untouched.
    /// </summary>
    public async Task<(string? File, UpdateFailure? Failure)> DownloadAsync(
        AvailableUpdate update,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!update.CanInstall)
        {
            return (null, UpdateFailure.NothingToInstall);
        }

        Directory.CreateDirectory(StagingFolder);

        var destination = Path.Combine(StagingFolder, $"d47-{update.Version}.exe");

        try
        {
            await DownloadToAsync(update.DownloadUrl!, destination, progress, cancellationToken)
                .ConfigureAwait(false);

            var published = await ReadChecksumAsync(update.ChecksumUrl!, cancellationToken)
                .ConfigureAwait(false);

            if (published is null)
            {
                Discard(destination);
                return (null, UpdateFailure.ChecksumMismatch);
            }

            var actual = await HashAsync(destination, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(actual, published, StringComparison.OrdinalIgnoreCase))
            {
                // Deleted, not kept for inspection: a file that failed its checksum is a file
                // nobody should be able to run by double-clicking it later.
                logger.LogError(
                    "The downloaded update did not match its published checksum; discarding it");

                Discard(destination);
                return (null, UpdateFailure.ChecksumMismatch);
            }

            logger.LogInformation("Downloaded and verified D47 {Version}", update.Version);
            return (destination, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogWarning(ex, "Could not download the update");
            Discard(destination);
            return (null, UpdateFailure.DownloadFailed);
        }
    }

    /// <summary>
    /// Puts <paramref name="downloaded"/> where <paramref name="runningExecutable"/> is, by
    /// retiring the current file rather than overwriting it.
    /// <para>
    /// The order matters. The outgoing executable is renamed first and only then is the new one
    /// moved in, so the window in which neither exists is a single rename wide — and if that
    /// second move fails, the retired file is put straight back. The Commander never ends up
    /// with no d47 at all.
    /// </para>
    /// </summary>
    internal bool TrySwap(string runningExecutable, string downloaded)
    {
        var retired = runningExecutable + RetiredSuffix;

        try
        {
            if (File.Exists(retired))
            {
                File.Delete(retired);
            }

            File.Move(runningExecutable, retired);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not retire the running executable");
            return false;
        }

        try
        {
            File.Move(downloaded, runningExecutable);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not move the update into place; putting the old build back");

            try
            {
                File.Move(retired, runningExecutable);
            }
            catch (Exception restore) when (restore is IOException or UnauthorizedAccessException)
            {
                // Nothing left to try in-process. Said loudly because the executable is now
                // sitting next to where it belongs under a different name.
                logger.LogCritical(
                    restore,
                    "D47 could not be restored to {Path}; it is at {Retired}",
                    runningExecutable,
                    retired);
            }

            return false;
        }
    }

    /// <summary>
    /// Removes the executable a previous update retired. Called at startup, because that is the
    /// first moment the old image is no longer running and the file can actually be deleted.
    /// </summary>
    public void CleanUpRetired(string runningExecutable)
    {
        var retired = runningExecutable + RetiredSuffix;

        try
        {
            if (File.Exists(retired))
            {
                File.Delete(retired);
                logger.LogInformation("Removed the previous build left by an update");
            }

            // Staged downloads are equally spent once one has been installed.
            if (Directory.Exists(StagingFolder))
            {
                foreach (var stale in Directory.EnumerateFiles(StagingFolder, "d47-*.exe"))
                {
                    File.Delete(stale);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftovers are untidy, not broken. Never a reason to fail a startup.
            logger.LogDebug(ex, "Could not remove a file left by a previous update");
        }
    }

    private async Task DownloadToAsync(
        string url,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var file = File.Create(destination);

        var buffer = new byte[81920];
        long written = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;

            if (total > 0)
            {
                progress?.Report((double)written / total);
            }
        }
    }

    /// <summary>
    /// The hash out of a sha256sum sidecar — "&lt;hex&gt;  d47.exe". Only the hash is read; the
    /// filename beside it names a file on a build agent, not one here.
    /// </summary>
    private async Task<string?> ReadChecksumAsync(string url, CancellationToken cancellationToken)
    {
        var body = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

        return ParseChecksum(body);
    }

    internal static string? ParseChecksum(string? body)
    {
        var first = body?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        var hash = first?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        // A hex SHA-256 and nothing else. Anything shorter, longer or non-hex is a page that is
        // not a checksum file — an error page, say — rather than a hash worth comparing against.
        return hash is { Length: 64 } && hash.All(Uri.IsHexDigit) ? hash : null;
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(path);

        var hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexString(hash);
    }

    private void Discard(string path)
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
            logger.LogDebug(ex, "Could not delete a partial download");
        }
    }
}
