namespace D47.Core.Audio;

/// <summary>
/// Speech put through a comms link, so a voice arriving from outside the ship sounds like it arrived
/// from outside the ship.
/// </summary>
public static class RadioVoice
{
    /// <summary>Which roles arrive over a link rather than from the next seat.</summary>
    public static bool IsOverTheAir(VoiceRole role) => role is not (VoiceRole.ShipAi or VoiceRole.Crew);

    /// <summary>The treatment for a role, or null where there is none.</summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role) => Colours(role, 1);

    /// <summary>The treatment for a role over a link of <paramref name="strength"/>, 0 to 1, or null where there is none.</summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role, double strength) =>
        IsOverTheAir(role) ? clip => Apply(clip, strength) : null;

    /// <summary>The bottom of the passband.</summary>
    private const double LowEdgeHz = 400;

    /// <summary>The top.</summary>
    private const double HighEdgeHz = 2_700;

    /// <summary>The bottom of the passband on a link with no signal left.</summary>
    private const double WeakLowEdgeHz = 600;

    /// <summary>And its top.</summary>
    private const double WeakHighEdgeHz = 2_000;

    /// <summary>
    /// The loudness every transmission is brought to, as an RMS of full scale — the receiver's
    /// automatic gain control, which is the thing a real link has and d47 did not.
    /// </summary>
    private const double Target = 0.10;

    /// <summary>Butterworth.</summary>
    private const double Q = 0.70710678118654752;

    /// <summary>How hard the link is driven before it saturates.</summary>
    private const double Drive = 2.6;

    /// <summary>How loud the static is under the words.</summary>
    private const double HissUnderVoice = 0.034;

    /// <summary>And how loud it is once the words stop, on the bare carrier.</summary>
    private const double HissOnTheOpenCarrier = 0.148;

    /// <summary>How loud the bare carrier is on a link with no signal left.</summary>
    private const double HissOnAWeakCarrier = 0.3;

    /// <summary>How long one stretch of voice lost to a weak link lasts.</summary>
    private static readonly TimeSpan Dropout = TimeSpan.FromMilliseconds(90);

    /// <summary>How long the voice takes to go and come back around a dropout.</summary>
    private static readonly TimeSpan DropoutEdge = TimeSpan.FromMilliseconds(6);

    /// <summary>The chance each stretch is lost on a link with no signal left.</summary>
    private const double DropoutChance = 0.35;

    /// <summary>How long the floor takes to come up between the two.</summary>
    private static readonly TimeSpan Swell = TimeSpan.FromMilliseconds(40);

    /// <summary>How long the link stays open after the last word, before it cuts.</summary>
    private static readonly TimeSpan Tail = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the very end is faded over.</summary>
    private static readonly TimeSpan Cut = TimeSpan.FromMilliseconds(3);

    /// <summary>The same speech, over a link.</summary>
    public static AudioClip Apply(AudioClip clip) => Apply(clip, 1);

    /// <summary>
    /// The same speech, over a link of <paramref name="strength"/>, 0 to 1. Below 1 the static rises, the
    /// passband narrows and stretches of the voice drop out to the carrier.
    /// </summary>
    public static AudioClip Apply(AudioClip clip, double strength)
    {
        var weakness = double.IsNaN(strength) ? 0 : 1 - Math.Clamp(strength, 0, 1);
        var pcm = clip.Pcm.Span;
        var samples = pcm.Length / 2;

        if (samples == 0)
        {
            return clip;
        }

        var channels = Math.Max(1, clip.Format.Channels);
        var rate = clip.Format.SampleRate > 0 ? clip.Format.SampleRate : AudioFormat.Standard.SampleRate;

        var lowEdge = LowEdgeHz + (weakness * (WeakLowEdgeHz - LowEdgeHz));
        var highEdge = HighEdgeHz + (weakness * (WeakHighEdgeHz - HighEdgeHz));
        var hissUnderVoice = HissUnderVoice + (weakness * (HissOnTheOpenCarrier - HissUnderVoice));
        var hissOnTheCarrier = HissOnTheOpenCarrier + (weakness * (HissOnAWeakCarrier - HissOnTheOpenCarrier));

        // One filter pair per channel, and a second pair for the static.
        var voiceBand = Band(channels, rate, lowEdge, highEdge);
        var staticBand = Band(channels, rate, lowEdge, highEdge);

        var shaped = new double[samples];

        var isSquared = 0.0;
        var peak = 0.0;

        for (var index = 0; index < samples; index++)
        {
            var channel = index % channels;
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;

            sample = Math.Tanh(voiceBand.Low[channel].Next(voiceBand.High[channel].Next(sample)) * Drive);

            shaped[index] = sample;
            isSquared += sample * sample;
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var makeup = Makeup(Math.Sqrt(isSquared / samples), peak);
        var voiced = Voiced((samples + channels - 1) / channels, rate, weakness);

        // The words, then the carrier still open, then the cut.
        var tail = (int)(Tail.TotalSeconds * rate) / channels * channels;
        var total = samples + tail;
        var fade = Math.Min(total, (int)(Cut.TotalSeconds * rate));

        var treated = new byte[total * 2];
        var noise = 0x9E3779B9u;
        var swell = Math.Max(1, (int)(Swell.TotalSeconds * rate));

        for (var index = 0; index < total; index++)
        {
            var channel = index % channels;

            noise = (noise * 1664525u) + 1013904223u;
            var white = ((noise >> 8) / 16777215.0 * 2) - 1;

            // The static goes through a band-pass of its own, so it is the same colour as the voice arriving
            // beside it.
            var gain = index < samples ? voiced?[index / channels] ?? 1 : 1;

            var hiss = index < samples
                ? hissUnderVoice + ((hissOnTheCarrier - hissUnderVoice) * (1 - gain))
                : hissUnderVoice + ((hissOnTheCarrier - hissUnderVoice)
                                    * Math.Min(1, (double)(index - samples) / swell));

            var floor = staticBand.Low[channel].Next(staticBand.High[channel].Next(white)) * hiss;

            var sample = (index < samples ? shaped[index] * makeup * gain : 0) + floor;

            if (index >= total - fade)
            {
                sample *= (double)(total - index) / fade;
            }

            var value = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);

            treated[index * 2] = (byte)(value & 0xFF);
            treated[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return clip with { Name = $"{clip.Name} (radio)", Pcm = treated };
    }

    /// <summary>A band-pass per channel, between the two edges.</summary>
    private static (Biquad[] High, Biquad[] Low) Band(int channels, int rate, double lowEdge, double highEdge)
    {
        var high = new Biquad[channels];
        var low = new Biquad[channels];

        for (var channel = 0; channel < channels; channel++)
        {
            high[channel] = Biquad.HighPass(lowEdge, rate);
            low[channel] = Biquad.LowPass(highEdge, rate);
        }

        return (high, low);
    }

    /// <summary>
    /// The voice's gain per frame, 0 where a weak link has lost it, ramped at each edge; null when nothing is
    /// lost. Seeded by a constant, so the same clip loses the same stretches.
    /// </summary>
    private static double[]? Voiced(int frames, int rate, double weakness)
    {
        var chance = weakness * DropoutChance;

        if (chance <= 0 || frames == 0)
        {
            return null;
        }

        var stretch = Math.Max(1, (int)(Dropout.TotalSeconds * rate));
        var step = 1.0 / Math.Max(1, (int)(DropoutEdge.TotalSeconds * rate));
        var gains = new double[frames];
        var state = 0x2545F491u;
        var gain = 1.0;
        var lost = false;

        for (var frame = 0; frame < frames; frame++)
        {
            if (frame % stretch == 0)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                lost = state / (double)uint.MaxValue < chance;
            }

            gain = lost ? Math.Max(0, gain - step) : Math.Min(1, gain + step);
            gains[frame] = gain;
        }

        return gains;
    }

    /// <summary>The gain that brings the treated line to <see cref="Target"/> — the receiver's AGC.</summary>
    private static double Makeup(double now, double peak)
    {
        if (now <= 0)
        {
            return 1;
        }

        var gain = Math.Clamp(Target / now, 0.1, 4.0);

        return peak * gain > 1 ? 1 / peak : gain;
    }

    /// <summary>One second-order section, direct form 1, with the usual cookbook coefficients.</summary>
    private struct Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public static Biquad LowPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 - cos) / 2 / a0,
                _b1 = (1 - cos) / a0,
                _b2 = (1 - cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        public static Biquad HighPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 + cos) / 2 / a0,
                _b1 = -(1 + cos) / a0,
                _b2 = (1 + cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        /// <summary>The two quantities both responses are built from.</summary>
        private static (double Cos, double Alpha) Shape(double frequency, int sampleRate)
        {
            var rate = sampleRate > 0 ? sampleRate : AudioFormat.Standard.SampleRate;
            var w0 = 2 * Math.PI * Math.Clamp(frequency, 1, rate * 0.45) / rate;

            return (Math.Cos(w0), Math.Sin(w0) / (2 * Q));
        }

        public double Next(double x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }
    }
}
