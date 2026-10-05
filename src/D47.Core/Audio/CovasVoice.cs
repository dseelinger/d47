namespace D47.Core.Audio;

/// <summary>
/// The stock COVAS core's voice: a short room reverb under the untouched dry voice, measured against the
/// in-game COVAS. No filtering and no pitch change.
/// </summary>
public static class CovasVoice
{
    /// <summary>The Guardian reverb's delays, with feedback from <c>10^(−3·delay / 0.8 s)</c>.</summary>
    private static readonly (double DelayMs, double Feedback)[] Combs =
    [
        (29.7, 0.774),
        (37.1, 0.726),
        (41.1, 0.701),
        (43.7, 0.686),
    ];

    private static readonly double[] AllpassMs = [5, 1.7];
    private const double AllpassGain = 0.7;
    private const double Dry = 1.0;
    private const double Wet = 0.6;
    private const double TailSeconds = 0.8;

    /// <summary>
    /// The makeup gain applied to every sample: the lowest whole-clip RMS levelling
    /// over the speech clips <c>TheCovasGainIsTheLowestLevellingOfSpokenClipsTests</c> names, rounded down.
    /// </summary>
    internal const double Gain = 0.661;

    /// <summary>The reverb as a running filter on <see cref="AudioFormat.Standard"/> PCM.</summary>
    public static IPcmFilter Filter() => new Reverb(AudioFormat.Standard);

    /// <summary>The clip through <see cref="Filter"/>: the reverb added, a faded tail appended, at <see cref="Gain"/>.</summary>
    public static AudioClip Apply(AudioClip clip)
    {
        if (clip.Pcm.Length / (2 * Math.Max(1, clip.Format.Channels)) == 0)
        {
            return clip;
        }

        var filter = new Reverb(clip.Format);
        var body = filter.Push(clip.Pcm.Span);
        var tail = filter.Finish();
        var pcm = new byte[body.Length + tail.Length];

        body.CopyTo(pcm, 0);
        tail.CopyTo(pcm, body.Length);

        return clip with
        {
            Name = $"{clip.Name} (covas)",
            Pcm = pcm,
        };
    }

    /// <summary>One channel with the reverb and the faded tail, before <see cref="Gain"/>.</summary>
    internal static double[] Reverberate(double[] signal, int rate)
    {
        var room = new Room(rate);
        var tail = TailFrames(rate);
        var output = new double[signal.Length + tail];

        for (var index = 0; index < signal.Length; index++)
        {
            output[index] = room.Next(signal[index]);
        }

        for (var index = 0; index < tail; index++)
        {
            output[signal.Length + index] = room.Next(0) * Fade(index, tail);
        }

        return output;
    }

    private static int TailFrames(int rate) => (int)Math.Round(TailSeconds * rate);

    private static double Fade(int index, int tail) => (double)(tail - 1 - index) / tail;

    /// <summary>Interleaved 16-bit PCM, one <see cref="Room"/> a channel.</summary>
    private sealed class Reverb : IPcmFilter
    {
        private readonly Room[] _rooms;
        private readonly int _tail;
        private byte[] _held = [];
        private bool _finished;

        public Reverb(AudioFormat format)
        {
            var rate = format.SampleRate > 0 ? format.SampleRate : AudioFormat.Standard.SampleRate;

            _rooms = [.. Enumerable.Range(0, Math.Max(1, format.Channels)).Select(_ => new Room(rate))];
            _tail = TailFrames(rate);
        }

        public byte[] Push(ReadOnlySpan<byte> pcm)
        {
            if (_finished)
            {
                throw new InvalidOperationException("The reverb has already been finished.");
            }

            var input = pcm;

            if (_held.Length > 0)
            {
                var joined = new byte[_held.Length + pcm.Length];

                _held.CopyTo(joined, 0);
                pcm.CopyTo(joined.AsSpan(_held.Length));
                input = joined;
            }
            var whole = input.Length - (input.Length % (2 * _rooms.Length));
            var output = new byte[whole];

            for (var at = 0; at < whole; at += 2)
            {
                var sample = (short)(input[at] | (input[at + 1] << 8)) / 32768.0;

                Write(output, at, _rooms[at / 2 % _rooms.Length].Next(sample));
            }

            _held = input[whole..].ToArray();
            return output;
        }

        public byte[] Finish()
        {
            if (_finished)
            {
                return [];
            }

            _finished = true;

            var output = new byte[_tail * _rooms.Length * 2];

            for (var frame = 0; frame < _tail; frame++)
            {
                for (var channel = 0; channel < _rooms.Length; channel++)
                {
                    Write(output, ((frame * _rooms.Length) + channel) * 2, _rooms[channel].Next(0) * Fade(frame, _tail));
                }
            }

            return output;
        }

        private static void Write(byte[] pcm, int at, double sample)
        {
            var value = (short)Math.Clamp(Math.Round(sample * Gain * 32767.0), short.MinValue, short.MaxValue);

            pcm[at] = (byte)(value & 0xFF);
            pcm[at + 1] = (byte)((value >> 8) & 0xFF);
        }
    }

    /// <summary>The dry sample plus the wet: four parallel combs averaged, then two allpasses in series.</summary>
    private sealed class Room(int rate)
    {
        private readonly GuardianVoice.FeedbackCombLine[] _combs =
            [.. Combs.Select(comb => new GuardianVoice.FeedbackCombLine(GuardianVoice.Samples(comb.DelayMs, rate), comb.Feedback))];

        private readonly GuardianVoice.AllpassLine[] _allpasses =
            [.. AllpassMs.Select(delayMs => new GuardianVoice.AllpassLine(GuardianVoice.Samples(delayMs, rate), AllpassGain))];

        public double Next(double input)
        {
            var wet = 0.0;

            foreach (var comb in _combs)
            {
                wet += comb.Next(input) / Combs.Length;
            }

            foreach (var allpass in _allpasses)
            {
                wet = allpass.Next(wet);
            }

            return (Dry * input) + (Wet * wet);
        }
    }
}
