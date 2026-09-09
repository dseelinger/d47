namespace D47.Core.Vr;

/// <summary>The rolling caption window (Phase 9, "TheApp appears in the headset").</summary>
public sealed class CaptionLayer
{
    private readonly List<string> _lines = [];

    /// <summary>Lines of the current utterance that have not been shown yet (#200).</summary>
    private readonly Queue<string> _pending = new();

    /// <summary>When the next event of the current utterance goes up.</summary>
    private DateTimeOffset? _advanceAt;

    /// <summary>Whether the voice has stopped.</summary>
    private bool _quiet;

    private DateTimeOffset? _clearAt;

    /// <summary>The last thing said, kept for nothing but the log and the tests.</summary>
    private string _showing = string.Empty;
    private long? _saying;

    public CaptionSettings Settings { get; set; } = new();

    /// <summary>The window, oldest first.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public bool Visible => Settings.Enabled && _lines.Count > 0;

    /// <summary>Raised when the window changed, so the surface knows to redraw and only then.</summary>
    public event Action? Changed;

    /// <summary>One thing said.</summary>
    /// <param name="utterance">
    /// Which clip this is, so the same one arriving twice is one caption.
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

        // The standard caps one event at two lines, and a sentence long enough to need more is shown as
        // consecutive events — which is what a caption track does with one too.
        _pending.Clear();

        foreach (var line in wrapped)
        {
            _pending.Enqueue(line);
        }

        _showing = text;

        // While the voice is still going there is no clear time.
        _quiet = false;
        _clearAt = null;

        Advance(now);
        Changed?.Invoke();
    }

    /// <summary>
    /// Puts the next event on screen: up to <see cref="Caption.WindowLines"/> lines, appended and
    /// rolled, which is the roll-up form live captioning uses (#200).
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

        // The voice having already stopped is the ordinary case for anything longer than two lines: Quiet
        // arrives when the audio ends, which is before the reader has seen the end of it.
        if (_quiet)
        {
            _clearAt = now + dwell;
        }
    }

    /// <summary>The voice has stopped.</summary>
    public void Quiet(DateTimeOffset now)
    {
        if (_lines.Count == 0 || _clearAt is not null)
        {
            return;
        }

        _quiet = true;

        // Lines still waiting have not been read yet, so there is nothing to start counting down (#200).
        if (_pending.Count > 0)
        {
            return;
        }

        // Timed against everything still on screen, not only the sentence that just finished.
        _clearAt = now + Caption.DwellFor(string.Join(' ', _lines), Settings.Sane().CharactersPerSecond);
    }

    /// <summary>Interrupted.</summary>
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
    /// Advances to the next event when its turn comes, and expires the window when its dwell is up.
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
