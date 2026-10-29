using D47.Core.Storage;

namespace D47.Core.Journal;

/// <summary>Elite writes one file per session into one folder shared by every Commander on the machine.</summary>
public static class JournalFolder
{
    public const string FilePattern = "Journal.*.log";

    /// <summary>The default location Elite Dangerous writes to on Windows.</summary>
    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games",
            "Frontier Developments",
            "Elite Dangerous");

    /// <summary>
    /// The newest journal file by ordinal filename sort, or null if the folder is missing or has none —
    /// a legitimate state (no Elite install, or Elite has never run) rather than an error.
    /// </summary>
    public static string? LatestFile(IFileSystem files, string directory) =>
        files.Enumerate(directory, FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .LastOrDefault();

    /// <summary>
    /// The index of the first file named at or after <paramref name="since"/> in an oldest-first list, or -1
    /// when every file is older.
    /// </summary>
    public static int FirstFileAt(IReadOnlyList<string> files, DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(files);

        // Compared as text against the same shape the folder's own ordering already relies on:
        // Journal.2026-08-22T190000.01.log.
        var cutoff = "Journal." + since.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        for (var i = 0; i < files.Count; i++)
        {
            if (string.CompareOrdinal(Path.GetFileName(files[i]), cutoff) >= 0)
            {
                return i;
            }
        }

        return -1;
    }
}
