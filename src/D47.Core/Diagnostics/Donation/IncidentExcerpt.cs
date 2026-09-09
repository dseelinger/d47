using D47.Core.Journal;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Which incident, how much of it, and the one choice the Commander gets to make about it (#160).
/// </summary>
/// <param name="MarkedAt">The bookmark.</param>
/// <param name="Before">How far back from the mark.</param>
/// <param name="After">How far past it.</param>
/// <param name="IncludeMySpeech">Whether the Commander's own words travel.</param>
public sealed record ExcerptRequest(
    DateTimeOffset MarkedAt,
    TimeSpan Before,
    TimeSpan After,
    bool IncludeMySpeech)
{
    /// <summary>The start of the window.</summary>
    public DateTimeOffset From => MarkedAt - Before;

    /// <summary>The end of it.</summary>
    public DateTimeOffset To => MarkedAt + After;
}

/// <summary>
/// What the excerpt contains and what it left out — the numbers the report prints so a reader can see
/// the shape of what is missing without being shown it.
/// </summary>
/// <param name="JournalEvents">Events that travelled.</param>
/// <param name="JournalWithheld">
/// Events dropped whole because the scrubber could not read them.
/// </param>
/// <param name="NamesReplaced">
/// Distinct names and IDs given a stand-in, across both halves.
/// </param>
/// <param name="LogEntries">d47 log entries that travelled.</param>
/// <param name="MySpeechLines">
/// Entries carrying the Commander's own words, travelling or not.
/// </param>
/// <param name="MySpeechIncluded">Whether those travelled.</param>
/// <param name="LinksDropped">
/// Fields removed outright rather than replaced — in practice the flag saying which minor faction
/// belongs to the Commander's squadron, which would otherwise undo the squadron scrub in one hop.
/// </param>
/// <param name="InGameMessages">
/// Messages whose words were dropped, counted across both halves — Elite's own <c>ReceiveText</c> and
/// <c>SendText</c> events, and the log entries where d47 re-voiced one.
/// </param>
public sealed record ExcerptTally(
    int JournalEvents,
    int JournalWithheld,
    int NamesReplaced,
    int LogEntries,
    int MySpeechLines,
    bool MySpeechIncluded,
    int InGameMessages,
    int LinksDropped);

/// <summary>One incident, in the two halves a report wants: the replay case and the diagnosis (#160).</summary>
/// <param name="From">The start of the window, as asked for.</param>
/// <param name="To">The end of it.</param>
/// <param name="Journal">Scrubbed journal lines, oldest first.</param>
/// <param name="Log">Redacted d47 log entries, oldest first.</param>
/// <param name="Tally">What travelled and what did not.</param>
public sealed record IncidentExcerpt(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<string> Journal,
    IReadOnlyList<string> Log,
    ExcerptTally Tally)
{
    /// <summary>Cuts the window out of what is already in memory and scrubs it.</summary>
    /// <param name="alsoReplace">
    /// Extra literal substitutions for the log half, longest first.
    /// </param>
    /// <param name="commander">
    /// Who is flying, from outside the window — and without it the log half leaks the name the journal
    /// half was scrubbed of.
    /// </param>
    /// <param name="carrier">
    /// The Commander's fleet carrier, likewise from outside the window.
    /// </param>
    public static IncidentExcerpt Take(
        IReadOnlyList<JournalEntry> journal,
        IReadOnlyList<LogEntry> log,
        ExcerptRequest request,
        IReadOnlyList<KeyValuePair<string, string>>? alsoReplace = null,
        CommanderIdentity? commander = null,
        CarrierState? carrier = null)
    {
        var names = Seeded(commander, carrier);

        var events = new List<string>();
        var withheld = 0;
        var messages = 0;
        var links = 0;

        // Oldest first, whatever order the source held them in.
        foreach (var entry in journal
                     .Where(entry => entry.Timestamp >= request.From && entry.Timestamp <= request.To)
                     .OrderBy(entry => entry.Timestamp))
        {
            var scrubbed = JournalScrub.Line(entry.Compact, names);

            if (scrubbed.Json is { } line)
            {
                events.Add(line);
                messages += scrubbed.BodiesDropped;
                links += scrubbed.FieldsDropped;
            }
            else
            {
                withheld++;
            }
        }

        var lines = new List<string>();
        var mine = 0;

        foreach (var entry in log.Where(entry => entry.At >= request.From && entry.At <= request.To))
        {
            if (entry.Voice == LogVoice.Commander)
            {
                mine++;

                if (!request.IncludeMySpeech)
                {
                    continue;
                }
            }

            if (entry.Voice == LogVoice.InGame)
            {
                messages++;
            }

            lines.Add(LogScrub.Redact(entry, names, alsoReplace));
        }

        return new IncidentExcerpt(
            request.From,
            request.To,
            events,
            lines,
            new ExcerptTally(
                events.Count,
                withheld,
                names.Count,
                lines.Count,
                mine,
                request.IncludeMySpeech,
                messages,
                links));
    }

    /// <summary>A fresh set of stand-ins with the identities that sit outside any window already in it.</summary>
    public static Pseudonyms Seeded(CommanderIdentity? commander, CarrierState? carrier)
    {
        var names = new Pseudonyms();

        if (commander is not null)
        {
            names.Person(commander.Name);
            names.FrontierId(commander.FrontierId);
        }

        if (carrier is not null)
        {
            if (carrier.Name is { Length: > 0 } named)
            {
                names.Carrier(named);
            }

            if (carrier.CallSign is { Length: > 0 } call)
            {
                names.Callsign(call);
            }
        }

        return names;
    }
}
