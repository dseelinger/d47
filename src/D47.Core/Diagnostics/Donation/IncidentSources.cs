using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Where an excerpt's two halves come from when it has to reach past the running session (#173).
/// </summary>
public static class IncidentSources
{
    /// <summary>Elite's own events across every journal that overlaps the window, oldest first.</summary>
    public static IReadOnlyList<JournalEntry> Journals(
        string folder,
        DateTimeOffset from,
        DateTimeOffset to,
        ILogger? logger = null)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(folder, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var log = new JournalLog(keep: int.MaxValue);

        foreach (var file in Overlapping(files, from, to))
        {
            foreach (var line in ReadLines(file))
            {
                if (JournalEvent.TryParse(line, logger ?? NullLogger.Instance, out var parsed)
                    && parsed is { } journalEvent
                    && journalEvent.Timestamp >= from
                    && journalEvent.Timestamp <= to)
                {
                    log.Add([journalEvent]);
                }
            }
        }

        // Oldest first, which is the order a replay needs and the reverse of the order the page wants.
        return [.. log.Read(noise: true).Reverse()];
    }

    /// <summary>d47's own log across every retained day the window touches, oldest first.</summary>
    /// <param name="zone">The zone the sink wrote in.</param>
    public static IReadOnlyList<LogEntry> Logs(
        string folder,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo zone)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var entries = new List<LogEntry>();

        foreach (var file in Directory.EnumerateFiles(folder, "d47-*.log").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (DayOf(file) is not { } day)
            {
                continue;
            }

            // A day either side, because a window's edges are instants and a file's day is local: the first
            // entry of a file can fall on the previous UTC day, and the last on the next.
            if (day < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, zone).DateTime).AddDays(-1) ||
                day > DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, zone).DateTime).AddDays(1))
            {
                continue;
            }

            foreach (var entry in LogScrub.Parse(ReadAll(file), day, zone))
            {
                if (entry.At >= from && entry.At <= to)
                {
                    entries.Add(entry);
                }
            }
        }

        return [.. entries.OrderBy(entry => entry.At)];
    }

    /// <summary>
    /// The files that could hold something in the window: every one starting at or before it ends, back
    /// to and including the one that was open when it began.
    /// </summary>
    internal static IEnumerable<string> Overlapping(List<string> files, DateTimeOffset from, DateTimeOffset to)
    {
        var started = files.Select(file => (File: file, At: StartedAt(file))).ToList();

        // Everything that began before the window ended.
        var candidates = started.Where(entry => entry.At is null || entry.At <= to).ToList();

        // And drop the ones that also ended before the window began — which is every file older than the last
        // one to start at or before `from`.
        var open = candidates.LastOrDefault(entry => entry.At is not null && entry.At <= from);

        return candidates
            .Where(entry => open.File is null || string.CompareOrdinal(entry.File, open.File) >= 0)
            .Select(entry => entry.File);
    }

    /// <summary>
    /// When a journal file's session began, from its name, or null where the name does not parse —
    /// which is a file this does not exclude rather than one it drops, because a name it cannot read is
    /// not evidence of anything.
    /// </summary>
    internal static DateTimeOffset? StartedAt(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var parts = name.Split('.');

        if (parts.Length < 2)
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            parts[1],
            "yyyy-MM-ddTHHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var at)
            ? at
            : null;
    }

    /// <summary>The day a <c>d47-yyyyMMdd.log</c> holds, or null where the name does not say.</summary>
    internal static DateOnly? DayOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        return name.Length > 4
               && DateOnly.TryParseExact(name[4..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;
    }

    /// <summary>Shared with whatever is still writing, deletion included.</summary>
    internal static IEnumerable<string> ReadLines(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static string ReadAll(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);

        return reader.ReadToEnd();
    }
}
