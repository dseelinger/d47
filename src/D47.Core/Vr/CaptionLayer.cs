namespace D47.Core.Vr;

/// <summary>
/// The caption band (Phase 9, "TheApp appears in the headset"): D47's rolling caption window, and the subtitle cue
/// covering a video's position.
/// </summary>
public sealed class CaptionLayer
{
    /// <summary>D47's own window.</summary>
    private readonly List<string> _lines = [];

    /// <summary>Lines of the current utterance that have not been shown yet (#200).</summary>
    private readonly Queue<string> _pending = new();

    /// <summary>When the next event of the current utterance goes up.</summary>
    private DateTimeOffset? _advanceAt;

    /// <summary>Whether the voice has stopped.</summary>
    private bool _quiet;

    private DateTimeOffset? _clearAt;
    private long? _saying;

    /// <summary>Measured speaking speed, characters a second.</summary>
    public const double InitialSpeakingSpeed = 13;
    public const double SlowestSpeakingSpeed = 8;
    public const double FastestSpeakingSpeed = 20;
    private const double SpeedWeight = 0.3;
    private static readonly TimeSpan ShortestSampled = TimeSpan.FromSeconds(1.5);

    private DateTimeOffset? _sayStartedAt;
    private int _sayCharacters;

    /// <summary>The subtitle cues being followed, or null with no video.</summary>
    private IReadOnlyList<SubtitleCue>? _cues;

    private SubtitleCue? _cue;

    /// <summary>The shown cue, wrapped.</summary>
    private IReadOnlyList<string> _cueLines = [];

    /// <summary>The last playback position given.</summary>
    private TimeSpan? _position;

    /// <summary>The playback position when the voice stopped.</summary>
    private TimeSpan? _quietAt;

    /// <summary>Running average of how fast sentences that ran to their end were spoken.</summary>
    public double SpeakingSpeed { get; private set; } = InitialSpeakingSpeed;

    public CaptionSettings Settings { get; set; } = new();

    /// <summary>
    /// The band's text, oldest first: D47's window, or the subtitle. D47's lines take the band alone, and the
    /// subtitle comes back once the voice has stopped and the video has moved on, or once D47's lines clear.
    /// </summary>
    public IReadOnlyList<string> Lines => _lines.Count > 0 ? _lines : _cueLines;

    public bool Visible => Settings.Enabled && Lines.Count > 0;

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

        Sample(now);
        _quietAt = null;
        _sayStartedAt = now;
        _sayCharacters = string.Join(' ', wrapped).Length;

        // A sentence long enough to need more lines than one event holds is shown as consecutive events — which is what a caption track does with one too.
        _pending.Clear();

        foreach (var line in wrapped)
        {
            _pending.Enqueue(line);
        }

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
        var eventCharacters = 0;

        for (var taken = 0; taken < Caption.WindowLines && _pending.Count > 0; taken++)
        {
            var line = _pending.Dequeue();
            eventCharacters += line.Length + (taken > 0 ? 1 : 0);
            _lines.Add(line);

            while (_lines.Count > Caption.WindowLines)
            {
                _lines.RemoveAt(0);
            }
        }

        var dwell = Caption.DwellFor(string.Join(' ', _lines), Settings.Sane().CharactersPerSecond);

        if (_pending.Count > 0)
        {
            // Held until the voice has reached the end of this event.
            var speaking = TimeSpan.FromSeconds(Math.Max(
                eventCharacters / SpeakingSpeed,
                Caption.MinimumDwell.TotalSeconds));

            _advanceAt = now + (speaking > dwell ? speaking : dwell);
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
        Sample(now);

        if (_lines.Count == 0 || _clearAt is not null)
        {
            return;
        }

        _quiet = true;
        _quietAt = _position;

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

        Forget();
        Changed?.Invoke();
    }

    /// <summary>Follows <paramref name="cues"/>, a video's subtitles, from the next <see cref="At"/>; null for none.</summary>
    public void Follow(IReadOnlyList<SubtitleCue>? cues)
    {
        _cues = cues;
        _position = null;
        _quietAt = null;

        if (_cue is null)
        {
            return;
        }

        _cue = null;
        _cueLines = [];
        Changed?.Invoke();
    }

    /// <summary>
    /// Follows <paramref name="cues"/> in place of the cues being followed, for the same video, as when more have been
    /// written: its position and D47's lines are kept, and the cue covering the position is shown at once.
    /// </summary>
    public void Extend(IReadOnlyList<SubtitleCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);

        if (_cues is null)
        {
            Follow(cues);
            return;
        }

        _cues = cues;

        if (_position is { } position && SubtitleCues.At(cues, position) is var cue && !ReferenceEquals(cue, _cue))
        {
            Show(cue);
        }
    }

    /// <summary>
    /// The followed video's playback position, given every tick: shows the cue covering it, or none. A seek or a pause
    /// shows the cue for the new position on the call that gives it.
    /// </summary>
    public void At(TimeSpan position)
    {
        if (_cues is not { } cues)
        {
            return;
        }

        _position = position;

        // The video playing on after the voice stopped means it was paused for D47 and D47 has finished.
        var yielded = _quiet
            && _lines.Count > 0
            && _quietAt is { } quietAt
            && position != quietAt;

        if (yielded)
        {
            Forget();
        }

        var cue = SubtitleCues.At(cues, position);

        if (!yielded && ReferenceEquals(cue, _cue))
        {
            return;
        }

        Show(cue);
    }

    private void Show(SubtitleCue? cue)
    {
        _cue = cue;
        _cueLines = cue is null ? [] : [.. Caption.Wrap(cue.Text).Take(Caption.WindowLines)];
        Changed?.Invoke();
    }

    /// <summary>Clears D47's window and what was pacing it.</summary>
    private void Forget()
    {
        _lines.Clear();
        _pending.Clear();
        _advanceAt = null;
        _quiet = false;
        _quietAt = null;
        _clearAt = null;
        _saying = null;
        _sayStartedAt = null;
    }

    /// <summary>Takes a speed sample from the sentence that just ended on its own.</summary>
    private void Sample(DateTimeOffset now)
    {
        if (_sayStartedAt is not { } started)
        {
            return;
        }

        _sayStartedAt = null;

        var elapsed = now - started;

        if (elapsed < ShortestSampled)
        {
            return;
        }

        var sample = _sayCharacters / elapsed.TotalSeconds;

        SpeakingSpeed = Math.Clamp(
            (SpeakingSpeed * (1 - SpeedWeight)) + (sample * SpeedWeight),
            SlowestSpeakingSpeed,
            FastestSpeakingSpeed);
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
