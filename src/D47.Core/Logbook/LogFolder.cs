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

/// <summary>The folder the logs live in (Phase 33, item 1).</summary>
public sealed class LogFolder(string folder, IFileSystem files, ILogger<LogFolder> logger)
{
    public const string FolderName = "commander-log";

    public string Folder => folder;

    /// <summary>Writes one log and answers where it went.</summary>
    public string Write(string content, LogRange range, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(range);

        var stem = $"{now:yyyy-MM-dd}-{LogRanges.IdOf(range.Span)}";
        var path = System.IO.Path.Combine(folder, $"{stem}.md");

        // Never overwrite.
        for (var suffix = 2; files.Stat(path) is not null && suffix < 1000; suffix++)
        {
            path = System.IO.Path.Combine(folder, $"{stem}-{suffix.ToString(CultureInfo.InvariantCulture)}.md");
        }

        files.WriteText(path, content);
        logger.LogInformation("Wrote a Commander's log to {Path}", path);

        return path;
    }

    /// <summary>What is in the folder, newest first.</summary>
    public IReadOnlyList<LogEntry> Entries()
    {
        try
        {
            return
            [
                .. files.Enumerate(folder, "*.md")
                    .Select(path => (Path: path, State: files.Stat(path)))
                    .Where(file => file.State is not null)
                    .Select(file => new LogEntry(file.Path, new DateTimeOffset(file.State!.Value.Written, TimeSpan.Zero), file.State.Value.Length))
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
