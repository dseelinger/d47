namespace D47.Core.Journal;

/// <summary>
/// Elite writes one file per session into one folder shared by every Commander on the
/// machine. Picking "the latest" is a filename comparison, not a filesystem-timestamp one,
/// because the filename already encodes the session's start time and that is what survives a
/// copy (list.md Phase 2).
/// </summary>
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
    /// The newest journal file by ordinal filename sort, or null if the folder is missing or
    /// has none — a legitimate state (no Elite install, or Elite has never run) rather than
    /// an error.
    /// </summary>
    public static string? LatestFile(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .LastOrDefault();
    }
}
