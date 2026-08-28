using Microsoft.Extensions.Logging;

namespace D47.Core.Listening;

/// <summary>How the gate decides when the Commander is talking to d47.</summary>
public enum ListenMode
{
    /// <summary>Hold the key. Released, the utterance ends.</summary>
    PushToTalk,

    /// <summary>Press to start, press again to stop. Same gate, different policy.</summary>
    Toggle,

    /// <summary>
    /// Hands free. The gate opens when somebody starts talking and closes when they stop
    /// (Phase 13). The key still works and still wins — a policy that decides for
    /// itself is not a reason to take away the one that does not.
    /// </summary>
    VoiceActivity,

    /// <summary>
    /// Hands free, and only when spoken to by name. The audio path is identical to
    /// <see cref="VoiceActivity"/> — the difference is entirely in what happens to the words
    /// afterwards, which is <see cref="WakeWordGate"/>'s business rather than the gate's.
    /// </summary>
    WakeWord,
}

/// <summary>
/// What the microphone is doing, for a Commander looking at the panel (Phase 13,
/// "Show that the microphone is open").
/// <para>
/// A property of the gate policy rather than of any one capability, which is what the checklist
/// asks for and is also the only place that can answer it: the device knows whether it is
/// capturing and the gate knows whether that capture is being kept, and it is the second
/// question a Commander is actually asking.
/// </para>
/// </summary>
public enum MicrophoneState
{
    /// <summary>Nothing is open. No key bound, no continuous mode, or no working device.</summary>
    Off,

    /// <summary>
    /// The device is open and nothing is being kept. Push-to-talk at rest: audio runs into a
    /// half-second ring and is overwritten, and no part of it is transcribed or stored.
    /// </summary>
    Idle,

    /// <summary>
    /// The device is open and d47 is deciding for itself when to listen. This is the state the
    /// checklist exists for: continuous capture with nothing on screen saying so is the thing a
    /// Commander is right to distrust.
    /// </summary>
    Armed,

    /// <summary>The gate is open. What is arriving now will be transcribed.</summary>
    Open,
}

/// <summary>One captured stretch of speech, ready to transcribe.</summary>
/// <param name="Samples">Mono float PCM.</param>
/// <param name="SampleRate">Hz.</param>
public sealed record Utterance(float[] Samples, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

    /// <summary>The loudest sample in it. Zero means nothing arrived at all.</summary>
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
    /// Whether the device delivered no signal whatsoever, as distinct from a Commander who was
    /// quiet. This is a different fault with a different fix, and telling them apart is the
    /// whole point: a virtual or muted input device is selected far more often than anyone
    /// expects — Windows will happily default to one — and it produces a stream of digital
    /// zeroes that transcribes to nothing, which otherwise reports as "nothing intelligible"
    /// and reads as though the Commander mumbled.
    /// <para>
    /// The threshold is far below anything a live microphone produces over a whole utterance:
    /// even a silent room in front of an open mic carries self-noise orders of magnitude above
    /// this. Reaching it means the samples are zeroes or very near it for the entire capture.
    /// </para>
    /// </summary>
    public bool IsSilent => Peak < 0.0001f;
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
/// Push-to-talk as <b>one gate policy over a continuous audio stream</b> (Phase 6).
/// <para>
/// That phrasing in the checklist is the architecture, not a description. The microphone runs
/// continuously into a ring buffer; the gate decides which part of that stream is speech
/// addressed to d47. Phase 13's continuous listening and wake word are then <em>policies</em>
/// over the same buffer rather than a rewrite of how audio is captured — which is what they
/// turned out to be: <see cref="ListenMode.VoiceActivity"/> adds a detector on the same
/// <see cref="Write"/> path and opens the same gate, and the wake word does not touch this
/// class at all.
/// </para>
/// <para>
/// <b>The pre-roll is what makes a polled key viable.</b> The key is sampled from the tick loop
/// at 10 Hz, so a key-down is seen up to 100 ms after it happened — which without a pre-roll
/// clips the front of every utterance, and the first word is where the proper nouns are. The
/// gate opens <em>retroactively</em> into the buffer, so audio from before the key was noticed
/// is still captured. This is also why the key can be polled at all instead of hooked; see
/// architecture.md D4 for why a low-level keyboard hook is not on the table. The pre-roll earns
/// its keep twice over in the continuous modes, where what it covers is the detector's onset
/// delay — a sentence is 60 ms old before the detector will call it speech.
/// </para>
/// <para>
/// Nothing here touches a microphone. It takes samples and edges, which is what lets the gate's
/// transitions be tested with a WAV file and no hardware (architecture.md §8).
/// </para>
/// </summary>
public sealed class ListenGate(int sampleRate, ILogger<ListenGate> logger) : ICaptureSink
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

    /// <summary>
    /// The detector the continuous modes run on. Exposed rather than hidden because its
    /// sensitivity and its hangover are settings rows, and the alternative is this class
    /// re-declaring both of them to pass them along.
    /// </summary>
    public VoiceActivityDetector Voice { get; } = new(sampleRate);

    /// <summary>Whether the gate is currently open. Drives the listening cue and the panel.</summary>
    public bool IsListening { get; private set; }

    /// <summary>
    /// Whether the mode is one where d47 decides for itself when to listen. The single place
    /// that answers it, so a fifth mode does not mean finding every comparison against two.
    /// </summary>
    public bool IsContinuous => Mode is ListenMode.VoiceActivity or ListenMode.WakeWord;

    /// <summary>
    /// Whether audio is actually arriving. Set by whoever owns the device, because the gate
    /// cannot tell a silent room from a closed microphone and must not guess.
    /// </summary>
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
    /// Whether d47 itself is currently audible. Set from the arbiter's activity.
    /// <para>
    /// It matters only when there is no echo cancellation. With the canceller running, d47's own
    /// voice is subtracted from capture before this class ever sees it, and the Commander can
    /// talk over it — which is the entire point of Phase 13's first item. Without one, a
    /// continuous mode with speakers is a feedback loop: d47 hears itself, transcribes itself,
    /// and answers itself. So uncancelled, the gate goes half-duplex while d47 speaks, and the
    /// settings row says as much rather than leaving it to be discovered.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Whether something upstream is removing d47's own output from what arrives here. Set by
    /// the host from the canceller's live state, not from the settings row — a canceller that
    /// was asked for and failed to start must not be believed in.
    /// </summary>
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

    /// <summary>Raised when <see cref="State"/> changed. The panel and the overlay follow it.</summary>
    public event Action<MicrophoneState>? StateChanged;

    /// <summary>Raised when an utterance is complete. Never raised for a discarded one.</summary>
    public event Action<Utterance>? Captured;

    /// <summary>Raised on every close, discarded or not, so a surface can explain the silence.</summary>
    public event Action<UtteranceEnd>? Ended;

    public event Action? Started;

    private readonly Lock _gate = new();
    private readonly List<float> _open = [];

    private bool _capturing;
    private bool _farEnd;

    /// <summary>
    /// What the detector decided on the audio thread and the tick thread has not carried out
    /// yet. Volatile rather than locked: they are set by one thread and cleared by another, they
    /// are single writes, and taking the capture lock to raise a flag would put the tick thread
    /// in the way of the device.
    /// </summary>
    private volatile bool _pendingOpen;

    private volatile bool _pendingClose;

    /// <summary>
    /// Whether the open gate was opened by the detector rather than by the key. The key wins:
    /// a Commander holding push-to-talk through a pause must not have the utterance ended
    /// underneath them by a detector that heard the pause.
    /// </summary>
    private bool _openedByVoice;

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
    /// <para>
    /// In a continuous mode the detector runs here — it has to, since this is where the audio is
    /// — but it does not act. What it decides is recorded and carried out by the next
    /// <see cref="Poll"/>, on the tick thread, for the reason the architecture states plainly:
    /// this is a real-time callback and it never blocks on anything (architecture.md §4).
    /// Opening the gate plays a cue and closing it hands an utterance to a transcriber, and
    /// neither belongs on the thread the device is waiting on. The cost is up to one tick of
    /// latency at each end, which the pre-roll already covers at the front and the detector's
    /// 700 ms hangover dwarfs at the back.
    /// </para>
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (IsContinuous && Suppressed() && !IsListening)
        {
            // d47 is talking and nothing is subtracting it, so most of what is arriving is d47.
            // Held out of the detector entirely rather than written and ignored: written, it
            // would learn d47's own voice as the room, and it would come out of the far end's
            // speech already believing somebody was mid-sentence — so the Commander's first real
            // word afterwards would not read as an onset at all.
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

    /// <summary>
    /// Whether a detector onset should be ignored. Only one reason so far, and it is the one
    /// worth being explicit about: d47 talking with nothing subtracting it.
    /// </summary>
    private bool Suppressed() => _farEnd && !EchoCancelled;

    private void Append(ReadOnlySpan<float> samples)
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
    /// <para>
    /// The key is live in every mode, including the hands-free ones. A Commander who wants to be
    /// certain d47 is listening should not have to trust a detector to agree.
    /// </para>
    /// </summary>
    public void KeyDown(DateTimeOffset now)
    {
        if (Mode == ListenMode.Toggle && IsListening)
        {
            Close(UtteranceEnd.Released);
            return;
        }

        if (IsListening && _openedByVoice)
        {
            // The detector got there first and the Commander has now pressed the key. Taking
            // ownership rather than opening again keeps the audio already captured and puts the
            // key in charge of when it ends.
            lock (_gate)
            {
                _openedByVoice = false;
                _openedAt = now;
            }

            return;
        }

        Open(byVoice: false, now);
    }

    /// <summary>The key came up. Only push-to-talk cares.</summary>
    public void KeyUp()
    {
        if (Mode != ListenMode.Toggle && IsListening && !_openedByVoice)
        {
            Close(UtteranceEnd.Released);
        }
    }

    /// <summary>
    /// Called from the tick loop with the current time, to enforce the length ceiling. The gate
    /// reads no clock of its own, like everything else in Core.
    /// <para>
    /// It is also where a gate opened by the detector gets its start time. The detector runs on
    /// the audio thread, which has no clock to hand it, so the ceiling on a hands-free utterance
    /// is measured from the first tick after it opened — up to a tenth of a second late, against
    /// a ceiling measured in minutes.
    /// </para>
    /// </summary>
    public void Poll(DateTimeOffset now)
    {
        bool overrun;

        // The detector's verdicts, carried out here rather than where they were reached. Read
        // and cleared before anything else, so a transition decided during this poll is not lost
        // and not applied twice.
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
            // Either the room went quiet for a whole hangover, or d47 started talking with
            // nothing subtracting it — in which case what has been captured so far is what the
            // Commander said and everything after it would be d47.
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

    /// <summary>
    /// Abandons anything open without emitting it. For the Commander cancelling, and for a
    /// device disappearing mid-utterance.
    /// </summary>
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

    /// <summary>
    /// The device went away or changed. Drops what was open and forgets the room the detector
    /// had learned, which described a microphone that is no longer the one being heard.
    /// </summary>
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

            // The retroactive part. Everything already in the ring is speech that happened
            // before the key was noticed — or before the detector was willing to call it speech
            // — and it belongs to this utterance.
            _open.AddRange(RingContents());
            _ringWritten = 0;
            _ringNext = 0;

            IsListening = true;
            _openedByVoice = byVoice;
            _openedAt = now;
        }

        // Outside the lock: a subscriber playing the listening cue must not run under the same
        // lock the audio thread takes on every buffer.
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
                // A mis-press. Transcribing 80 ms of room tone produces a confident wrong word.
                logger.LogDebug("Discarding a {Ms} ms press as too short to be speech", duration.TotalMilliseconds);
                reason = UtteranceEnd.TooShort;
            }
            else if (samples.Length > 0)
            {
                utterance = new Utterance(samples, SampleRate);
            }
        }

        // The detector is told the utterance is over however it ended, or a gate closed by the
        // key mid-sentence leaves it believing speech is still in progress — and the next real
        // onset then never arrives, because it never stopped.
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
