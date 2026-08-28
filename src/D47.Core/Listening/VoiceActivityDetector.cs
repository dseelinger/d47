namespace D47.Core.Listening;

/// <summary>What the detector decided about the frames just written.</summary>
public enum VoiceActivity
{
    /// <summary>Nothing changed. Overwhelmingly the common answer.</summary>
    Unchanged,

    /// <summary>Speech began. The gate opens on this.</summary>
    Started,

    /// <summary>Speech ended and the hangover has run out. The gate closes on this.</summary>
    Ended,
}

/// <summary>
/// Whether somebody is talking (Phase 13, "Voice Activity Detection").
/// <para>
/// It is deliberately not a model. The checklist asks for a <em>gate policy over the existing
/// stream</em>, and what a gate policy has to be is cheap enough to run on every buffer on the
/// audio thread and predictable enough that a Commander who finds it too eager can turn one
/// number and have it behave. An energy detector over an adaptive noise floor is both; a neural
/// VAD is neither, and would be a second model to download beside Whisper for a decision
/// Whisper itself re-checks a moment later.
/// </para>
/// <para>
/// <b>The noise floor is the whole design.</b> An absolute threshold is what makes every energy
/// VAD ever shipped either deaf on a quiet microphone or permanently open on a loud one, because
/// "loud enough to be speech" is a property of the gain the Commander's interface happens to be
/// set to, and that varies by tens of decibels across the headsets this will meet. So the floor
/// follows the room: it falls quickly toward quiet and rises slowly toward loud, which means a
/// fan or a cooling pump is learned within a second or two while a sentence — which is loud and
/// short by comparison — never drags it up far enough to swallow itself.
/// </para>
/// <para>
/// There is still a hard floor underneath it, because a follower alone has a failure mode with
/// no bottom: in a silent room on a muted device the floor converges toward digital zero, and a
/// single bit of dither then sits far above it and reads as a shout.
/// </para>
/// <para>
/// Reads no clock and owns no thread. Durations are counted in samples, which is the only clock
/// a stream of audio actually has.
/// </para>
/// </summary>
public sealed class VoiceActivityDetector
{
    /// <summary>
    /// What an all-zero frame reports as. Finite rather than negative infinity, so the follower
    /// has something to converge to and the arithmetic never meets a NaN.
    /// </summary>
    private const double SilenceDb = -100;

    /// <summary>
    /// How much of the room has to have been heard before anything counts as speech. Opening
    /// the gate on the first frame after a device opens means opening it on that device's own
    /// startup transient, every single time.
    /// </summary>
    private const int SettleFrames = 25;

    private readonly int _sampleRate;

    /// <summary>
    /// 20 ms. Long enough for the energy of a voiced frame to be a stable number, short enough
    /// that the onset delay sits inside what the pre-roll already covers.
    /// </summary>
    private readonly int _frame;

    private readonly float[] _pending;

    private int _held;
    private int _framesSeen;
    private double _floorDb = SilenceDb;
    private int _speechFrames;
    private int _silenceFrames;
    private bool _speaking;

    public VoiceActivityDetector(int sampleRate)
    {
        _sampleRate = sampleRate;
        _frame = Math.Max(1, sampleRate / 50);
        _pending = new float[_frame];
    }

    /// <summary>
    /// How far above the room a frame has to be before it counts as speech, in decibels.
    /// <para>
    /// The one number a Commander turns. Lower hears more and opens on a chair creak; higher
    /// needs the sentence well started before it notices. 9 dB is roughly "clearly louder than
    /// the room" and holds up across a headset boom, a desk condenser and a laptop array.
    /// </para>
    /// </summary>
    public double Sensitivity { get; set; } = 9;

    /// <summary>
    /// The absolute floor, under which nothing is speech however far above the room it sits.
    /// This is what stops a muted or digitally silent device producing a stream of utterances
    /// out of its own dither.
    /// </summary>
    public double Minimum { get; set; } = -55;

    /// <summary>
    /// How long the quiet after a sentence has to last before the utterance is over.
    /// <para>
    /// Generous on purpose. A Commander pauses mid-sentence to look at something, and an
    /// utterance cut at the first gap is transcribed as half a question — which then reaches the
    /// model as half a question. The cost of being wrong the other way is a little dead air on
    /// the end of the clip, which Whisper ignores.
    /// </para>
    /// </summary>
    public TimeSpan Hangover { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// How much has to be loud before the gate opens. Three frames — 60 ms — is past a click, a
    /// keyboard and a stick detent, and short enough that the pre-roll covers what it cost.
    /// </summary>
    public int Onset { get; set; } = 3;

    /// <summary>Whether the detector currently believes somebody is talking.</summary>
    public bool Speaking => _speaking;

    /// <summary>The most recent frame's level, in dBFS. What a level meter would show.</summary>
    public double Level { get; private set; } = SilenceDb;

    /// <summary>What the room is currently measured at, in dBFS.</summary>
    public double NoiseFloor => _floorDb;

    /// <summary>Whether enough of the room has been heard for the floor to mean anything.</summary>
    public bool Settled => _framesSeen >= SettleFrames;

    /// <summary>
    /// Feeds audio and says whether the answer changed. Frames are accumulated across calls, so
    /// the caller may write whatever the device hands it.
    /// <para>
    /// One transition per call at most. A buffer containing both the start and the end of an
    /// utterance would have to be longer than the hangover, which is longer than any capture
    /// buffer WASAPI produces — and were one ever to be, reporting the start and letting the end
    /// arrive on the next call is the safe way round.
    /// </para>
    /// </summary>
    public VoiceActivity Write(ReadOnlySpan<float> samples)
    {
        var verdict = VoiceActivity.Unchanged;
        var at = 0;

        while (at < samples.Length)
        {
            var take = Math.Min(_frame - _held, samples.Length - at);
            samples.Slice(at, take).CopyTo(_pending.AsSpan(_held));
            _held += take;
            at += take;

            if (_held < _frame)
            {
                break;
            }

            _held = 0;

            var frame = Consider(_pending);

            if (frame != VoiceActivity.Unchanged && verdict == VoiceActivity.Unchanged)
            {
                verdict = frame;
            }
        }

        return verdict;
    }

    /// <summary>
    /// Forgets the room and whatever it was in the middle of. Called when the device changes:
    /// the floor it learned describes a microphone that is no longer the one being heard.
    /// </summary>
    public void Reset()
    {
        _held = 0;
        _framesSeen = 0;
        _floorDb = SilenceDb;
        _speechFrames = 0;
        _silenceFrames = 0;
        _speaking = false;
        Level = SilenceDb;
    }

    /// <summary>
    /// Drops the detector's own idea of an utterance without reporting an end, keeping the room
    /// it has learned. For when something above it has already decided — the Commander pressed
    /// the key, or the gate was abandoned — because a stale "still speaking" would swallow the
    /// next real onset.
    /// </summary>
    public void Settle()
    {
        _speaking = false;
        _speechFrames = 0;
        _silenceFrames = 0;
    }

    private VoiceActivity Consider(ReadOnlySpan<float> frame)
    {
        double sum = 0;

        foreach (var sample in frame)
        {
            sum += (double)sample * sample;
        }

        var rms = Math.Sqrt(sum / frame.Length);
        var db = rms <= 1e-10 ? SilenceDb : Math.Max(SilenceDb, 20 * Math.Log10(rms));

        Level = db;

        if (_framesSeen == 0)
        {
            // The first frame <em>is</em> the room, as far as anybody knows. Starting the
            // follower at silence instead and letting it climb takes half a minute at the rate
            // below — half a minute in which everything is far above the floor and every noise
            // in the room is speech. What that rate is for is drift, not arrival.
            _floorDb = db;
        }

        _framesSeen++;

        if (!Settled)
        {
            // Still measuring. Symmetric and quick, because this window is the measurement and
            // there is nothing yet for a slow rise to protect.
            _floorDb += (db - _floorDb) * 0.15;
        }
        else if (db < _floorDb)
        {
            // Quiet is believed almost at once: a room that got quieter is a room, immediately.
            _floorDb += (db - _floorDb) * 0.35;
        }
        else if (!_speaking)
        {
            // Loud is believed slowly, and not at all while somebody is talking. Both halves
            // matter: without the slow rate a passing noise becomes the new room, and without
            // the freeze a long sentence drags the floor up to meet itself and the gate shuts in
            // the middle of it.
            _floorDb += (db - _floorDb) * 0.002;
        }

        if (!Settled)
        {
            // Still learning the room. Not deaf to the audio — the floor is being built out of
            // it — only unwilling to call any of it speech yet.
            return VoiceActivity.Unchanged;
        }

        if (db > _floorDb + Sensitivity && db > Minimum)
        {
            _silenceFrames = 0;
            _speechFrames++;

            if (_speaking || _speechFrames < Onset)
            {
                return VoiceActivity.Unchanged;
            }

            _speaking = true;
            return VoiceActivity.Started;
        }

        _speechFrames = 0;

        if (!_speaking)
        {
            return VoiceActivity.Unchanged;
        }

        _silenceFrames++;

        if (_silenceFrames * (double)_frame / _sampleRate < Hangover.TotalSeconds)
        {
            return VoiceActivity.Unchanged;
        }

        _speaking = false;
        _silenceFrames = 0;
        return VoiceActivity.Ended;
    }
}
