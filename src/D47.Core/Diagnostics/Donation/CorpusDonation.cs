using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// What a corpus donation carries and what was taken out of it
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>Deliberately not <see cref="ExcerptTally"/>.</b> That record has a log half — entries, the
/// Commander's own speech, whether it travelled — and a corpus donation has no log half at all.
/// Reusing it would put zeros in four fields that mean something, and a zero that means "not
/// applicable" reads exactly like a zero that means "none found".
/// </para>
/// </summary>
/// <param name="Events">Events that travelled.</param>
/// <param name="Withheld">Events dropped whole because the scrubber could not read them.</param>
/// <param name="NamesReplaced">Distinct names and IDs given a stand-in across the whole donation.</param>
/// <param name="InGameMessages">Message bodies dropped — another player's words are not the donor's to give.</param>
/// <param name="LinksDropped">Fields removed outright rather than replaced, chiefly the squadron link.</param>
/// <param name="Unreadable">
/// Lines the journal parser could not read at all, so they never became events and appear in no
/// other count here.
/// <para>
/// <b>Counted because this whole feature is about what a reader cannot see.</b> The excerpt path
/// drops these silently and can afford to — a Commander reads that payload in full, so a missing
/// line is a missing line they could notice. Nobody reads a corpus, so a silent drop is invisible
/// by construction, and a consent step that hides its own losses is the thing #174 exists to
/// avoid.
/// </para>
/// </param>
public sealed record CorpusTally(
    int Events,
    int Withheld,
    int NamesReplaced,
    int InGameMessages,
    int LinksDropped,
    int Unreadable);

/// <summary>
/// Everything a reader is asked to consent to, in the size they can actually read it
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// </summary>
/// <param name="First">The earliest event that travelled, or null where nothing did.</param>
/// <param name="Last">The latest.</param>
/// <param name="Files">How many journal files were opened.</param>
/// <param name="Bytes">How large the payload will be, counted rather than estimated.</param>
/// <param name="Tally">What travelled and what did not.</param>
/// <param name="Kinds">One entry per distinct event kind, with a real scrubbed instance of each.</param>
public sealed record CorpusSurvey(
    DateTimeOffset? First,
    DateTimeOffset? Last,
    int Files,
    long Bytes,
    CorpusTally Tally,
    IReadOnlyList<KindCensus> Kinds);

/// <summary>
/// Reads a whole journal history, scrubs it, and describes the result without holding it
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>Two passes, and no temporary file.</b> The survey pass scrubs everything and keeps only the
/// counts and one sample line per kind; the write pass scrubs it again straight into whatever sink
/// the Commander chose. Nothing is written anywhere until they have said yes, and memory stays at
/// one journal file whatever the corpus weighs — 383 MB of history never exists in one place.
/// </para>
/// <para>
/// <b>Both passes are handed the same <see cref="Pseudonyms"/>.</b> That is what makes the samples
/// in the report byte-identical to the lines in the payload, which is #160's "what is shown is what
/// leaves" surviving into a payload nobody can read in full.
/// <para>
/// Two fresh instances over identical input would in fact agree — stand-ins are issued in
/// encounter order, so the same walk produces the same numbering. But that is a property of the
/// second walk being an exact repeat of the first, and it stops holding the moment anything between
/// them differs: a different range, a journal file Elite appended to while the Commander was
/// reading, a caller that seeds one pass and not the other. Sharing the instance makes the
/// guarantee independent of all of that, which is worth more than the allocation it saves.
/// </para>
/// </para>
/// <para>
/// <b>Within one donation, stand-ins are consistent; across donations they are not.</b> A replay
/// corpus where the same Commander is CMDR ALPHA in one file and CMDR DELTA in the next is not a
/// corpus. Whether donations should be joinable to <i>each other</i> is a different question and
/// deliberately not settled here — see
/// <a href="https://github.com/dseelinger/d47/issues/176">#176</a>.
/// </para>
/// </summary>
public static class CorpusDonation
{
    /// <summary>
    /// Walks the history and reports what is in it, keeping one scrubbed line per kind.
    /// </summary>
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
    /// Walks it again and writes the payload, one scrubbed event per line, oldest first — the order
    /// a replay needs, which is the order this is collected for.
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

    /// <summary>
    /// One journal file at a time, oldest first, scrubbed and handed on. The caller keeps whatever
    /// it needs; nothing accumulates here.
    /// </summary>
    /// <summary>What a walk covered, as opposed to what it produced.</summary>
    private sealed record Walked(int Files, int Unreadable);

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

        // The same file-overlap rule the excerpt path uses, borrowed rather than restated: a
        // session that began before the range can still hold events inside it.
        foreach (var file in IncidentSources.Overlapping(files, from, to))
        {
            cancel.ThrowIfCancellationRequested();

            // One file's events, then let them go. A JournalLog per file rather than one for the
            // corpus, because the whole point is that the corpus never sits in memory at once —
            // and this is the existing road from a parsed event to a Kind and a Compact line.
            var log = new JournalLog(keep: int.MaxValue);
            var parsed = new List<JournalEvent>();

            foreach (var line in IncidentSources.ReadLines(file))
            {
                if (!JournalEvent.TryParse(line, logger ?? NullLogger.Instance, out var journalEvent)
                    || journalEvent is not { } read)
                {
                    // A blank line is not an unreadable one — a journal file ends with one and it
                    // means nothing. Anything else here is a line that will appear in no count on
                    // the report unless it is counted now.
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
