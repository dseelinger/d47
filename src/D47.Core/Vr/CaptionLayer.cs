namespace D47.Core.Vr;

/// <summary>
/// The rolling caption window (list.md Phase 9, "TheApp appears in the headset").
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

    private DateTimeOffset? _clearAt;
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

        // The standard caps one event at two lines. A sentence long enough to need more is
        // shown as consecutive events, which is what a caption track does with one too - the
        // window rolls and the reader keeps up, rather than a wall of text arriving at once.
        foreach (var line in wrapped)
        {
            _lines.Add(line);

            while (_lines.Count > Caption.WindowLines)
            {
                _lines.RemoveAt(0);
            }
        }

        _showing = text;

        // While the voice is still going there is no clear time. It is set when speech stops,
        // which is what "timed from the end of speech" means.
        _clearAt = null;
        Changed?.Invoke();
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

        _clearAt = now + Caption.DwellFor(_showing, Settings.Sane().CharactersPerSecond);
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
        _clearAt = null;
        _showing = string.Empty;
        _saying = null;
        Changed?.Invoke();
    }

    /// <summary>Expires the window when its dwell is up. Called from the tick loop.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (_clearAt is not { } clearAt || now < clearAt)
        {
            return;
        }

        Silence();
    }
}
