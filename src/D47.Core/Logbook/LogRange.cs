using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>What span a log covers (Phase 33, "A session becomes something worth keeping").</summary>
public enum LogSpan
{
    /// <summary>Since the last <c>LoadGame</c>.</summary>
    Session,

    /// <summary>Since local midnight.</summary>
    Today,

    Week,

    Month,

    /// <summary>Two instants the Commander named.</summary>
    Between,
}

/// <summary>One resolved window, with the words for it.</summary>
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
/// Turns a span and an instant into a window, and — for a session — reads the journals to find where it
/// started.
/// </summary>
public static class LogRanges
{
    /// <summary>The value the settings row and the tool parameter use for each span.</summary>
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

    /// <summary>Reads a span id, falling back to a session.</summary>
    public static LogSpan Parse(string? id) => (id ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "today" => LogSpan.Today,
        "week" => LogSpan.Week,
        "month" => LogSpan.Month,
        "between" => LogSpan.Between,
        _ => LogSpan.Session,
    };

    /// <summary>Two named instants.</summary>
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

    /// <summary>Resolves a span against the corpus.</summary>
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

    /// <summary>Walks the newest journals backwards for the most recent <c>LoadGame</c>.</summary>
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

        // No LoadGame anywhere in reach.
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
                // A name that will not parse is read rather than skipped.
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

    /// <summary>The instant in a journal's filename — <c>Journal.2026-08-18T193021.01.log</c>.</summary>
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
