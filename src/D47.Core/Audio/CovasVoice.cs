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

    /// <summary>The clip with the reverb added, a faded tail appended, and the result at the dry clip's loudness.</summary>
    public static AudioClip Apply(AudioClip clip)
    {
        var channels = Math.Max(1, clip.Format.Channels);
        var frames = clip.Pcm.Length / (2 * channels);

        if (frames == 0)
        {
            return clip;
        }

        var rate = clip.Format.SampleRate > 0 ? clip.Format.SampleRate : AudioFormat.Standard.SampleRate;
        var dry = GuardianVoice.Decode(clip.Pcm.Span, channels, frames);
        var treated = new double[channels][];

        for (var channel = 0; channel < channels; channel++)
        {
            treated[channel] = Reverb(dry[channel], rate);
        }

        return clip with
        {
            Name = $"{clip.Name} (covas)",
            Pcm = GuardianVoice.Encode(treated, GuardianVoice.Level(dry, treated, frames)),
        };
    }

    private static double[] Reverb(double[] signal, int rate)
    {
        var tail = (int)Math.Round(TailSeconds * rate);
        var total = signal.Length + tail;
        var wet = new double[total];

        foreach (var (delayMs, feedback) in Combs)
        {
            var comb = GuardianVoice.FeedbackComb(signal, GuardianVoice.Samples(delayMs, rate), feedback, total);

            for (var index = 0; index < total; index++)
            {
                wet[index] += comb[index] / Combs.Length;
            }
        }

        foreach (var delayMs in AllpassMs)
        {
            wet = GuardianVoice.Allpass(wet, GuardianVoice.Samples(delayMs, rate), AllpassGain);
        }

        var output = new double[total];

        for (var index = 0; index < total; index++)
        {
            var sample = (Dry * (index < signal.Length ? signal[index] : 0)) + (Wet * wet[index]);

            if (index >= signal.Length)
            {
                sample *= (double)(total - 1 - index) / tail;
            }

            output[index] = sample;
        }

        return output;
    }
}
