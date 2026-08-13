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
    public void Say(string text, DateTimeOffset now)
    {
        if (!Settings.Enabled)
        {
            return;
        }

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
