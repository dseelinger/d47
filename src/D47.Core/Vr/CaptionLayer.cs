namespace D47.Core.Vr;

/// <summary>
/// The rolling caption window (Phase 9, "TheApp appears in the headset").
/// <para>
/// Output only, and unmovable. It is a separate overlay handle from the panel precisely so
/// that the placement settings cannot reach it: a caption that the Commander can drag is a
/// caption they can drag somewhere they will not see it, and the one thing a caption has to be
/// is where they are already looking.
/// </para>
/// <para>
/// It owns no clock. Speech tells it when a line starts and when the voice stops, and the tick
/// loop tells it what time it is — so a whole evening's worth of captions can be replayed in a
/// test in microseconds.
/// </para>
/// </summary>
public sealed class CaptionLayer
{
    private readonly List<string> _lines = [];

    /// <summary>
    /// Lines of the current utterance that have not been shown yet
    /// (<a href="https://github.com/dseelinger/d47/issues/200">#200</a>).
    /// <para>
    /// <b>The queue is the whole of the fix.</b> The roll-off used to run <em>inside</em> the loop
    /// that was still adding the wrapped lines, and the whole loop was synchronous with one
    /// <c>Changed</c> at the end — so a sentence wrapping to eight lines had six of them added and
    /// removed between two frames and the surface was told once, about the last two. The comment
    /// above that loop described consecutive events; there was no timing between the iterations,
    /// so there were none.
    /// </para>
    /// </summary>
    private readonly Queue<string> _pending = new();

    /// <summary>When the next event of the current utterance goes up. Null when none is waiting.</summary>
    private DateTimeOffset? _advanceAt;

    /// <summary>
    /// Whether the voice has stopped. Kept because the dwell cannot start while lines are still
    /// waiting to be shown: <see cref="Quiet"/> arrives when the audio ends, which for a long
    /// sentence is before the reader has seen the end of it.
    /// </summary>
    private bool _quiet;

    private DateTimeOffset? _clearAt;

    /// <summary>The last thing said, kept for nothing but the log and the tests.</summary>
    private string _showing = string.Empty;
    private long? _saying;

    public CaptionSettings Settings { get; set; } = new();

    /// <summary>The window, oldest first. Never longer than <see cref="Caption.WindowLines"/>.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public bool Visible => Settings.Enabled && _lines.Count > 0;

    /// <summary>Raised when the window changed, so the surface knows to redraw and only then.</summary>
    public event Action? Changed;

    /// <summary>
    /// One thing said. Called as each clip starts playing, which is per sentence — the speech
    /// pipeline already splits a reply at sentence boundaries so it can start speaking before
    /// the model has finished, and a sentence is the right size for a caption event anyway.
    /// </summary>
    /// <param name="utterance">
    /// Which clip this is, so the same one arriving twice is one caption.
    /// <para>
    /// It arrives twice as a matter of course. The audio arbiter re-raises a snapshot of
    /// everything audible for every change to any of it — the next sentence being queued behind
    /// this one, the thinking bed stopping, a music track starting — and each of those carries
    /// the current clip's caption again. Without an identity to compare, every one of them was
    /// another <c>Say</c>: the line went onto the screen twice, and a three-line window filled
    /// with two copies of one sentence (remediation.md, "Only the first caption arrives").
    /// </para>
    /// <para>
    /// Null means "no identity", which is always a new caption. That is what the tests and the
    /// audition path pass, and it is the honest answer for a caller that has no clip.
    /// </para>
    /// </param>
    public void Say(string text, DateTimeOffset now, long? utterance = null)
    {
        if (!Settings.Enabled)
        {
            return;
        }

        if (utterance is not null && utterance == _saying)
        {
            return;
        }

        _saying = utterance;

        var wrapped = Caption.Wrap(text);

        if (wrapped.Count == 0)
        {
            return;
        }

        // The standard caps one event at two lines, and a sentence long enough to need more is
        // shown as consecutive events — which is what a caption track does with one too. The
        // queue is what makes them consecutive; before it, every line but the last two was added
        // and rolled off between two frames and nobody ever saw the beginning of a long answer
        // (#200).
        //
        // **A new utterance replaces whatever is still waiting, and that is a ruling rather than
        // an accident.** The FCC asks captions to be complete *and* synchronous, and when a
        // reader's chosen speed is slower than the voice those two want different things. A
        // caption still working through a sentence the voice finished with ten seconds ago has
        // stopped captioning and started transcribing. So the voice wins — and what is lost is
        // the tail, which the reader can see moved on, rather than the head, which they cannot.
        // In practice they rarely conflict: a full event is 84 characters, which is 4.2 seconds
        // at the default reading speed against roughly 5.6 seconds to say.
        _pending.Clear();

        foreach (var line in wrapped)
        {
            _pending.Enqueue(line);
        }

        _showing = text;

        // While the voice is still going there is no clear time. It is set when speech stops,
        // which is what "timed from the end of speech" means.
        _quiet = false;
        _clearAt = null;

        Advance(now);
        Changed?.Invoke();
    }

    /// <summary>
    /// Puts the next event on screen: up to <see cref="Caption.WindowLines"/> lines, appended and
    /// rolled, which is the roll-up form live captioning uses (#200).
    /// <para>
    /// Appending rather than replacing is what keeps consecutive <em>short</em> sentences sharing
    /// the window — one line each, the older rolling off — while a single long one still arrives
    /// two lines at a time, because appending two lines to a two-line window leaves exactly those
    /// two.
    /// </para>
    /// <para>
    /// The interval between events is the same <see cref="Caption.DwellFor"/> that decides how
    /// long the last one lingers, measured against everything on screen. One rule for how long
    /// text stays readable, applied in both places, rather than a second constant that can
    /// disagree with the reading-speed row.
    /// </para>
    /// </summary>
    private void Advance(DateTimeOffset now)
    {
        for (var taken = 0; taken < Caption.WindowLines && _pending.Count > 0; taken++)
        {
            _lines.Add(_pending.Dequeue());

            while (_lines.Count > Caption.WindowLines)
            {
                _lines.RemoveAt(0);
            }
        }

        var dwell = Caption.DwellFor(string.Join(' ', _lines), Settings.Sane().CharactersPerSecond);

        if (_pending.Count > 0)
        {
            _advanceAt = now + dwell;
            _clearAt = null;
            return;
        }

        _advanceAt = null;

        // The voice having already stopped is the ordinary case for anything longer than two
        // lines: Quiet arrives when the audio ends, which is before the reader has seen the end
        // of it. So the dwell on the last event starts here rather than there.
        if (_quiet)
        {
            _clearAt = now + dwell;
        }
    }

    /// <summary>
    /// The voice has stopped. Starts the dwell, so the last thing said stays readable for as
    /// long as the standard says it takes to read it.
    /// </summary>
    public void Quiet(DateTimeOffset now)
    {
        if (_lines.Count == 0 || _clearAt is not null)
        {
            return;
        }

        _quiet = true;

        // Lines still waiting have not been read yet, so there is nothing to start counting down
        // (#200). The voice ending is not the reader finishing — for anything past two lines the
        // audio stops several events early — so the dwell is started by whichever of the two
        // finishes last, which is the queue draining in Advance.
        if (_pending.Count > 0)
        {
            return;
        }

        // Timed against everything still on screen, not only the sentence that just finished.
        // The window is a roll-up: what a reader is catching up on when the voice stops is the
        // lines in front of them, and timing a short last line on its own put the whole window
        // away after five sixths of a second — reported as the caption vanishing the moment the
        // voice did (remediation.md, "Captions: which standard, and how long").
        _clearAt = now + Caption.DwellFor(string.Join(' ', _lines), Settings.Sane().CharactersPerSecond);
    }

    /// <summary>
    /// Interrupted. The Commander said stop, so the captions go with the voice — a caption
    /// still sitting there after a silence command is d47 visibly not having stopped.
    /// </summary>
    public void Silence()
    {
        if (_lines.Count == 0)
        {
            return;
        }

        _lines.Clear();
        _pending.Clear();
        _advanceAt = null;
        _quiet = false;
        _clearAt = null;
        _showing = string.Empty;
        _saying = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Advances to the next event when its turn comes, and expires the window when its dwell is
    /// up. Called from the tick loop.
    /// <para>
    /// Advancing is checked first: a queue that still has lines in it has no clear time set, so
    /// the two can never both be due — but reading it in the other order would make that a fact
    /// about <see cref="Advance"/> that this method quietly relies on.
    /// </para>
    /// </summary>
    public void Tick(DateTimeOffset now)
    {
        if (_advanceAt is { } advanceAt && now >= advanceAt)
        {
            Advance(now);
            Changed?.Invoke();
            return;
        }

        if (_clearAt is not { } clearAt || now < clearAt)
        {
            return;
        }

        Silence();
    }
}
