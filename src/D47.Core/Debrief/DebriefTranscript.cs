namespace D47.Core.Debrief;

/// <summary>
/// Who said one line of a session, and the whole of the poisoning defence at extraction time
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b><see cref="Game"/> exists so that untrusted text can be recorded and provably skipped.</b>
/// The alternative — never writing an in-game message down — reads safer and is worse: a
/// correction only makes sense next to what provoked it, and a session record with the provoking
/// line missing is one a reader cannot check. So everything is recorded and only
/// <see cref="Commander"/> is ever extracted from. A hostile message saying <em>from now on,
/// always…</em> is in the record, is visible, and produces nothing.
/// </para>
/// <para>
/// Decided by the caller and not recoverable afterwards, for the reason
/// <c>PanelViewModel.TranscriptVoice</c> is: once three voices are flattened into one page, the
/// only thing telling them apart is prose, and prose is what the attack is made of.
/// </para>
/// </summary>
public enum DebriefSpeaker
{
    /// <summary>
    /// The Commander, however they said it — spoken, typed, or through a switch. The only
    /// speaker <see cref="DebriefExtractor"/> reads.
    /// </summary>
    Commander,

    /// <summary>d47's own reply. Recorded for context; never extracted from.</summary>
    Ship,

    /// <summary>
    /// Anything that came from outside the two of them: an in-game message read out, a journal
    /// line, a web result quoted back. <b>Untrusted, and never extracted from</b> (architecture.md
    /// §7).
    /// </summary>
    Game,
}

/// <summary>
/// One line of a session, as the debrief pass reads it.
/// </summary>
/// <param name="When">When it was said. Supplied, because Core reads no clock.</param>
/// <param name="Who">Which of the three voices it was.</param>
/// <param name="Text">What was said, verbatim.</param>
/// <param name="Clip">
/// The audio flight recorder's row id for this line, where the recorder was running
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>). Null is the ordinary case
/// and costs nothing: the transcript alone is enough to draft a direction from, and the clip only
/// ever anchors a proposal to the exact audio for a Commander who wants to hear it back.
/// </param>
public sealed record DebriefLine(DateTimeOffset When, DebriefSpeaker Who, string Text, string? Clip = null);

/// <summary>
/// What a session sounded like, held for the length of that session and no longer
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>In memory, never on disk, and that is a decision rather than an omission.</b> Phase 31 wrote
/// down why the memory store holds facts and never a transcript — a rolling transcript is a privacy
/// liability, a context-window problem and a confabulation engine — and a debrief that persisted
/// one would have bought all three back for the convenience of running the pass tomorrow instead of
/// tonight. So the pass runs at the end of the session, over what is still in memory, and the only
/// thing that reaches disk is the handful of proposals it drafted, each one quoting the Commander's
/// own sentence. Kill d47 and the record is gone, which is the correct amount of it to survive.
/// </para>
/// <para>
/// Capped, for the reason every ring in this repository is capped: a session that runs for two days
/// must not grow until the machine notices. The oldest lines go first, which is also the right end
/// to lose — a correction the Commander made this evening is the one worth acting on.
/// </para>
/// <para>
/// No thread and no clock. Every <see cref="Say"/> carries its own instant, and the lock is here
/// because the speech loop and the panel write from different threads.
/// </para>
/// </summary>
public sealed class DebriefSession(int capacity = DebriefSession.DefaultCapacity)
{
    /// <summary>
    /// How many lines one session may hold. Generous — a long evening of talking is a few hundred
    /// — and bounded, which is the point.
    /// </summary>
    public const int DefaultCapacity = 2_000;

    /// <summary>
    /// The longest line kept. A reply runs long; a correction does not, and the whole of what this
    /// record is read for is the short end.
    /// </summary>
    public const int MaxLineLength = 2_000;

    private readonly Lock _gate = new();
    private readonly Queue<DebriefLine> _lines = new();
    private readonly int _capacity = capacity > 0 ? capacity : DefaultCapacity;

    /// <summary>Everything still held, oldest first.</summary>
    public IReadOnlyList<DebriefLine> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    /// <summary>Writes one line down. Blank text is nothing to record and is dropped.</summary>
    public void Say(DateTimeOffset when, DebriefSpeaker who, string text, string? clip = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var trimmed = text.Trim();

        if (trimmed.Length > MaxLineLength)
        {
            trimmed = trimmed[..MaxLineLength];
        }

        lock (_gate)
        {
            _lines.Enqueue(new DebriefLine(when, who, trimmed, clip));

            while (_lines.Count > _capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    /// <summary>
    /// Forgets everything. What a privacy erase reaches, and what a session boundary does once the
    /// pass has run over it.
    /// </summary>
    public void Empty()
    {
        lock (_gate)
        {
            _lines.Clear();
        }
    }
}

/// <summary>
/// The kinds of feedback nobody typed (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>These propose questions and never directions.</b> Cutting a sentence off might mean "you are
/// too verbose" and might mean the Commander needed to say something urgent; switching a warning
/// off might mean it fires too eagerly and might mean it fired correctly and was dealt with. A loop
/// that adapted to either reading silently would be a companion whose behaviour changed for reasons
/// its Commander could not name. So the debrief converts implicit into explicit — by asking — or
/// drops it.
/// </para>
/// </summary>
public enum DebriefSignalKind
{
    /// <summary>d47 was speaking and was stopped before it finished.</summary>
    SpeechCutOff,

    /// <summary>A warning fired and was switched off within seconds of firing.</summary>
    WarningDisabledSoonAfter,
}

/// <summary>
/// One thing that happened which nobody said anything about.
/// </summary>
/// <param name="When">The most recent occurrence, for ordering the questions.</param>
/// <param name="Kind">Which reading is being offered, never taken.</param>
/// <param name="What">
/// The subject, in the words a Commander would recognise — "the fuel warning", "a reply about
/// engineering". Written by the thing that observed it, never by a model.
/// </param>
/// <param name="Count">
/// How many times. One is noise and is deliberately below
/// <see cref="DebriefExtractor.SignalThreshold"/>; a pattern is what earns a question.
/// </param>
public sealed record DebriefSignal(DateTimeOffset When, DebriefSignalKind Kind, string What, int Count = 1);
