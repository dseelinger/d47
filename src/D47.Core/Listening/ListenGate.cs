using Microsoft.Extensions.Logging;

namespace D47.Core.Listening;

/// <summary>How the gate decides when the Commander is talking to d47.</summary>
public enum ListenMode
{
    /// <summary>Hold the key.</summary>
    PushToTalk,

    /// <summary>Press to start, press again to stop.</summary>
    Toggle,

    /// <summary>Hands free.</summary>
    VoiceActivity,

    /// <summary>Hands free, and only when spoken to by name.</summary>
    WakeWord,
}

/// <summary>
/// What the microphone is doing, for a Commander looking at the panel (Phase 13, "Show that the
/// microphone is open").
/// </summary>
public enum MicrophoneState
{
    /// <summary>Nothing is open.</summary>
    Off,

    /// <summary>The device is open and nothing is being kept.</summary>
    Idle,

    /// <summary>The device is open and d47 is deciding for itself when to listen.</summary>
    Armed,

    /// <summary>The gate is open.</summary>
    Open,
}

/// <summary>One captured stretch of speech, ready to transcribe.</summary>
/// <param name="Samples">Mono float PCM.</param>
/// <param name="SampleRate">Hz.</param>
public sealed record Utterance(float[] Samples, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

    /// <summary>The loudest sample in it.</summary>
    public float Peak
    {
        get
        {
            var peak = 0f;

            foreach (var sample in Samples)
            {
                peak = MathF.Max(peak, MathF.Abs(sample));
            }

            return peak;
        }
    }

    /// <summary>
    /// Whether the device delivered no signal whatsoever, as distinct from a Commander who was quiet.
    /// </summary>
    public bool IsSilent => Peak < 0.0001f;
}

/// <summary>Why an utterance ended, which decides whether it is worth transcribing.</summary>
public enum UtteranceEnd
{
    /// <summary>The Commander let go, or pressed again in toggle mode.</summary>
    Released,

    /// <summary>Too short to be speech.</summary>
    TooShort,

    /// <summary>Ran past the ceiling — a stuck key, or a Commander who forgot.</summary>
    TooLong,
}

/// <summary>Push-to-talk as one gate policy over a continuous audio stream (Phase 6).</summary>
public sealed class ListenGate(int sampleRate, ILogger<ListenGate> logger) : ICaptureSink
{
    /// <summary>
    /// How much audio from before the key-down is included. 500 ms comfortably covers the 100 ms
    /// worst-case polling delay plus the moment between a Commander pressing the key and starting to
    /// speak — which is often negative, since people start talking as they press.
    /// </summary>
    public TimeSpan PreRoll { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Below this an utterance is a mis-press rather than speech.</summary>
    public TimeSpan MinimumLength { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>A ceiling, for the stuck key and the Commander who walked away in toggle mode.</summary>
    public TimeSpan MaximumLength { get; set; } = TimeSpan.FromSeconds(60);

    public ListenMode Mode { get; set; } = ListenMode.PushToTalk;

    public int SampleRate { get; } = sampleRate;

    /// <summary>The detector the continuous modes run on.</summary>
    public VoiceActivityDetector Voice { get; } = new(sampleRate);

    /// <summary>Whether the gate is currently open.</summary>
    public bool IsListening { get; private set; }

    /// <summary>Whether the mode is one where d47 decides for itself when to listen.</summary>
    public bool IsContinuous => Mode is ListenMode.VoiceActivity or ListenMode.WakeWord;

    /// <summary>Whether audio is actually arriving.</summary>
    public bool Capturing
    {
        get => _capturing;

        set
        {
            if (_capturing == value)
            {
                return;
            }

            _capturing = value;
            RaiseState();
        }
    }

    /// <summary>
    /// Whether the speech model is still being loaded, so what is captured now is held rather than
    /// transcribed (#147).
    /// </summary>
    public bool ModelLoading
    {
        get => _modelLoading;

        set
        {
            if (_modelLoading == value)
            {
                return;
            }

            _modelLoading = value;
            ModelLoadingChanged?.Invoke(value);
        }
    }

    /// <summary>Raised when <see cref="ModelLoading"/> changed.</summary>
    public event Action<bool>? ModelLoadingChanged;

    /// <summary>Whether d47 itself is currently audible.</summary>
    public bool FarEndActive
    {
        get => _farEnd;

        set
        {
            if (_farEnd == value)
            {
                return;
            }

            _farEnd = value;
        }
    }

    /// <summary>Whether something upstream is removing d47's own output from what arrives here.</summary>
    public bool EchoCancelled { get; set; }

    /// <summary>What a Commander looking at the panel should be told the microphone is doing.</summary>
    public MicrophoneState State
    {
        get
        {
            if (IsListening)
            {
                return MicrophoneState.Open;
            }

            if (!_capturing)
            {
                return MicrophoneState.Off;
            }

            return IsContinuous ? MicrophoneState.Armed : MicrophoneState.Idle;
        }
    }

    /// <summary>Raised when <see cref="State"/> changed.</summary>
    public event Action<MicrophoneState>? StateChanged;

    /// <summary>Raised when an utterance is complete.</summary>
    public event Action<Utterance>? Captured;

    /// <summary>Raised on every close, discarded or not, so a surface can explain the silence.</summary>
    public event Action<UtteranceEnd>? Ended;

    public event Action? Started;

    private readonly Lock _gate = new();
    private readonly List<float> _open = [];

    private bool _capturing;
    private bool _farEnd;
    private bool _modelLoading;

    /// <summary>
    /// What the detector decided on the audio thread and the tick thread has not carried out yet.
    /// </summary>
    private volatile bool _pendingOpen;

    private volatile bool _pendingClose;

    /// <summary>Whether the open gate was opened by the detector rather than by the key.</summary>
    private bool _openedByVoice;

    /// <summary>The pre-roll ring.</summary>
    private float[] _ring = [];
    private int _ringWritten;
    private int _ringNext;

    /// <summary>Feeds captured audio.</summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (IsContinuous && Suppressed() && !IsListening)
        {
            // d47 is talking and nothing is subtracting it, so most of what is arriving is d47.
            Voice.Settle();
        }
        else if (IsContinuous)
        {
            switch (Voice.Write(samples))
            {
                case VoiceActivity.Started:
                    _pendingOpen = true;
                    break;

                case VoiceActivity.Ended:
                    _pendingClose = true;
                    break;
            }
        }

        Append(samples);
    }

    /// <summary>Whether a detector onset should be ignored.</summary>
    private bool Suppressed() => _farEnd && !EchoCancelled;

    private void Append(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            if (IsListening)
            {
                // Bounded so a stuck key cannot grow this without limit; the ceiling is enforced on the next
                // Poll, which is where the clock lives.
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

    /// <summary>The key went down.</summary>
    public void KeyDown(DateTimeOffset now)
    {
        if (Mode == ListenMode.Toggle && IsListening)
        {
            Close(UtteranceEnd.Released);
            return;
        }

        if (IsListening && _openedByVoice)
        {
            // The detector got there first and the Commander has now pressed the key.
            lock (_gate)
            {
                _openedByVoice = false;
                _openedAt = now;
            }

            return;
        }

        Open(byVoice: false, now);
    }

    /// <summary>The key came up.</summary>
    public void KeyUp()
    {
        if (Mode != ListenMode.Toggle && IsListening && !_openedByVoice)
        {
            Close(UtteranceEnd.Released);
        }
    }

    /// <summary>Called from the tick loop with the current time, to enforce the length ceiling.</summary>
    public void Poll(DateTimeOffset now)
    {
        bool overrun;

        // The detector's verdicts, carried out here rather than where they were reached.
        var opening = _pendingOpen;
        var closing = _pendingClose;
        _pendingOpen = false;
        _pendingClose = false;

        if (opening && !IsListening && !Suppressed())
        {
            Open(byVoice: true, now);
        }

        if (_openedByVoice && (closing || Suppressed()))
        {
            // Either the room went quiet for a whole hangover, or d47 started talking with nothing
            // subtracting it — in which case what has been captured so far is what the Commander said and
            // everything after it would be d47.
            if (Suppressed() && !closing)
            {
                logger.LogDebug(
                    "Closing a hands-free utterance because D47 started speaking and nothing is cancelling it");
            }

            Close(UtteranceEnd.Released);
        }

        lock (_gate)
        {
            if (IsListening && _openedAt is null)
            {
                _openedAt = now;
            }

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

    /// <summary>Abandons anything open without emitting it.</summary>
    public void Abandon()
    {
        var was = State;

        _pendingOpen = false;
        _pendingClose = false;

        lock (_gate)
        {
            if (!IsListening)
            {
                return;
            }

            IsListening = false;
            _openedByVoice = false;
            _open.Clear();
            _openedAt = null;
        }

        Voice.Settle();
        Ended?.Invoke(UtteranceEnd.TooShort);
        RaiseState(was);
    }

    /// <summary>The device went away or changed.</summary>
    public void Reset()
    {
        Abandon();
        Voice.Reset();
    }

    private DateTimeOffset? _openedAt;

    private void Open(bool byVoice, DateTimeOffset? now = null)
    {
        var was = State;

        lock (_gate)
        {
            if (IsListening)
            {
                return;
            }

            _open.Clear();

            // The retroactive part.
            if (!FarEndActive)
            {
                _open.AddRange(RingContents());
            }

            _ringWritten = 0;
            _ringNext = 0;

            IsListening = true;
            _openedByVoice = byVoice;
            _openedAt = now;
        }

        // Outside the lock: a subscriber playing the listening cue must not run under the same lock the audio
        // thread takes on every buffer.
        Started?.Invoke();
        RaiseState(was);
    }

    private void Close(UtteranceEnd reason)
    {
        Utterance? utterance = null;
        var was = State;

        // Whatever the detector was about to say belongs to the utterance being closed here.
        _pendingOpen = false;
        _pendingClose = false;

        lock (_gate)
        {
            if (!IsListening)
            {
                return;
            }

            IsListening = false;
            _openedByVoice = false;
            _openedAt = null;

            var samples = _open.ToArray();
            _open.Clear();

            var duration = TimeSpan.FromSeconds((double)samples.Length / SampleRate);

            if (reason == UtteranceEnd.Released && duration < MinimumLength)
            {
                // A mis-press.
                logger.LogDebug("Discarding a {Ms} ms press as too short to be speech", duration.TotalMilliseconds);
                reason = UtteranceEnd.TooShort;
            }
            else if (samples.Length > 0)
            {
                utterance = new Utterance(samples, SampleRate);
            }
        }

        // The detector is told the utterance is over however it ended, or a gate closed by the key
        // mid-sentence leaves it believing speech is still in progress — and the next real onset then never
        // arrives, because it never stopped.
        Voice.Settle();

        if (utterance is not null)
        {
            Captured?.Invoke(utterance);
        }

        Ended?.Invoke(reason);
        RaiseState(was);
    }

    private void RaiseState() => RaiseState(null);

    private void RaiseState(MicrophoneState? was)
    {
        var now = State;

        if (was == now)
        {
            return;
        }

        StateChanged?.Invoke(now);
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
