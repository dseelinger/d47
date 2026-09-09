using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Diagnostics.Donation;

/// <summary>What a corpus donation carries and what was taken out of it (#174).</summary>
/// <param name="Events">Events that travelled.</param>
/// <param name="Withheld">Events dropped whole because the scrubber could not read them.</param>
/// <param name="NamesReplaced">
/// Distinct names and IDs given a stand-in across the whole donation.
/// </param>
/// <param name="InGameMessages">
/// Message bodies dropped — another player's words are not the donor's to give.
/// </param>
/// <param name="LinksDropped">
/// Fields removed outright rather than replaced, chiefly the squadron link.
/// </param>
/// <param name="Unreadable">
/// Lines the journal parser could not read at all, so they never became events and appear in no other
/// count here.
/// </param>
public sealed record CorpusTally(
    int Events,
    int Withheld,
    int NamesReplaced,
    int InGameMessages,
    int LinksDropped,
    int Unreadable);

/// <summary>Everything a reader is asked to consent to, in the size they can actually read it (#174).</summary>
/// <param name="First">The earliest event that travelled, or null where nothing did.</param>
/// <param name="Last">The latest.</param>
/// <param name="Files">How many journal files were opened.</param>
/// <param name="Bytes">How large the payload will be, counted rather than estimated.</param>
/// <param name="Tally">What travelled and what did not.</param>
/// <param name="Kinds">
/// One entry per distinct event kind, with a real scrubbed instance of each.
/// </param>
public sealed record CorpusSurvey(
    DateTimeOffset? First,
    DateTimeOffset? Last,
    int Files,
    long Bytes,
    CorpusTally Tally,
    IReadOnlyList<KindCensus> Kinds);

/// <summary>
/// Reads a whole journal history, scrubs it, and describes the result without holding it (#174).
/// </summary>
public static class CorpusDonation
{
    /// <summary>Walks the history and reports what is in it, keeping one scrubbed line per kind.</summary>
    public static CorpusSurvey Survey(
        string folder,
        DateTimeOffset from,
        DateTimeOffset to,
        Pseudonyms names,
        ILogger? logger = null,
        IProgress<int>? progress = null,
        CancellationToken cancel = default)
    {
        var census = new CorpusCensus();
        var events = 0;
        var withheld = 0;
        var messages = 0;
        var links = 0;
        var bytes = 0L;
        DateTimeOffset? first = null;
        DateTimeOffset? last = null;

        var walked = Walk(folder, from, to, names, logger, progress, cancel, (entry, scrubbed) =>
        {
            census.Saw(entry.Kind, entry.Compact, scrubbed.Json);

            if (scrubbed.Json is not { } line)
            {
                withheld++;
                return;
            }

            events++;
            messages += scrubbed.BodiesDropped;
            links += scrubbed.FieldsDropped;

            // The payload is one line per event, so the newline is part of what it will weigh.
            bytes += line.Length + Environment.NewLine.Length;

            first ??= entry.Timestamp;
            last = entry.Timestamp;
        });

        return new CorpusSurvey(
            first,
            last,
            walked.Files,
            bytes,
            new CorpusTally(events, withheld, names.Count, messages, links, walked.Unreadable),
            census.Kinds);
    }

    /// <summary>
    /// Walks it again and writes the payload, one scrubbed event per line, oldest first — the order a
    /// replay needs, which is the order this is collected for.
    /// </summary>
    /// <returns>What actually travelled, so a caller can check it against the survey.</returns>
    public static CorpusTally Write(
        string folder,
        DateTimeOffset from,
        DateTimeOffset to,
        Pseudonyms names,
        TextWriter sink,
        ILogger? logger = null,
        IProgress<int>? progress = null,
        CancellationToken cancel = default)
    {
        var events = 0;
        var withheld = 0;
        var messages = 0;
        var links = 0;

        var walked = Walk(folder, from, to, names, logger, progress, cancel, (_, scrubbed) =>
        {
            if (scrubbed.Json is not { } line)
            {
                withheld++;
                return;
            }

            events++;
            messages += scrubbed.BodiesDropped;
            links += scrubbed.FieldsDropped;
            sink.WriteLine(line);
        });

        return new CorpusTally(events, withheld, names.Count, messages, links, walked.Unreadable);
    }

    /// <summary>One journal file at a time, oldest first, scrubbed and handed on.</summary>
    private sealed record Walked(int Files, int Unreadable);

    /// <summary>
    /// <returns>How many files were opened, and how many lines in them were unreadable.</returns>
    /// </summary>
    /// <returns>How many files were opened, and how many lines in them were unreadable.</returns>
    private static Walked Walk(
        string folder,
        DateTimeOffset from,
        DateTimeOffset to,
        Pseudonyms names,
        ILogger? logger,
        IProgress<int>? progress,
        CancellationToken cancel,
        Action<JournalEntry, ScrubbedLine> onEvent)
    {
        if (!Directory.Exists(folder))
        {
            return new Walked(0, 0);
        }

        var files = Directory
            .EnumerateFiles(folder, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var opened = 0;
        var unreadable = 0;

        // The same file-overlap rule the excerpt path uses, borrowed rather than restated: a session that
        // began before the range can still hold events inside it.
        foreach (var file in IncidentSources.Overlapping(files, from, to))
        {
            cancel.ThrowIfCancellationRequested();

            // One file's events, then let them go.
            var log = new JournalLog(keep: int.MaxValue);
            var parsed = new List<JournalEvent>();

            foreach (var line in IncidentSources.ReadLines(file))
            {
                if (!JournalEvent.TryParse(line, logger ?? NullLogger.Instance, out var journalEvent)
                    || journalEvent is not { } read)
                {
                    // A blank line is not an unreadable one — a journal file ends with one and it means
                    // nothing.
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        unreadable++;
                    }

                    continue;
                }

                if (read.Timestamp >= from && read.Timestamp <= to)
                {
                    parsed.Add(read);
                }
            }

            log.Add(parsed);

            foreach (var entry in log.Entries)
            {
                onEvent(entry, JournalScrub.Line(entry.Compact, names));
            }

            opened++;
            progress?.Report(opened);
        }

        return new Walked(opened, unreadable);
    }
}
