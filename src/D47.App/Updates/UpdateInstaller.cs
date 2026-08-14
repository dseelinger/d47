using System.IO.Compression;
using System.Security.Cryptography;
using D47.Core;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>Why an in-place update did not happen. The Commander is told which one it was.</summary>
public enum UpdateFailure
{
    /// <summary>The release carried no archive, or not one from this repository.</summary>
    NothingToInstall,

    /// <summary>The download did not complete.</summary>
    DownloadFailed,

    /// <summary>The bytes that arrived are not the bytes the release published.</summary>
    ChecksumMismatch,

    /// <summary>The archive verified but did not contain a d47 build.</summary>
    BadArchive,

    /// <summary>The build could not be replaced — most often a folder needing elevation.</summary>
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
/// only files ever replaced are d47's own executable and the native libraries a release ships
/// beside it.
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
    /// The suffix a replaced file is renamed to. Windows will not let a running image — the
    /// executable, or a native library the process has loaded — be overwritten or deleted, but
    /// it will let one be renamed, which is what makes this possible without a second helper
    /// process to babysit. Applies to every file the swap touches, not only the exe, because
    /// whisper.dll is just as loaded as d47.exe once a speech model is.
    /// </summary>
    internal const string RetiredSuffix = ".old";

    /// <summary>The one file an archive must contain to be a d47 release at all.</summary>
    private const string ExecutableName = "d47.exe";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Where downloads land: beside the executable, like everything else d47 writes.</summary>
    public string StagingFolder => Path.Combine(paths.Data, "updates");

    /// <summary>
    /// Fetches the release archive, verifies it and unpacks it. Returns the folder holding the
    /// new build's files, or the reason there is not one. Nothing is replaced here — a failed
    /// download leaves the running d47 untouched.
    /// </summary>
    public async Task<(string? Payload, UpdateFailure? Failure)> DownloadAsync(
        AvailableUpdate update,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!update.CanInstall)
        {
            return (null, UpdateFailure.NothingToInstall);
        }

        Directory.CreateDirectory(StagingFolder);

        var archive = Path.Combine(StagingFolder, $"d47-{update.Version}.zip");

        try
        {
            await DownloadToAsync(update.DownloadUrl!, archive, progress, cancellationToken)
                .ConfigureAwait(false);

            var published = await ReadChecksumAsync(update.ChecksumUrl!, cancellationToken)
                .ConfigureAwait(false);

            if (published is null)
            {
                Discard(archive);
                return (null, UpdateFailure.ChecksumMismatch);
            }

            var actual = await HashAsync(archive, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(actual, published, StringComparison.OrdinalIgnoreCase))
            {
                // Deleted, not kept for inspection: a file that failed its checksum is a file
                // nobody should be able to run by double-clicking it later.
                logger.LogError(
                    "The downloaded update did not match its published checksum; discarding it");

                Discard(archive);
                return (null, UpdateFailure.ChecksumMismatch);
            }

            var payload = Extract(archive, update.Version);

            if (payload is null)
            {
                return (null, UpdateFailure.BadArchive);
            }

            logger.LogInformation("Downloaded and verified D47 {Version}", update.Version);
            return (payload, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogWarning(ex, "Could not download the update");
            Discard(archive);
            return (null, UpdateFailure.DownloadFailed);
        }
    }

    /// <summary>
    /// Unpacks the verified archive and checks it actually holds a d47 build. The extraction is
    /// only ever from a checksum-verified asset of this repository, and ExtractToDirectory
    /// itself refuses entries that would escape the destination folder.
    /// </summary>
    internal string? Extract(string archive, string version)
    {
        var payload = Path.Combine(StagingFolder, $"d47-{version}");

        try
        {
            if (Directory.Exists(payload))
            {
                Directory.Delete(payload, recursive: true);
            }

            ZipFile.ExtractToDirectory(archive, payload);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The downloaded archive could not be unpacked");
            Discard(archive);
            return null;
        }

        // The zip verified, but verified against what the release published — this asks whether
        // what the release published is a build at all, before anything is replaced with it.
        if (!File.Exists(Path.Combine(payload, ExecutableName)))
        {
            logger.LogError("The downloaded archive holds no {Executable}; not installing it", ExecutableName);

            Discard(archive);
            TryDeleteFolder(payload);
            return null;
        }

        // Spent the moment it is unpacked; the payload folder is what the swap consumes.
        Discard(archive);
        return payload;
    }

    /// <summary>
    /// Puts the unpacked build where the running one is, file by file, by retiring what each new
    /// file displaces rather than overwriting it.
    /// <para>
    /// The executable goes last, as the commit point: every other file is in place before the
    /// exe changes, so the window in which the running name does not exist is a single rename
    /// wide. Native libraries under <c>runtimes\</c> that the new build no longer ships are
    /// retired too — Whisper's loader picks up whatever sits in that folder, and a stale
    /// library beside new ones is a load failure with nobody to blame. Any failure rolls the
    /// whole set back; the Commander never ends up with half an update.
    /// </para>
    /// </summary>
    internal bool TrySwap(string runningExecutable, string payloadFolder)
    {
        var installRoot = Path.GetDirectoryName(Path.GetFullPath(runningExecutable))!;
        var exeName = Path.GetFileName(runningExecutable);

        var shipped = Directory.EnumerateFiles(payloadFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(payloadFolder, file))
            .OrderBy(rel => string.Equals(rel, exeName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        // What has been done, so it can be undone. Source is null for a bare retirement.
        var done = new Stack<(string Destination, string? Source, string? Retired)>();

        try
        {
            foreach (var stale in StaleRuntimeFiles(installRoot, shipped))
            {
                done.Push((stale, null, Retire(stale)));
            }

            foreach (var rel in shipped)
            {
                var source = Path.Combine(payloadFolder, rel);
                var destination = Path.Combine(installRoot, rel);

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                var retired = File.Exists(destination) ? Retire(destination) : null;

                File.Move(source, destination);
                done.Push((destination, source, retired));
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not put the update in place; putting the old build back");

            RollBack(done);
            return false;
        }
    }

    /// <summary>Renames a file aside, replacing whatever an earlier update left there.</summary>
    private static string Retire(string file)
    {
        var retired = file + RetiredSuffix;

        if (File.Exists(retired))
        {
            File.Delete(retired);
        }

        File.Move(file, retired);
        return retired;
    }

    /// <summary>
    /// Files under <c>runtimes\</c> that the new build does not ship. Only that folder: it is
    /// the one place a release puts files whose mere presence changes behaviour, and everything
    /// else beside the exe is the Commander's data, which an update has no business touching.
    /// </summary>
    private static IEnumerable<string> StaleRuntimeFiles(string installRoot, List<string> shipped)
    {
        var runtimes = Path.Combine(installRoot, "runtimes");

        if (!Directory.Exists(runtimes))
        {
            yield break;
        }

        var keep = shipped.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(runtimes, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(installRoot, file);

            if (!keep.Contains(rel) && !file.EndsWith(RetiredSuffix, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private void RollBack(Stack<(string Destination, string? Source, string? Retired)> done)
    {
        while (done.TryPop(out var step))
        {
            try
            {
                if (step.Source is not null && File.Exists(step.Destination))
                {
                    File.Move(step.Destination, step.Source);
                }

                if (step.Retired is not null)
                {
                    File.Move(step.Retired, step.Destination);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nothing left to try in-process for this file. Said loudly because something
                // is now sitting next to where it belongs under a different name.
                logger.LogCritical(
                    ex,
                    "Could not restore {Path} while undoing a failed update; look for it at {Retired}",
                    step.Destination,
                    step.Retired ?? step.Source);
            }
        }
    }

    /// <summary>
    /// Removes what a previous update retired or staged. Called at startup, because that is the
    /// first moment the old image is no longer running and its files can actually be deleted.
    /// </summary>
    public void CleanUpRetired(string runningExecutable)
    {
        try
        {
            var installRoot = Path.GetDirectoryName(Path.GetFullPath(runningExecutable))!;
            var removed = false;

            foreach (var retired in RetiredFiles(runningExecutable, installRoot))
            {
                File.Delete(retired);
                removed = true;
            }

            if (removed)
            {
                logger.LogInformation("Removed what the previous update retired");
            }

            // Staged downloads are equally spent once one has been installed. The .exe glob is
            // for what updaters before v0.5.14 staged.
            if (Directory.Exists(StagingFolder))
            {
                foreach (var stale in Directory.EnumerateFiles(StagingFolder, "d47-*.*")
                             .Where(file => file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                            || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    File.Delete(stale);
                }

                foreach (var payload in Directory.EnumerateDirectories(StagingFolder, "d47-*"))
                {
                    Directory.Delete(payload, recursive: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftovers are untidy, not broken. Never a reason to fail a startup.
            logger.LogDebug(ex, "Could not remove a file left by a previous update");
        }
    }

    private static IEnumerable<string> RetiredFiles(string runningExecutable, string installRoot)
    {
        if (File.Exists(runningExecutable + RetiredSuffix))
        {
            yield return runningExecutable + RetiredSuffix;
        }

        var runtimes = Path.Combine(installRoot, "runtimes");

        if (Directory.Exists(runtimes))
        {
            foreach (var retired in Directory
                         .EnumerateFiles(runtimes, "*" + RetiredSuffix, SearchOption.AllDirectories))
            {
                yield return retired;
            }
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
    /// The hash out of a sha256sum sidecar — "&lt;hex&gt;  d47.zip". Only the hash is read; the
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

    private void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not delete an unpacked download");
        }
    }
}
