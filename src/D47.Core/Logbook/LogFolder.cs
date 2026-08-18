using System.Globalization;
using System.Text;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>One log on disk.</summary>
public sealed record LogEntry(string Path, DateTimeOffset Written, long Bytes)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// The folder the logs live in (list.md Phase 33, item 1).
/// <para>
/// <b><c>data/commander-log/</c>, and deliberately not <c>data/logs/</c></b>, which
/// <see cref="AppPaths.Logs"/> already owns and fills with d47's own diagnostics. Two different
/// things called "logs" one folder apart is a support question waiting to be asked, and the one
/// the Commander cares about is the one that should not need explaining.
/// </para>
/// <para>
/// <b>Plain markdown, and a run never overwrites.</b> The item says this is the one artefact of d47
/// that leaves the machine because the Commander chose to take it — which means it is theirs, they
/// will edit it, and a second run for the same day must not silently replace the version they had
/// already worked on.
/// </para>
/// </summary>
public sealed class LogFolder(string folder, ILogger<LogFolder> logger)
{
    public const string FolderName = "commander-log";

    public string Folder => folder;

    /// <summary>
    /// Writes one log and answers where it went. The name carries the date and the span, so a
    /// folder of them sorts and reads without being opened.
    /// </summary>
    public string Write(string content, LogRange range, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(range);

        var stem = $"{now:yyyy-MM-dd}-{LogRanges.IdOf(range.Span)}";
        var path = System.IO.Path.Combine(folder, $"{stem}.md");

        // Never overwrite. The first one may already have been edited, and this is the one file in
        // data/ that d47 does not own.
        for (var suffix = 2; File.Exists(path) && suffix < 1000; suffix++)
        {
            path = System.IO.Path.Combine(folder, $"{stem}-{suffix.ToString(CultureInfo.InvariantCulture)}.md");
        }

        AtomicFile.WriteAllText(path, content);
        logger.LogInformation("Wrote a Commander's log to {Path}", path);

        return path;
    }

    /// <summary>What is in the folder, newest first.</summary>
    public IReadOnlyList<LogEntry> Entries()
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return
            [
                .. Directory.EnumerateFiles(folder, "*.md")
                    .Select(path => new FileInfo(path))
                    .Select(file => new LogEntry(file.FullName, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), file.Length))
                    .OrderByDescending(entry => entry.Written),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list the Commander's logs in {Folder}", folder);
            return [];
        }
    }

    /// <summary>What the panel row and the readback tool both say about the folder.</summary>
    public string Describe()
    {
        var entries = Entries();

        if (entries.Count == 0)
        {
            return $"No logs written yet. They will go to {folder}.";
        }

        var text = new StringBuilder();

        text.Append(entries.Count.ToString("N0", CultureInfo.InvariantCulture))
            .Append(entries.Count == 1 ? " log in " : " logs in ")
            .AppendLine(folder);

        foreach (var entry in entries.Take(10))
        {
            text.Append("- ").Append(entry.Name).Append(" — ")
                .Append(entry.Written.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        if (entries.Count > 10)
        {
            text.Append("- and ")
                .Append((entries.Count - 10).ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" more");
        }

        return text.ToString().TrimEnd();
    }
}
