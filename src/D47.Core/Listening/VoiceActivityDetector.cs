namespace D47.Core.Listening;

/// <summary>What the detector decided about the frames just written.</summary>
public enum VoiceActivity
{
    /// <summary>Nothing changed.</summary>
    Unchanged,

    /// <summary>Speech began.</summary>
    Started,

    /// <summary>Speech ended and the hangover has run out.</summary>
    Ended,
}

/// <summary>Whether somebody is talking (Phase 13, "Voice Activity Detection").</summary>
public sealed class VoiceActivityDetector
{
    /// <summary>What an all-zero frame reports as.</summary>
    private const double SilenceDb = -100;

    /// <summary>How much of the room has to have been heard before anything counts as speech.</summary>
    private const int SettleFrames = 25;

    private readonly int _sampleRate;

    /// <summary>20 ms.</summary>
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

    /// <summary>How far above the room a frame has to be before it counts as speech, in decibels.</summary>
    public double Sensitivity { get; set; } = 9;

    /// <summary>The absolute floor, under which nothing is speech however far above the room it sits.</summary>
    public double Minimum { get; set; } = -55;

    /// <summary>How long the quiet after a sentence has to last before the utterance is over.</summary>
    public TimeSpan Hangover { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>How much has to be loud before the gate opens.</summary>
    public int Onset { get; set; } = 3;

    /// <summary>Whether the detector currently believes somebody is talking.</summary>
    public bool Speaking => _speaking;

    /// <summary>The most recent frame's level, in dBFS.</summary>
    public double Level { get; private set; } = SilenceDb;

    /// <summary>What the room is currently measured at, in dBFS.</summary>
    public double NoiseFloor => _floorDb;

    /// <summary>Whether enough of the room has been heard for the floor to mean anything.</summary>
    public bool Settled => _framesSeen >= SettleFrames;

    /// <summary>Feeds audio and says whether the answer changed.</summary>
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

    /// <summary>Forgets the room and whatever it was in the middle of.</summary>
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
    /// Drops the detector's own idea of an utterance without reporting an end, keeping the room it has
    /// learned.
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
            // The first frame is the room, as far as anybody knows.
            _floorDb = db;
        }

        _framesSeen++;

        if (!Settled)
        {
            // Still measuring.
            _floorDb += (db - _floorDb) * 0.15;
        }
        else if (db < _floorDb)
        {
            // Quiet is believed almost at once: a room that got quieter is a room, immediately.
            _floorDb += (db - _floorDb) * 0.35;
        }
        else if (!_speaking)
        {
            // Loud is believed slowly, and not at all while somebody is talking.
            _floorDb += (db - _floorDb) * 0.002;
        }

        if (!Settled)
        {
            // Still learning the room.
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
