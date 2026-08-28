using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>
/// What span a log covers (Phase 33, "A session becomes something worth keeping").
/// <para>
/// The item says <em>a session — or a week</em> and both halves are here. A session is not a
/// duration and cannot be expressed as one, which is why this is a closed set rather than a
/// number of days with a special case bolted to it.
/// </para>
/// </summary>
public enum LogSpan
{
    /// <summary>Since the last <c>LoadGame</c>. The default, and the one a Commander asks for most.</summary>
    Session,

    /// <summary>Since local midnight.</summary>
    Today,

    Week,

    Month,

    /// <summary>Two instants the Commander named. Reachable from the panel, never from a phrase.</summary>
    Between,
}

/// <summary>
/// One resolved window, with the words for it. Both ends are absolute instants by the time
/// anything downstream sees this — no component below here gets to reinterpret "a week".
/// </summary>
public sealed record LogRange
{
    public required LogSpan Span { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>How the header and the spoken answer name this window.</summary>
    public required string Label { get; init; }

    public TimeSpan Length => To > From ? To - From : TimeSpan.Zero;

    public bool Holds(DateTimeOffset at) => at >= From && at <= To;

    /// <summary>The two ends as dates, which is how the file is named and how the header reads.</summary>
    public string Dates =>
        From.Date == To.Date
            ? From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{From:yyyy-MM-dd} to {To:yyyy-MM-dd}";
}

/// <summary>
/// Turns a span and an instant into a window, and — for a session — reads the journals to find
/// where it started.
/// <para>
/// <b>A session starts at the last <c>LoadGame</c>, not at the last file.</b> Elite rolls a long
/// session into a continuation journal without re-emitting <c>LoadGame</c>, so taking the newest
/// file would silently start the log halfway through the evening. <see cref="SessionSummary"/>
/// already records this trap; this is the second reader that has to know about it.
/// </para>
/// <para>
/// No clock is read here, like everywhere else in Core: the instant arrives as an argument, and
/// the App is what knows what time it is.
/// </para>
/// </summary>
public static class LogRanges
{
    /// <summary>
    /// The value the settings row and the tool parameter use for each span. Ids rather than
    /// labels, because these go in the settings file and into a tool call.
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } = ["session", "today", "week", "month"];

    public static string IdOf(LogSpan span) => span switch
    {
        LogSpan.Session => "session",
        LogSpan.Today => "today",
        LogSpan.Week => "week",
        LogSpan.Month => "month",
        _ => "between",
    };

    public static string LabelOf(string id) => Parse(id) switch
    {
        LogSpan.Session => "the last session",
        LogSpan.Today => "today",
        LogSpan.Week => "the last seven days",
        LogSpan.Month => "the last thirty days",
        _ => "a range of dates",
    };

    /// <summary>
    /// Reads a span id, falling back to a session. A value nobody recognises is the same state as
    /// no value at all, and the fallback is the cheapest window rather than the widest — a
    /// misspelt setting should not silently buy thirty days of prose.
    /// </summary>
    public static LogSpan Parse(string? id) => (id ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "today" => LogSpan.Today,
        "week" => LogSpan.Week,
        "month" => LogSpan.Month,
        "between" => LogSpan.Between,
        _ => LogSpan.Session,
    };

    /// <summary>
    /// Two named instants. Ordered here rather than trusted, because a panel with two date
    /// pickers can produce them the wrong way round and a window with a negative length reads
    /// back as "nothing happened".
    /// </summary>
    public static LogRange Between(DateTimeOffset from, DateTimeOffset to)
    {
        var (start, end) = from <= to ? (from, to) : (to, from);

        return new LogRange
        {
            Span = LogSpan.Between,
            From = start,
            To = end,
            Label = start.Date == end.Date
                ? $"{start:yyyy-MM-dd}"
                : $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}",
        };
    }

    /// <summary>
    /// Resolves a span against the corpus. <paramref name="files"/> is only read for
    /// <see cref="LogSpan.Session"/>; every other span is arithmetic on <paramref name="now"/>.
    /// </summary>
    public static LogRange Resolve(
        LogSpan span,
        DateTimeOffset now,
        IReadOnlyList<string> files,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);

        return span switch
        {
            LogSpan.Today => new LogRange
            {
                Span = span,
                From = new DateTimeOffset(now.Date, now.Offset),
                To = now,
                Label = "today",
            },

            LogSpan.Week => new LogRange
            {
                Span = span,
                From = now.AddDays(-7),
                To = now,
                Label = "the last seven days",
            },

            LogSpan.Month => new LogRange
            {
                Span = span,
                From = now.AddDays(-30),
                To = now,
                Label = "the last thirty days",
            },

            _ => Session(now, files, logger),
        };
    }

    /// <summary>
    /// Walks the newest journals backwards for the most recent <c>LoadGame</c>.
    /// <para>
    /// Backwards and stopping at the first hit, because the answer is almost always in the last
    /// file and reading thirteen months to find something in the final one would make the cheapest
    /// range the slowest. Eight files is the bound — a session split across more continuation
    /// journals than that has not been observed, and the fallback below is honest rather than
    /// wrong.
    /// </para>
    /// </summary>
    private static LogRange Session(DateTimeOffset now, IReadOnlyList<string> files, ILogger logger)
    {
        const int LookBack = 8;

        for (var index = files.Count - 1; index >= 0 && index > files.Count - 1 - LookBack; index--)
        {
            DateTimeOffset? started = null;

            try
            {
                foreach (var line in File.ReadLines(files[index]))
                {
                    if (!line.Contains("\"LoadGame\"", StringComparison.Ordinal) ||
                        !JournalEvent.TryParse(line, logger, out var parsed) ||
                        parsed is not { Kind: "LoadGame" })
                    {
                        continue;
                    }

                    started = parsed.Timestamp;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not read {File} looking for the session start", files[index]);
                continue;
            }

            if (started is { } at)
            {
                return new LogRange
                {
                    Span = LogSpan.Session,
                    From = at,
                    To = now > at ? now : at,
                    Label = "the last session",
                };
            }
        }

        // No LoadGame anywhere in reach. A day is the honest fallback and it says so, rather than
        // reporting an empty session — which would read as "you did nothing" instead of "I could
        // not find where you started".
        logger.LogInformation("No LoadGame found in the last {Count} journals; falling back to a day", LookBack);

        return new LogRange
        {
            Span = LogSpan.Session,
            From = now.AddDays(-1),
            To = now,
            Label = "the last day — I could not find where your last session started",
        };
    }

    /// <summary>
    /// The journals that could hold events inside a window, by the timestamp in their own names.
    /// <para>
    /// <b>One file earlier than the window is included deliberately.</b> Elite names a journal for
    /// the moment the session began, so a session that started at 23:50 and ran past midnight is
    /// entirely inside a file named for the previous day — filtering on the name alone would drop
    /// the first hour of every late-night log.
    /// </para>
    /// <para>
    /// Filtering on names rather than reading everything is what keeps a one-day log from walking
    /// thirteen months. Anything that slips through is dropped again by timestamp when the events
    /// are folded, so this is an optimisation rather than a second definition of the window.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> FilesFor(IReadOnlyList<string> files, LogRange range)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(range);

        var kept = new List<string>();
        string? previous = null;

        foreach (var file in files)
        {
            var started = StartedAt(file);

            if (started is null)
            {
                // A name that will not parse is read rather than skipped. The cost is one extra
                // file; the cost of the other choice is a silently short log.
                kept.Add(file);
                continue;
            }

            if (started > range.To)
            {
                break;
            }

            if (started >= range.From)
            {
                if (previous is not null)
                {
                    kept.Add(previous);
                    previous = null;
                }

                kept.Add(file);
            }
            else
            {
                previous = file;
            }
        }

        if (previous is not null && kept.Count == 0)
        {
            kept.Add(previous);
        }

        return kept;
    }

    /// <summary>
    /// The instant in a journal's filename — <c>Journal.2026-08-18T193021.01.log</c>. Null for a
    /// name that does not carry one.
    /// </summary>
    public static DateTimeOffset? StartedAt(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
        var parts = name.Split('.');

        if (parts.Length < 2)
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            parts[1],
            "yyyy-MM-dd'T'HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
