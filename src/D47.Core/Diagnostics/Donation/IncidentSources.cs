using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Where an excerpt's two halves come from when it has to reach past the running session
/// (<a href="https://github.com/dseelinger/d47/issues/173">#173</a>).
/// <para>
/// <b>Both halves used to come from memory, and neither said how far that was.</b> The journal
/// half read <c>JournalLog</c>, which holds 4,000 events fed by a spine that tails the newest
/// journal file — so it reached the start of the current Elite session and stopped. The log half
/// read the newest <c>d47-*.log</c>, so it could not cross midnight. A Commander who restarted d47
/// and then asked for sixty minutes got twenty, silently. The defect was never that the window was
/// small; it was that the control implied a reach the sources did not have.
/// </para>
/// <para>
/// <b>This reads files, and it is deliberately the only part of the donation path that does.</b>
/// <see cref="IncidentExcerpt.Take"/> still takes values and still opens nothing, which is what
/// keeps the scrubbing drivable from a test with no machine underneath it. What is on disk and what
/// is done to it stay separate questions.
/// </para>
/// </summary>
public static class IncidentSources
{
    /// <summary>
    /// Elite's own events across every journal that overlaps the window, oldest first.
    /// <para>
    /// <b>The filename is the index.</b> Elite names these <c>Journal.yyyy-MM-ddTHHmmss.NN.log</c>
    /// and the name encodes when the session started, so a file that started after the window ended
    /// cannot hold anything in it and is never opened. The lower bound needs more care: a session
    /// running for six hours has a start long before the window and events inside it, so the file
    /// that was open when the window began is included whatever its name says.
    /// </para>
    /// </summary>
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

        // Oldest first, which is the order a replay needs and the reverse of the order the page
        // wants. JournalLog.Read hands back newest-first for the reader's sake.
        return [.. log.Read(noise: true).Reverse()];
    }

    /// <summary>
    /// d47's own log across every retained day the window touches, oldest first.
    /// <para>
    /// <b>The day comes from the filename, and it has to.</b> The human-readable sink writes a time
    /// of day and no date — it rolls daily and the name carries it — so once an excerpt spans more
    /// than one file, <c>14:02:11</c> is ambiguous between them. Parsing each file against its own
    /// date is what lets the window compare real instants instead of times of day, and it is why
    /// the wrap-around-midnight special case this used to need is gone.
    /// </para>
    /// </summary>
    /// <param name="zone">
    /// The zone the sink wrote in. The file holds local wall-clock time with no offset, so turning
    /// it back into an instant needs the zone stated rather than assumed — the same reason nothing
    /// in Core reads <c>TimeZoneInfo.Local</c> for itself.
    /// </param>
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

            // A day either side, because a window's edges are instants and a file's day is local:
            // the first entry of a file can fall on the previous UTC day, and the last on the next.
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
    /// The files that could hold something in the window: every one starting at or before it ends,
    /// back to and including the one that was open when it began.
    /// </summary>
    internal static IEnumerable<string> Overlapping(List<string> files, DateTimeOffset from, DateTimeOffset to)
    {
        var started = files.Select(file => (File: file, At: StartedAt(file))).ToList();

        // Everything that began before the window ended. A file beginning after it cannot hold an
        // event inside it, because Elite writes a session's events into the session's own file.
        var candidates = started.Where(entry => entry.At is null || entry.At <= to).ToList();

        // And drop the ones that also ended before the window began — which is every file older
        // than the last one to start at or before `from`.
        var open = candidates.LastOrDefault(entry => entry.At is not null && entry.At <= from);

        return candidates
            .Where(entry => open.File is null || string.CompareOrdinal(entry.File, open.File) >= 0)
            .Select(entry => entry.File);
    }

    /// <summary>
    /// When a journal file's session began, from its name, or null where the name does not parse —
    /// which is a file this does not exclude rather than one it drops, because a name it cannot
    /// read is not evidence of anything.
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

    /// <summary>
    /// Shared with whatever is still writing, deletion included. Elite holds the current journal
    /// open and Serilog holds today's log open; a reader that forbade either would throw on exactly
    /// the two files an incident is most likely to be in.
    /// </summary>
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
