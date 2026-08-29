using D47.Core.Journal;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Which incident, how much of it, and the one choice the Commander gets to make about it
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// </summary>
/// <param name="MarkedAt">
/// The bookmark. <b>The outburst is not the payload</b> — the frustrated sentence that marks an
/// incident is used to find the window and then thrown away, and this instant is all that is left
/// of it.
/// </param>
/// <param name="Before">How far back from the mark. Most of a window: the symptom precedes the swearing.</param>
/// <param name="After">
/// How far past it. Not zero by default, because a Commander says "note that for the report" while
/// a thing is going wrong at least as often as afterwards.
/// </param>
/// <param name="IncludeMySpeech">
/// Whether the Commander's own words travel. <b>Off unless they say so</b>, and asked per incident
/// rather than remembered: sometimes the exact words are the bug — a mishearing is reproduced by
/// what was misheard — and sometimes they are nobody's business.
/// </param>
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
/// What the excerpt contains and what it left out — the numbers the report prints so a reader can
/// see the shape of what is missing without being shown it.
/// </summary>
/// <param name="JournalEvents">Events that travelled.</param>
/// <param name="JournalWithheld">Events dropped whole because the scrubber could not read them.</param>
/// <param name="NamesReplaced">Distinct names and IDs given a stand-in, across both halves.</param>
/// <param name="LogEntries">d47 log entries that travelled.</param>
/// <param name="MySpeechLines">Entries carrying the Commander's own words, travelling or not.</param>
/// <param name="MySpeechIncluded">Whether those travelled.</param>
/// <param name="InGameMessages">
/// Messages whose words were dropped, counted <b>across both halves</b> — Elite's own
/// <c>ReceiveText</c> and <c>SendText</c> events, and the log entries where d47 re-voiced one.
/// There is no switch for these, and the two halves are added rather than reported apart because
/// the claim the report makes is about the excerpt and not about either half of it.
/// </param>
public sealed record ExcerptTally(
    int JournalEvents,
    int JournalWithheld,
    int NamesReplaced,
    int LogEntries,
    int MySpeechLines,
    bool MySpeechIncluded,
    int InGameMessages);

/// <summary>
/// One incident, in the two halves a report wants: <b>the replay case and the diagnosis</b>
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// The journal half is what <c>spike/CorpusReplay</c> can drive — Elite's own events, scrubbed by
/// field list, one per line and in the order they happened, which is the order a replay needs. The
/// log half is evidence of what this build did with them. Neither half is the other's summary, and
/// a report wants both.
/// </para>
/// </summary>
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
    /// <summary>
    /// Cuts the window out of what is already in memory and scrubs it.
    /// <para>
    /// <b>Reads no clock and opens no file.</b> The mark comes in on the request and the two halves
    /// come in as values — the journal from <see cref="JournalLog"/>, which the tick loop already
    /// feeds, and the log as the text a surface read. So this is drivable from a test with no
    /// machine underneath it, which is the property the whole excerpt idea rests on.
    /// </para>
    /// <para>
    /// <b>One <see cref="Pseudonyms"/> across both halves</b>, journal first. The journal is the
    /// half with a schema, so it is where a real name is recognised as a name; the log half then
    /// substitutes what that pass learned. Doing it the other way round would leave the log with
    /// nothing to substitute.
    /// </para>
    /// </summary>
    /// <param name="zone">
    /// The zone the log's timestamps are in. The human-readable file carries a time of day and no
    /// date — it rolls daily and the filename holds the date — so the window has to be brought to
    /// local time to select on it. A value rather than <c>TimeZoneInfo.Local</c>, for the reason
    /// nothing in Core reads a clock.
    /// </param>
    /// <param name="alsoReplace">
    /// Extra literal substitutions for the log half, longest first. The Windows account name goes
    /// here: see <see cref="LogScrub.Redact"/>.
    /// </param>
    /// <param name="commander">
    /// Who is flying, from outside the window — and <b>without it the log half leaks the name the
    /// journal half was scrubbed of</b>. Elite writes <c>Commander</c> and <c>LoadGame</c> once, at
    /// the front of a session, so an incident three hours in contains neither; the log meanwhile
    /// names the Commander in what d47 <em>says</em> — <i>"JOHN DEPARAGON is in Eurybia, docked
    /// at…"</i> — a hundred times a day. Found flying it against a real session, which is the only
    /// place the two halves are ever both real.
    /// <para>
    /// The ship's given name has the same shape of hole and is deliberately not plugged here: it is
    /// the Commander's own name for their own ship rather than an identity, and the log half's
    /// control is the show step. This one is plugged because an identity is what the journal half's
    /// whole field list exists to remove.
    /// </para>
    /// </param>
    public static IncidentExcerpt Take(
        IReadOnlyList<JournalEntry> journal,
        string log,
        ExcerptRequest request,
        TimeZoneInfo zone,
        IReadOnlyList<KeyValuePair<string, string>>? alsoReplace = null,
        CommanderIdentity? commander = null)
    {
        var names = new Pseudonyms();

        // First, so the Commander is CMDR ALPHA rather than whoever the window happened to name
        // first — and so a stand-in exists for the log pass whether or not the window carries the
        // events that would have created one.
        if (commander is not null)
        {
            names.Person(commander.Name);
            names.FrontierId(commander.FrontierId);
        }

        var events = new List<string>();
        var withheld = 0;
        var messages = 0;

        // Oldest first, whatever order the source held them in. The page shows newest first
        // because that is what a reader wants; a replay is not a reader, and an excerpt handed to
        // one backwards is a state machine driven backwards.
        foreach (var entry in journal
                     .Where(entry => entry.Timestamp >= request.From && entry.Timestamp <= request.To)
                     .OrderBy(entry => entry.Timestamp))
        {
            var scrubbed = JournalScrub.Line(entry.Compact, names);

            if (scrubbed.Json is { } line)
            {
                events.Add(line);
                messages += scrubbed.BodiesDropped;
            }
            else
            {
                withheld++;
            }
        }

        var from = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.From, zone).DateTime);
        var to = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.To, zone).DateTime);

        var lines = new List<string>();
        var mine = 0;

        foreach (var entry in LogScrub.Parse(log).Where(entry => Within(entry.At, from, to)))
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
                messages));
    }

    /// <summary>
    /// Whether a time of day falls in the window, <b>wrap-aware</b>. A Commander flying at
    /// midnight has a window whose start is a larger number than its end, and a plain
    /// <c>&gt;= from &amp;&amp; &lt;= to</c> answers no to every line in it.
    /// </summary>
    private static bool Within(TimeOnly at, TimeOnly from, TimeOnly to) =>
        from <= to ? at >= from && at <= to : at >= from || at <= to;
}
