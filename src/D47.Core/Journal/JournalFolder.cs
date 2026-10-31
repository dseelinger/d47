using System.Globalization;
using System.Text.Json;
using D47.Core.Storage;

namespace D47.Core.Journal;

/// <summary>Elite writes one file per session into one folder shared by every Commander on the machine.</summary>
public static class JournalFolder
{
    public const string FilePattern = "Journal.*.log";

    /// <summary>How far a file's first <c>timestamp</c> may precede the instant its name gives.</summary>
    private static readonly TimeSpan HeaderSlack = TimeSpan.FromMinutes(1);

    /// <summary>The default location Elite Dangerous writes to on Windows.</summary>
    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games",
            "Frontier Developments",
            "Elite Dangerous");

    /// <summary>The newest journal file by <see cref="Ordered"/>, or null when the folder is missing or has none.</summary>
    public static string? LatestFile(IFileSystem files, string directory, TimeZoneInfo? zone = null) =>
        Ordered(files, directory, zone) is { Count: > 0 } ordered ? ordered[^1] : null;

    /// <summary>
    /// Journal files oldest first, by the UTC instant each session started, then by name. Names with no stamp
    /// go first, by name.
    /// </summary>
    public static IReadOnlyList<string> Ordered(IFileSystem files, string directory, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        return [.. files.Enumerate(directory, FilePattern)
            .Select(path => (Path: path, Started: StartedAt(files, path, zone)))
            .OrderBy(file => file.Started.HasValue)
            .ThenBy(file => file.Started)
            .ThenBy(file => Path.GetFileName(file.Path), StringComparer.Ordinal)
            .Select(file => file.Path)];
    }

    /// <summary>
    /// The UTC instant a journal's session started, or null for a name that carries no stamp. The name is
    /// local time in <paramref name="zone"/>, the local zone when null; an hour the zone repeats is resolved
    /// from the file's first line, or as the later instant when that line cannot be read.
    /// </summary>
    public static DateTimeOffset? StartedAt(IFileSystem files, string path, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        var name = Path.GetFileName(path);

        // "Journal." is eight characters; the stamp is the seventeen after it.
        if (name.Length < 25
            || !DateTime.TryParseExact(
                name.Substring(8, 17),
                "yyyy-MM-dd'T'HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }

        zone ??= TimeZoneInfo.Local;

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var earlier = new DateTimeOffset(local, offsets.Max()).ToUniversalTime();
            var later = new DateTimeOffset(local, offsets.Min()).ToUniversalTime();

            return FirstTimestamp(files, path) is { } header && header < later - HeaderSlack ? earlier : later;
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>
    /// The index of the first file whose session started at or after <paramref name="since"/> in an
    /// oldest-first list, or -1 when every file is older.
    /// </summary>
    public static int FirstFileAt(IFileSystem fileSystem, IReadOnlyList<string> files, DateTimeOffset since, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        for (var i = 0; i < files.Count; i++)
        {
            if (StartedAt(fileSystem, files[i], zone) >= since)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The <c>timestamp</c> of a file's first line, or null when it is missing or cannot be read.</summary>
    private static DateTimeOffset? FirstTimestamp(IFileSystem files, string path)
    {
        try
        {
            using var stream = files.OpenRead(path);

            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);

            if (reader.ReadLine() is not { Length: > 0 } line)
            {
                return null;
            }

            using var document = JsonDocument.Parse(line);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("timestamp", out var timestamp)
                && timestamp.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(
                    timestamp.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var stamp)
                ? stamp
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
