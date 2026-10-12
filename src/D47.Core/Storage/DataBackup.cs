using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace D47.Core.Storage;

/// <summary>Zips the data folder when a different build first opens it, keeping the newest <see cref="Kept"/> zips.</summary>
public static class DataBackup
{
    public const int Kept = 25;

    private const string Prefix = "data-backup-";
    private const string Extension = ".zip";

    /// <summary>Top-level folders left out: downloads, recordings, logs and the Commander's music, which are large and which a build does not change.</summary>
    public static readonly IReadOnlySet<string> LeftOut = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "audio", "avatar-clips", "backups", "flight", "logs", "models", "ships", "stories",
    };

    /// <summary>Sorts by time; <paramref name="label"/> names the build that took it.</summary>
    public static string FileName(DateTime at, string label) =>
        $"{Prefix}{at:yyyyMMdd-HHmmss}-{string.Join('-', label.Split(Path.GetInvalidFileNameChars()))}{Extension}";

    /// <summary>The backups among <paramref name="paths"/> older than the newest <see cref="Kept"/>. Other files are never named.</summary>
    public static IReadOnlyList<string> Expired(IEnumerable<string> paths) =>
    [
        .. paths
            .Where(path => Path.GetFileName(path) is var name
                && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Skip(Kept),
    ];

    /// <summary>
    /// When <paramref name="build"/> is not the build recorded in <see cref="AppPaths.LastBuildFile"/>, zips the data
    /// folder, deletes the expired zips and records the build. Logs a failure rather than throwing it, and records
    /// nothing, so the next start tries again.
    /// </summary>
    public static void RunOnNewBuild(AppPaths paths, string build, string label, DateTime at, IFileSystem files, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var target = Path.Combine(paths.Backups, FileName(at, label));

        try
        {
            var last = files.ReadText(paths.LastBuildFile)?.Trim();

            if (string.Equals(last, build, StringComparison.Ordinal))
            {
                return;
            }

            var (added, unreadable) = Zip(paths.Data, target, files);

            foreach (var expired in Expired(files.Enumerate(paths.Backups, Prefix + "*" + Extension)))
            {
                files.Delete(expired);
            }

            files.WriteText(paths.LastBuildFile, build);

            logger.LogInformation(
                "A different build opened {Data} (was {Last}); backed up {Files} files to {Backup}, {Unreadable} could not be read",
                paths.Data,
                last ?? "none recorded",
                added,
                added > 0 ? target : "nothing",
                unreadable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not back up {Data} to {Backup}", paths.Data, target);
        }
    }

    /// <summary>Writes nothing when no file was added.</summary>
    private static (int Added, int Unreadable) Zip(string data, string target, IFileSystem files)
    {
        var added = 0;
        var unreadable = 0;

        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in files.Enumerate(data, "*", recursive: true))
            {
                var relative = Path.GetRelativePath(data, path);
                var separator = relative.IndexOf(Path.DirectorySeparatorChar, StringComparison.Ordinal);

                if (separator > 0 && LeftOut.Contains(relative[..separator]))
                {
                    continue;
                }

                byte[]? bytes;

                try
                {
                    bytes = files.ReadBytes(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    unreadable++;
                    continue;
                }

                if (bytes is null)
                {
                    continue;
                }

                var entry = zip.CreateEntry(relative.Replace(Path.DirectorySeparatorChar, '/'), CompressionLevel.Fastest);

                using (var stream = entry.Open())
                {
                    stream.Write(bytes);
                }

                added++;
            }
        }

        if (added > 0)
        {
            files.WriteBytes(target, buffer.ToArray());
        }

        return (added, unreadable);
    }
}
