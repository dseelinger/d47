using Microsoft.Extensions.Logging;

namespace D47.Core.Listening;

/// <summary>How the gate decides when the Commander is talking to d47.</summary>
public enum ListenMode
{
    /// <summary>Hold the key. Released, the utterance ends.</summary>
    PushToTalk,

    /// <summary>Press to start, press again to stop. Same gate, different policy.</summary>
    Toggle,
}

/// <summary>One captured stretch of speech, ready to transcribe.</summary>
/// <param name="Samples">Mono float PCM.</param>
/// <param name="SampleRate">Hz.</param>
public sealed record Utterance(float[] Samples, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}

/// <summary>Why an utterance ended, which decides whether it is worth transcribing.</summary>
public enum UtteranceEnd
{
    /// <summary>The Commander let go, or pressed again in toggle mode.</summary>
    Released,

    /// <summary>Too short to be speech. Discarded.</summary>
    TooShort,

    /// <summary>Ran past the ceiling — a stuck key, or a Commander who forgot. Kept.</summary>
    TooLong,
}

/// <summary>
/// Push-to-talk as <b>one gate policy over a continuous audio stream</b> (list.md Phase 6).
/// <para>
/// That phrasing in the checklist is the architecture, not a description. The microphone runs
/// continuously into a ring buffer; the gate decides which part of that stream is speech
/// addressed to d47. Continuous listening and a wake word are then later <em>policies</em> over
/// the same buffer rather than a rewrite of how audio is captured.
/// </para>
/// <para>
/// <b>The pre-roll is what makes a polled key viable.</b> The key is sampled from the tick loop
/// at 10 Hz, so a key-down is seen up to 100 ms after it happened — which without a pre-roll
/// clips the front of every utterance, and the first word is where the proper nouns are. The
/// gate opens <em>retroactively</em> into the buffer, so audio from before the key was noticed
/// is still captured. This is also why the key can be polled at all instead of hooked; see
/// architecture.md D4 for why a low-level keyboard hook is not on the table.
/// </para>
/// <para>
/// Nothing here touches a microphone. It takes samples and edges, which is what lets the gate's
/// transitions be tested with a WAV file and no hardware (architecture.md §8).
/// </para>
/// </summary>
public sealed class ListenGate(int sampleRate, ILogger<ListenGate> logger)
{
    /// <summary>
    /// How much audio from before the key-down is included. 500 ms comfortably covers the
    /// 100 ms worst-case polling delay plus the moment between a Commander pressing the key and
    /// starting to speak — which is often negative, since people start talking as they press.
    /// </summary>
    public TimeSpan PreRoll { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Below this an utterance is a mis-press rather than speech. Transcribing 80 ms of room
    /// tone produces a confident wrong word, which is worse than producing nothing.
    /// </summary>
    public TimeSpan MinimumLength { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// A ceiling, for the stuck key and the Commander who walked away in toggle mode. The
    /// utterance is emitted rather than discarded — they said something, and it is better
    /// transcribed late than lost.
    /// </summary>
    public TimeSpan MaximumLength { get; set; } = TimeSpan.FromSeconds(60);

    public ListenMode Mode { get; set; } = ListenMode.PushToTalk;

    public int SampleRate { get; } = sampleRate;

    /// <summary>Whether the gate is currently open. Drives the listening cue and the panel.</summary>
    public bool IsListening { get; private set; }

    /// <summary>Raised when an utterance is complete. Never raised for a discarded one.</summary>
    public event Action<Utterance>? Captured;

    /// <summary>Raised on every close, discarded or not, so a surface can explain the silence.</summary>
    public event Action<UtteranceEnd>? Ended;

    public event Action? Started;

    private readonly Lock _gate = new();
    private readonly List<float> _open = [];

    /// <summary>
    /// The pre-roll ring. Sized on first write from <see cref="PreRoll"/>, so changing the
    /// pre-roll takes effect on the next capture session rather than corrupting this one.
    /// </summary>
    private float[] _ring = [];
    private int _ringWritten;
    private int _ringNext;

    /// <summary>
    /// Feeds captured audio. Called from the audio thread, continuously, whether or not the
    /// gate is open — that is the point of the design, and it is why this method does no
    /// allocation in the common case.
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            if (IsListening)
            {
                // Bounded so a stuck key cannot grow this without limit; the ceiling is
                // enforced on the next Poll, which is where the clock lives.
                _open.AddRange(samples);
                return;
            }

            EnsureRing();

            if (_ring.Length == 0)
            {
                return;
            }

            foreach (var sample in samples)
            {
                _ring[_ringNext] = sample;
                _ringNext = (_ringNext + 1) % _ring.Length;
                _ringWritten = Math.Min(_ringWritten + 1, _ring.Length);
            }
        }
    }

    /// <summary>
    /// The key went down. In push-to-talk this opens the gate; in toggle it flips it.
    /// <para>
    /// Awaits nothing, allocates almost nothing, and is safe to call from the tick loop — the
    /// checklist's "the key-down path awaits nothing before recording starts" is this method
    /// having nothing in it to await.
    /// </para>
    /// </summary>
    public void KeyDown(DateTimeOffset now)
    {
        if (Mode == ListenMode.Toggle && IsListening)
        {
            Close(UtteranceEnd.Released);
            return;
        }

        Open(now);
    }

    /// <summary>The key came up. Only push-to-talk cares.</summary>
    public void KeyUp()
    {
        if (Mode == ListenMode.PushToTalk && IsListening)
        {
            Close(UtteranceEnd.Released);
        }
    }

    /// <summary>
    /// Called from the tick loop with the current time, to enforce the length ceiling. The gate
    /// reads no clock of its own, like everything else in Core.
    /// </summary>
    public void Poll(DateTimeOffset now)
    {
        bool overrun;

        lock (_gate)
        {
            overrun = IsListening &&
                      _openedAt is { } opened &&
                      now - opened > MaximumLength;
        }

        if (overrun)
        {
            logger.LogWarning(
                "Listening ran past {Seconds} seconds; closing. A stuck push-to-talk key would look like this",
                MaximumLength.TotalSeconds);

            Close(UtteranceEnd.TooLong);
        }
    }

    /// <summary>
    /// Abandons anything open without emitting it. For the Commander cancelling, and for a
    /// device disappearing mid-utterance.
    /// </summary>
    public void Abandon()
    {
        lock (_gate)
        {
            if (!IsListening)
            {
                return;
            }

            IsListening = false;
            _open.Clear();
            _openedAt = null;
        }

        Ended?.Invoke(UtteranceEnd.TooShort);
    }

    private DateTimeOffset? _openedAt;

    private void Open(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (IsListening)
            {
                return;
            }

            _open.Clear();

            // The retroactive part. Everything already in the ring is speech that happened
            // before the key was noticed, and it belongs to this utterance.
            _open.AddRange(RingContents());
            _ringWritten = 0;
            _ringNext = 0;

            IsListening = true;
            _openedAt = now;
        }

        // Outside the lock: a subscriber playing the listening cue must not run under the same
        // lock the audio thread takes on every buffer.
        Started?.Invoke();
    }

    private void Close(UtteranceEnd reason)
    {
        Utterance? utterance = null;

        lock (_gate)
        {
            if (!IsListening)
            {
                return;
            }

            IsListening = false;
            _openedAt = null;

            var samples = _open.ToArray();
            _open.Clear();

            var duration = TimeSpan.FromSeconds((double)samples.Length / SampleRate);

            if (reason == UtteranceEnd.Released && duration < MinimumLength)
            {
                // A mis-press. Transcribing 80 ms of room tone produces a confident wrong word.
                logger.LogDebug("Discarding a {Ms} ms press as too short to be speech", duration.TotalMilliseconds);
                reason = UtteranceEnd.TooShort;
            }
            else if (samples.Length > 0)
            {
                utterance = new Utterance(samples, SampleRate);
            }
        }

        if (utterance is not null)
        {
            Captured?.Invoke(utterance);
        }

        Ended?.Invoke(reason);
    }

    private void EnsureRing()
    {
        var wanted = (int)(PreRoll.TotalSeconds * SampleRate);

        if (_ring.Length == wanted)
        {
            return;
        }

        _ring = new float[Math.Max(0, wanted)];
        _ringWritten = 0;
        _ringNext = 0;
    }

    /// <summary>The ring in order, oldest first.</summary>
    private IEnumerable<float> RingContents()
    {
        if (_ringWritten == 0 || _ring.Length == 0)
        {
            yield break;
        }

        var start = _ringWritten < _ring.Length ? 0 : _ringNext;

        for (var offset = 0; offset < _ringWritten; offset++)
        {
            yield return _ring[(start + offset) % _ring.Length];
        }
    }
}
