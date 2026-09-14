using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>The treatments a Guardian core's voice can be given, in any combination.</summary>
[Flags]
public enum GuardianTreatment
{
    None = 0,

    /// <summary>Channel vocoder onto a fixed-pitch sawtooth.</summary>
    Cylon = 1 << 0,

    /// <summary>Four semitones down, duration kept.</summary>
    PitchDown = 1 << 1,

    /// <summary>The line an octave lower, mixed under the dry voice.</summary>
    OctaveDown = 1 << 2,

    /// <summary>Three swept delayed copies.</summary>
    Chorus = 1 << 3,

    /// <summary>A 9 ms feedback comb.</summary>
    Comb = 1 << 4,

    /// <summary>A 45 Hz ring modulator blended with the dry voice.</summary>
    RingMod = 1 << 5,

    /// <summary>Short damaged stretches at irregular intervals.</summary>
    Glitch = 1 << 6,

    /// <summary>Schroeder reverb, adding a half-second tail.</summary>
    Reverb = 1 << 7,
}

/// <summary>
/// Speech made to sound like a Guardian core. Treatments run in the order the flags are declared, whatever is
/// switched on, and the result is brought back to the dry clip's loudness.
/// </summary>
public static class GuardianVoice
{
    /// <summary>The vocoder's carrier, as a share of the core's base pitch.</summary>
    private const double CarrierShare = 0.85;

    /// <summary>White noise in the carrier, relative to the sawtooth's amplitude.</summary>
    private const double CarrierNoise = 0.15;

    /// <summary>How far either side of a bin the speech envelope is averaged over.</summary>
    private const double EnvelopeHalfWidthHz = 280;

    private const double PitchDownSemitones = -4;

    private const double OctaveLayer = 0.6;

    private const double ChorusLayer = 0.8;

    /// <summary>(base ms, depth ms, rate Hz, phase) per copy.</summary>
    private static readonly (double Base, double Depth, double Rate, double Phase)[] ChorusCopies =
    [
        (12, 2.5, 0.31, 0),
        (19, 3.0, 0.47, 1.3),
        (27, 3.5, 0.23, 2.6),
    ];

    private const double CombDelayMs = 9;
    private const double CombFeedback = 0.7;
    private const double CombDry = 0.4;
    private const double CombWet = 0.6;

    private const double RingHz = 45;
    private const double RingDry = 0.65;
    private const double RingWet = 0.35;

    private static readonly (double DelayMs, double Feedback)[] ReverbCombs =
    [
        (29.7, 0.88),
        (37.1, 0.87),
        (41.1, 0.87),
        (43.7, 0.86),
    ];

    private static readonly double[] ReverbAllpassMs = [5, 1.7];
    private const double ReverbAllpassGain = 0.7;
    private const double ReverbLowPassHz = 5_000;
    private const double ReverbDry = 0.75;
    private const double ReverbWet = 0.45;
    private const double ReverbTailSeconds = 0.5;

    /// <summary>The highest peak the levelled result may reach.</summary>
    private const double Ceiling = 0.98;

    /// <summary>The phase-vocoder frame, as a duration; rounded to a power of two at the clip's rate.</summary>
    private const double FrameSeconds = 2048.0 / 48_000;

    /// <summary>
    /// The clip with the chosen treatments applied. <paramref name="basePitchHz"/> is the core's fixed pitch,
    /// which the <see cref="GuardianTreatment.Cylon"/> carrier is derived from; it is not measured per clip.
    /// </summary>
    public static AudioClip Apply(AudioClip clip, GuardianTreatment treatments, double basePitchHz)
    {
        var channels = Math.Max(1, clip.Format.Channels);
        var frames = clip.Pcm.Length / (2 * channels);

        if (treatments == GuardianTreatment.None || frames == 0)
        {
            return clip;
        }

        var rate = clip.Format.SampleRate > 0 ? clip.Format.SampleRate : AudioFormat.Standard.SampleRate;
        var dry = Decode(clip.Pcm.Span, channels, frames);
        var treated = new double[channels][];

        for (var channel = 0; channel < channels; channel++)
        {
            treated[channel] = Chain(dry[channel], treatments, basePitchHz, rate);
        }

        return clip with
        {
            Name = $"{clip.Name} (guardian)",
            Pcm = Encode(treated, Level(dry, treated, frames)),
        };
    }

    /// <summary>Every treatment switched on in settings, combined (#225).</summary>
    public static GuardianTreatment TreatmentsFrom(SpeechSettings speech)
    {
        var treatments = GuardianTreatment.None;

        if (speech.GuardianVoiceCylon)
        {
            treatments |= GuardianTreatment.Cylon;
        }

        if (speech.GuardianVoicePitchDown)
        {
            treatments |= GuardianTreatment.PitchDown;
        }

        if (speech.GuardianVoiceOctaveDown)
        {
            treatments |= GuardianTreatment.OctaveDown;
        }

        if (speech.GuardianVoiceChorus)
        {
            treatments |= GuardianTreatment.Chorus;
        }

        if (speech.GuardianVoiceComb)
        {
            treatments |= GuardianTreatment.Comb;
        }

        if (speech.GuardianVoiceRingMod)
        {
            treatments |= GuardianTreatment.RingMod;
        }

        if (speech.GuardianVoiceGlitch)
        {
            treatments |= GuardianTreatment.Glitch;
        }

        if (speech.GuardianVoiceReverb)
        {
            treatments |= GuardianTreatment.Reverb;
        }

        return treatments;
    }

    /// <summary>
    /// The Cylon carrier's base pitch for a core's <see cref="VoiceHint.Gender"/> — the median pitches
    /// measured for Edge Andrew and Ava (#224).
    /// </summary>
    public static double BasePitchHz(D47.Core.Persona.VoiceGender gender) => gender switch
    {
        D47.Core.Persona.VoiceGender.Male => 124,
        D47.Core.Persona.VoiceGender.Female => 209,
        _ => 165,
    };

    /// <summary>The ship AI's treatment for the settings in force, or null when every toggle is off (#225).</summary>
    public static Func<AudioClip, AudioClip>? ColourFor(SpeechSettings speech, D47.Core.Persona.VoiceGender gender)
    {
        var treatments = TreatmentsFrom(speech);

        if (treatments == GuardianTreatment.None)
        {
            return null;
        }

        var basePitch = BasePitchHz(gender);

        return clip => Apply(clip, treatments, basePitch);
    }

    private static double[] Chain(double[] signal, GuardianTreatment treatments, double basePitchHz, int rate)
    {
        if (treatments.HasFlag(GuardianTreatment.Cylon))
        {
            signal = Cylon(signal, basePitchHz * CarrierShare, rate);
        }

        if (treatments.HasFlag(GuardianTreatment.PitchDown))
        {
            signal = Shift(signal, Math.Pow(2, PitchDownSemitones / 12), rate);
        }

        if (treatments.HasFlag(GuardianTreatment.OctaveDown))
        {
            var layer = Shift(signal, 0.5, rate);
            signal = Mix(signal, 1, layer, OctaveLayer);
        }

        if (treatments.HasFlag(GuardianTreatment.Chorus))
        {
            signal = Chorus(signal, rate);
        }

        if (treatments.HasFlag(GuardianTreatment.Comb))
        {
            signal = Comb(signal, rate);
        }

        if (treatments.HasFlag(GuardianTreatment.RingMod))
        {
            signal = RingMod(signal, rate);
        }

        if (treatments.HasFlag(GuardianTreatment.Glitch))
        {
            signal = Glitch(signal, rate);
        }

        if (treatments.HasFlag(GuardianTreatment.Reverb))
        {
            signal = Reverb(signal, rate);
        }

        return signal;
    }

    /// <summary>The speech's smoothed spectral envelope imposed on a flattened sawtooth-plus-noise carrier.</summary>
    private static double[] Cylon(double[] speech, double carrierHz, int rate)
    {
        var length = speech.Length;
        var frame = FrameLength(rate);
        var hop = frame / 4;
        var bins = frame / 2;
        var padded = length + (2 * frame);
        var halfWidth = Math.Max(1, (int)Math.Round(EnvelopeHalfWidthHz / ((double)rate / frame)));

        var carrier = Carrier(padded, carrierHz, rate);
        var output = new double[padded];
        var window = Hann(frame);

        var speechRe = new double[frame];
        var speechIm = new double[frame];
        var carrierRe = new double[frame];
        var carrierIm = new double[frame];
        var speechMagnitude = new double[bins + 1];
        var carrierMagnitude = new double[bins + 1];
        var speechEnvelope = new double[bins + 1];
        var carrierEnvelope = new double[bins + 1];

        for (var start = 0; start + frame <= padded; start += hop)
        {
            for (var index = 0; index < frame; index++)
            {
                var source = start + index - frame;
                speechRe[index] = (source >= 0 && source < length ? speech[source] : 0) * window[index];
                speechIm[index] = 0;
                carrierRe[index] = carrier[start + index] * window[index];
                carrierIm[index] = 0;
            }

            Fourier(speechRe, speechIm, inverse: false);
            Fourier(carrierRe, carrierIm, inverse: false);

            for (var bin = 0; bin <= bins; bin++)
            {
                speechMagnitude[bin] = Math.Sqrt((speechRe[bin] * speechRe[bin]) + (speechIm[bin] * speechIm[bin]));
                carrierMagnitude[bin] = Math.Sqrt((carrierRe[bin] * carrierRe[bin]) + (carrierIm[bin] * carrierIm[bin]));
            }

            Smooth(speechMagnitude, speechEnvelope, halfWidth);
            Smooth(carrierMagnitude, carrierEnvelope, halfWidth);

            for (var bin = 0; bin <= bins; bin++)
            {
                var gain = carrierEnvelope[bin] > 1e-12 ? speechEnvelope[bin] / carrierEnvelope[bin] : 0;
                carrierRe[bin] *= gain;
                carrierIm[bin] *= gain;
            }

            Mirror(carrierRe, carrierIm);
            Fourier(carrierRe, carrierIm, inverse: true);

            for (var index = 0; index < frame; index++)
            {
                output[start + index] += carrierRe[index] * window[index] / OverlapGain;
            }
        }

        return output.AsSpan(frame, length).ToArray();
    }

    /// <summary>A band-limited sawtooth at unit amplitude with white noise under it.</summary>
    private static double[] Carrier(int length, double hertz, int rate)
    {
        var carrier = new double[length];
        var step = Math.Clamp(hertz, 1, rate * 0.45) / rate;
        var phase = 0.0;
        var noise = new Noise(0x6A09E667u);

        for (var index = 0; index < length; index++)
        {
            carrier[index] = (2 * phase) - 1 - PolyBlep(phase, step) + (CarrierNoise * noise.Next(-1, 1));

            phase += step;

            if (phase >= 1)
            {
                phase -= 1;
            }
        }

        return carrier;
    }

    /// <summary>The correction that removes the aliasing from a sawtooth's reset.</summary>
    private static double PolyBlep(double phase, double step)
    {
        if (phase < step)
        {
            var t = phase / step;
            return t + t - (t * t) - 1;
        }

        if (phase > 1 - step)
        {
            var t = (phase - 1) / step;
            return (t * t) + t + t + 1;
        }

        return 0;
    }

    /// <summary>
    /// A pitch shift by <paramref name="factor"/> that keeps the duration: a phase vocoder with identity phase
    /// locking compresses time by the factor, and resampling restores the length.
    /// </summary>
    private static double[] Shift(double[] signal, double factor, int rate)
    {
        var length = signal.Length;
        var frame = FrameLength(rate);
        var synthesisHop = frame / 4;
        var analysisHop = Math.Max(1, (int)Math.Round(synthesisHop / factor));
        var bins = frame / 2;
        var frames = ((length + frame) / analysisHop) + 1;

        var stretched = new double[((frames - 1) * synthesisHop) + frame];
        var window = Hann(frame);
        var re = new double[frame];
        var im = new double[frame];
        var magnitude = new double[bins + 1];
        var phase = new double[bins + 1];
        var previous = new double[bins + 1];
        var synthesis = new double[bins + 1];
        var peaks = new List<int>();

        for (var k = 0; k < frames; k++)
        {
            var start = (k * analysisHop) - frame;

            for (var index = 0; index < frame; index++)
            {
                var source = start + index;
                re[index] = (source >= 0 && source < length ? signal[source] : 0) * window[index];
                im[index] = 0;
            }

            Fourier(re, im, inverse: false);

            for (var bin = 0; bin <= bins; bin++)
            {
                magnitude[bin] = Math.Sqrt((re[bin] * re[bin]) + (im[bin] * im[bin]));
                phase[bin] = Math.Atan2(im[bin], re[bin]);
            }

            if (k == 0)
            {
                Array.Copy(phase, synthesis, bins + 1);
            }
            else
            {
                Lock(magnitude, phase, previous, synthesis, peaks, frame, analysisHop, synthesisHop);
            }

            Array.Copy(phase, previous, bins + 1);

            for (var bin = 0; bin <= bins; bin++)
            {
                re[bin] = magnitude[bin] * Math.Cos(synthesis[bin]);
                im[bin] = magnitude[bin] * Math.Sin(synthesis[bin]);
            }

            Mirror(re, im);
            Fourier(re, im, inverse: true);

            var at = k * synthesisHop;

            for (var index = 0; index < frame; index++)
            {
                stretched[at + index] += re[index] * window[index] / OverlapGain;
            }
        }

        var ratio = (double)synthesisHop / analysisHop;
        var shifted = new double[length];

        for (var index = 0; index < length; index++)
        {
            shifted[index] = Read(stretched, ((index + (frame / 2.0)) * ratio) + (frame / 2.0));
        }

        return shifted;
    }

    /// <summary>
    /// Advances the synthesis phase of each spectral peak by its measured frequency, and sets every other bin's
    /// phase relative to the nearest peak.
    /// </summary>
    private static void Lock(
        double[] magnitude,
        double[] phase,
        double[] previous,
        double[] synthesis,
        List<int> peaks,
        int frame,
        int analysisHop,
        int synthesisHop)
    {
        var bins = frame / 2;
        peaks.Clear();

        for (var bin = 1; bin < bins; bin++)
        {
            if (magnitude[bin] > magnitude[bin - 1] && magnitude[bin] >= magnitude[bin + 1])
            {
                peaks.Add(bin);
            }
        }

        if (peaks.Count == 0)
        {
            Array.Copy(phase, synthesis, bins + 1);
            return;
        }

        foreach (var peak in peaks)
        {
            var expected = 2 * Math.PI * peak / frame;
            var deviation = Wrap(phase[peak] - previous[peak] - (expected * analysisHop));
            synthesis[peak] = Wrap(synthesis[peak] + ((expected + (deviation / analysisHop)) * synthesisHop));
        }

        var nearest = 0;

        for (var bin = 0; bin <= bins; bin++)
        {
            while (nearest + 1 < peaks.Count && bin > (peaks[nearest] + peaks[nearest + 1]) / 2)
            {
                nearest++;
            }

            var peak = peaks[nearest];

            if (bin != peak)
            {
                synthesis[bin] = synthesis[peak] + phase[bin] - phase[peak];
            }
        }
    }

    private static double[] Chorus(double[] signal, int rate)
    {
        var output = new double[signal.Length];

        for (var index = 0; index < signal.Length; index++)
        {
            var seconds = (double)index / rate;
            var copies = 0.0;

            foreach (var (baseMs, depthMs, hertz, offset) in ChorusCopies)
            {
                var delayMs = baseMs + (depthMs * Math.Sin((2 * Math.PI * hertz * seconds) + offset));
                copies += Read(signal, index - (delayMs / 1000 * rate));
            }

            output[index] = signal[index] + (ChorusLayer * copies);
        }

        return output;
    }

    private static double[] Comb(double[] signal, int rate)
    {
        var delay = Samples(CombDelayMs, rate);
        var fed = FeedbackComb(signal, delay, CombFeedback, signal.Length);

        return Mix(signal, CombDry, fed, CombWet);
    }

    private static double[] RingMod(double[] signal, int rate)
    {
        var output = new double[signal.Length];

        for (var index = 0; index < signal.Length; index++)
        {
            var carrier = Math.Sin(2 * Math.PI * RingHz * index / rate);
            output[index] = (RingDry * signal[index]) + (RingWet * signal[index] * carrier);
        }

        return output;
    }

    /// <summary>
    /// Damages one short stretch at a time, rotating through sample-and-hold, bit reduction and stutter. The
    /// schedule comes from a fixed seed, so the same clip is damaged the same way every time.
    /// </summary>
    private static double[] Glitch(double[] signal, int rate)
    {
        var output = (double[])signal.Clone();
        var random = new Noise(0xBB67AE85u);
        var at = random.Next(0.4, 0.9) * rate;
        var kind = 0;

        while (at < signal.Length)
        {
            var start = (int)at;
            var end = Math.Min(signal.Length, start + (int)(random.Next(0.06, 0.18) * rate));

            switch (kind % 3)
            {
                case 0:
                    var hold = Math.Max(1, (int)Math.Round(random.Next(0.25, 0.55) / 1000 * rate));

                    for (var index = start; index < end; index++)
                    {
                        output[index] = signal[start + ((index - start) / hold * hold)];
                    }

                    break;

                case 1:
                    var step = 2.0 / Math.Round(random.Next(8, 32));

                    for (var index = start; index < end; index++)
                    {
                        output[index] = Math.Round(signal[index] / step) * step;
                    }

                    break;

                default:
                    var grain = Math.Max(1, (int)(random.Next(0.025, 0.05) * rate));

                    for (var index = start; index < end; index++)
                    {
                        output[index] = signal[start + ((index - start) % grain)];
                    }

                    break;
            }

            kind++;
            at += random.Next(0.7, 1.6) * rate;
        }

        return output;
    }

    /// <summary>Four parallel combs averaged, two allpasses in series, a low-pass on the wet, and a faded tail.</summary>
    private static double[] Reverb(double[] signal, int rate)
    {
        var tail = (int)Math.Round(ReverbTailSeconds * rate);
        var total = signal.Length + tail;
        var wet = new double[total];

        foreach (var (delayMs, feedback) in ReverbCombs)
        {
            var comb = FeedbackComb(signal, Samples(delayMs, rate), feedback, total);

            for (var index = 0; index < total; index++)
            {
                wet[index] += comb[index] / ReverbCombs.Length;
            }
        }

        foreach (var delayMs in ReverbAllpassMs)
        {
            wet = Allpass(wet, Samples(delayMs, rate), ReverbAllpassGain);
        }

        var smoothing = Math.Exp(-2 * Math.PI * ReverbLowPassHz / rate);
        var low = 0.0;
        var output = new double[total];

        for (var index = 0; index < total; index++)
        {
            low = ((1 - smoothing) * wet[index]) + (smoothing * low);

            var sample = (ReverbDry * (index < signal.Length ? signal[index] : 0)) + (ReverbWet * low);

            if (index >= signal.Length)
            {
                sample *= (double)(total - 1 - index) / tail;
            }

            output[index] = sample;
        }

        return output;
    }

    /// <summary><c>y[n] = x[n] + g·y[n−D]</c>, run for <paramref name="length"/> samples.</summary>
    private static double[] FeedbackComb(double[] signal, int delay, double feedback, int length)
    {
        var output = new double[length];

        for (var index = 0; index < length; index++)
        {
            var input = index < signal.Length ? signal[index] : 0;
            output[index] = input + (index >= delay ? feedback * output[index - delay] : 0);
        }

        return output;
    }

    /// <summary><c>y[n] = −g·x[n] + x[n−D] + g·y[n−D]</c>.</summary>
    private static double[] Allpass(double[] signal, int delay, double gain)
    {
        var output = new double[signal.Length];

        for (var index = 0; index < signal.Length; index++)
        {
            var delayed = index >= delay ? signal[index - delay] + (gain * output[index - delay]) : 0;
            output[index] = (-gain * signal[index]) + delayed;
        }

        return output;
    }

    private static double[] Mix(double[] first, double firstGain, double[] second, double secondGain)
    {
        var output = new double[first.Length];

        for (var index = 0; index < first.Length; index++)
        {
            output[index] = (firstGain * first[index]) + (secondGain * second[index]);
        }

        return output;
    }

    /// <summary>
    /// The gain that brings the treated clip to the dry clip's RMS, measured over the dry clip's length, lowered
    /// where the treated peak would exceed <see cref="Ceiling"/>.
    /// </summary>
    private static double Level(double[][] dry, double[][] treated, int frames)
    {
        var drySquared = 0.0;
        var treatedSquared = 0.0;
        var peak = 0.0;

        for (var channel = 0; channel < dry.Length; channel++)
        {
            for (var index = 0; index < frames; index++)
            {
                drySquared += dry[channel][index] * dry[channel][index];
                treatedSquared += treated[channel][index] * treated[channel][index];
            }

            foreach (var sample in treated[channel])
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }
        }

        var gain = drySquared > 0 && treatedSquared > 0 ? Math.Sqrt(drySquared / treatedSquared) : 1;

        return peak * gain > Ceiling ? Ceiling / peak : gain;
    }

    private static double[][] Decode(ReadOnlySpan<byte> pcm, int channels, int frames)
    {
        var decoded = new double[channels][];

        for (var channel = 0; channel < channels; channel++)
        {
            decoded[channel] = new double[frames];
        }

        for (var index = 0; index < frames * channels; index++)
        {
            decoded[index % channels][index / channels] =
                (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
        }

        return decoded;
    }

    private static byte[] Encode(double[][] channels, double gain)
    {
        var frames = channels[0].Length;
        var pcm = new byte[frames * channels.Length * 2];

        for (var index = 0; index < frames * channels.Length; index++)
        {
            var sample = channels[index % channels.Length][index / channels.Length] * gain;
            var value = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }

    /// <summary>The sum of the squared Hann window at a quarter-frame hop.</summary>
    private const double OverlapGain = 1.5;

    /// <summary>The power of two nearest <see cref="FrameSeconds"/> at this rate.</summary>
    private static int FrameLength(int rate) =>
        1 << Math.Max(4, (int)Math.Round(Math.Log2(FrameSeconds * rate)));

    private static int Samples(double milliseconds, int rate) =>
        Math.Max(1, (int)Math.Round(milliseconds / 1000 * rate));

    /// <summary>Periodic Hann.</summary>
    private static double[] Hann(int length)
    {
        var window = new double[length];

        for (var index = 0; index < length; index++)
        {
            window[index] = 0.5 - (0.5 * Math.Cos(2 * Math.PI * index / length));
        }

        return window;
    }

    /// <summary>A linearly interpolated read, zero outside the signal.</summary>
    private static double Read(double[] signal, double position)
    {
        if (position < 0 || position > signal.Length - 1)
        {
            return 0;
        }

        var below = (int)position;

        if (below >= signal.Length - 1)
        {
            return signal[^1];
        }

        var fraction = position - below;

        return (signal[below] * (1 - fraction)) + (signal[below + 1] * fraction);
    }

    /// <summary>A moving average of <paramref name="source"/> over ±<paramref name="halfWidth"/> bins.</summary>
    private static void Smooth(double[] source, double[] target, int halfWidth)
    {
        var running = 0.0;
        var from = 0;
        var to = -1;

        for (var bin = 0; bin < source.Length; bin++)
        {
            while (to < Math.Min(source.Length - 1, bin + halfWidth))
            {
                running += source[++to];
            }

            while (from < bin - halfWidth)
            {
                running -= source[from++];
            }

            target[bin] = Math.Max(0, running) / (to - from + 1);
        }
    }

    /// <summary>Fills the negative-frequency half from the positive one, so the inverse transform is real.</summary>
    private static void Mirror(double[] re, double[] im)
    {
        var length = re.Length;

        for (var bin = 1; bin < length / 2; bin++)
        {
            re[length - bin] = re[bin];
            im[length - bin] = -im[bin];
        }

        im[0] = 0;
        im[length / 2] = 0;
    }

    private static double Wrap(double angle) => angle - (2 * Math.PI * Math.Round(angle / (2 * Math.PI)));

    /// <summary>In-place radix-2 FFT; the length must be a power of two. The inverse is scaled by 1/N.</summary>
    private static void Fourier(double[] re, double[] im, bool inverse)
    {
        var length = re.Length;

        for (int index = 1, swap = 0; index < length; index++)
        {
            var bit = length >> 1;

            for (; (swap & bit) != 0; bit >>= 1)
            {
                swap ^= bit;
            }

            swap ^= bit;

            if (index < swap)
            {
                (re[index], re[swap]) = (re[swap], re[index]);
                (im[index], im[swap]) = (im[swap], im[index]);
            }
        }

        for (var size = 2; size <= length; size <<= 1)
        {
            var angle = (inverse ? 2 : -2) * Math.PI / size;
            var stepRe = Math.Cos(angle);
            var stepIm = Math.Sin(angle);
            var half = size / 2;

            for (var start = 0; start < length; start += size)
            {
                var turnRe = 1.0;
                var turnIm = 0.0;

                for (var offset = 0; offset < half; offset++)
                {
                    var a = start + offset;
                    var b = a + half;
                    var productRe = (re[b] * turnRe) - (im[b] * turnIm);
                    var productIm = (re[b] * turnIm) + (im[b] * turnRe);

                    re[b] = re[a] - productRe;
                    im[b] = im[a] - productIm;
                    re[a] += productRe;
                    im[a] += productIm;

                    (turnRe, turnIm) = ((turnRe * stepRe) - (turnIm * stepIm), (turnRe * stepIm) + (turnIm * stepRe));
                }
            }
        }

        if (inverse)
        {
            for (var index = 0; index < length; index++)
            {
                re[index] /= length;
                im[index] /= length;
            }
        }
    }

    /// <summary>A linear congruential generator, so every draw is the same from one call to the next.</summary>
    private struct Noise(uint seed)
    {
        private uint _state = seed;

        public double Next(double low, double high)
        {
            _state = (_state * 1664525u) + 1013904223u;
            return low + ((high - low) * ((_state >> 8) / 16777215.0));
        }
    }
}
