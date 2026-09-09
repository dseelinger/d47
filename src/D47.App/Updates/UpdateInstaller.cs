using System.IO.Compression;
using System.Security.Cryptography;
using D47.Core;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>Why an in-place update did not happen.</summary>
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
/// Downloads a newer d47 and puts it in place of the running one (Phase 19, "Check for Updates on
/// start" — "the user is given an opportunity to exit, install it, and restart").
/// </summary>
public sealed class UpdateInstaller(AppPaths paths, ILogger<UpdateInstaller> logger)
{
    /// <summary>The suffix a replaced file is renamed to.</summary>
    internal const string RetiredSuffix = ".old";

    /// <summary>The one file an archive must contain to be a d47 release at all.</summary>
    private const string ExecutableName = "d47.exe";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Where downloads land: beside the executable, like everything else d47 writes.</summary>
    public string StagingFolder => Path.Combine(paths.Data, "updates");

    /// <summary>Fetches the release archive, verifies it and unpacks it.</summary>
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
                // Deleted, not kept for inspection: a file that failed its checksum is a file nobody should
                // be able to run by double-clicking it later.
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

    /// <summary>Unpacks the verified archive and checks it actually holds a d47 build.</summary>
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

            // A half-written payload is dead weight — and on a full disk, it is sitting on the very space
            // whose absence just failed the extraction.
            TryDeleteFolder(payload);
            return null;
        }

        // The zip verified, but verified against what the release published — this asks whether what the
        // release published is a build at all, before anything is replaced with it.
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
    /// Puts the unpacked build where the running one is, file by file, by retiring what each new file
    /// displaces rather than overwriting it.
    /// </summary>
    internal bool TrySwap(string runningExecutable, string payloadFolder)
    {
        var installRoot = Path.GetDirectoryName(Path.GetFullPath(runningExecutable))!;

        var shipped = Directory.EnumerateFiles(payloadFolder, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(payloadFolder, file))
            .OrderBy(rel => string.Equals(rel, ExecutableName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        // What has been done, so it can be undone.
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

                // The archive's d47.exe goes where the running executable actually is, whatever the Commander
                // may have renamed it to; everything else keeps its archive-relative place beside it.
                var destination = string.Equals(rel, ExecutableName, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFullPath(runningExecutable)
                    : Path.Combine(installRoot, rel);

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                if (File.Exists(destination))
                {
                    done.Push((destination, null, Retire(destination)));
                }

                File.Move(source, destination);
                done.Push((destination, source, null));
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

    /// <summary>Files under <c>runtimes\</c> or <c>ships\</c> that the new build does not ship.</summary>
    private static IEnumerable<string> StaleRuntimeFiles(string installRoot, List<string> shipped)
    {
        var keep = shipped.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var owned in new[] { "runtimes", "ships" })
        {
            var folder = Path.Combine(installRoot, owned);

            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(installRoot, file);

                if (!keep.Contains(rel) && !file.EndsWith(RetiredSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
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
                // Nothing left to try in-process for this file.
                logger.LogCritical(
                    ex,
                    "Could not restore {Path} while undoing a failed update; look for it at {Retired}",
                    step.Destination,
                    step.Retired ?? step.Source);
            }
        }
    }

    /// <summary>Removes what a previous update retired or staged.</summary>
    public void CleanUpRetired(string runningExecutable)
    {
        try
        {
            var installRoot = Path.GetDirectoryName(Path.GetFullPath(runningExecutable))!;
            var removed = false;

            // File by file, each surviving the others' failures: the first start after an update overlaps the
            // outgoing build, whose d47.exe.old is still a running image and undeletable until next time.
            foreach (var retired in RetiredFiles(runningExecutable, installRoot))
            {
                removed |= TryDelete(retired);
            }

            if (removed)
            {
                logger.LogInformation("Removed what the previous update retired");
            }

            // Staged downloads are equally spent once one has been installed.
            if (Directory.Exists(StagingFolder))
            {
                foreach (var stale in Directory.EnumerateFiles(StagingFolder, "d47-*.*")
                             .Where(file => file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                            || file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    TryDelete(stale);
                }

                foreach (var payload in Directory.EnumerateDirectories(StagingFolder, "d47-*"))
                {
                    TryDeleteFolder(payload);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftovers are untidy, not broken.
            logger.LogDebug(ex, "Could not remove a file left by a previous update");
        }
    }

    private bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not remove {File} yet; the next start will try again", file);
            return false;
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

    /// <summary>The hash out of a sha256sum sidecar — "&lt;hex&gt; d47.zip".</summary>
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

        // A hex SHA-256 and nothing else.
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
