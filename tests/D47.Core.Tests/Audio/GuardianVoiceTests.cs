using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;
using VoiceGender = D47.Core.Persona.VoiceGender;

namespace D47.Core.Tests.Audio;

/// <summary>The treatments that make a Guardian core not sound like a person.</summary>
public class GuardianVoiceTests
{
    private const int Rate = 48_000;

    /// <summary>A neutral core's base pitch.</summary>
    private const double BasePitch = 200;

    private static readonly GuardianTreatment[] EachTreatment =
        Enum.GetValues<GuardianTreatment>().Where(t => t != GuardianTreatment.None).ToArray();

    private static AudioClip Clip(double[] samples, string name = "line")
    {
        var pcm = new byte[samples.Length * 2];

        for (var index = 0; index < samples.Length; index++)
        {
            var value = (short)Math.Clamp(Math.Round(samples[index] * short.MaxValue), short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip(name, pcm, AudioFormat.Standard);
    }

    private static AudioClip Tone(double hertz, double seconds = 1.0, double amplitude = 0.3) =>
        Clip(Enumerable.Range(0, (int)(Rate * seconds))
            .Select(index => Math.Sin(2 * Math.PI * hertz * index / Rate) * amplitude)
            .ToArray());

    /// <summary>A voiced sound: every harmonic of a fundamental up to 4 kHz, falling off as 1/n.</summary>
    private static AudioClip Voiced(double hertz, double seconds = 1.0, double amplitude = 0.1) =>
        Clip(Enumerable.Range(0, (int)(Rate * seconds))
            .Select(index => Enumerable.Range(1, (int)(4_000 / hertz))
                .Sum(n => Math.Sin(2 * Math.PI * hertz * n * index / Rate) / n) * amplitude)
            .ToArray());

    private static AudioClip WhiteNoise(double seconds = 2.0)
    {
        var state = 12345u;

        return Clip(Enumerable.Range(0, (int)(Rate * seconds))
            .Select(_ =>
            {
                state = (state * 1664525u) + 1013904223u;
                return (((state >> 8) / 16777215.0 * 2) - 1) * 0.2;
            })
            .ToArray());
    }

    private static double[] Samples(AudioClip clip)
    {
        var pcm = clip.Pcm.Span;
        var samples = new double[pcm.Length / 2];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
        }

        return samples;
    }

    /// <summary>The middle half, clear of anything happening at either end.</summary>
    private static double[] Middle(AudioClip clip)
    {
        var samples = Samples(clip);
        return samples.AsSpan(samples.Length / 4, samples.Length / 2).ToArray();
    }

    /// <summary>Goertzel.</summary>
    private static double Magnitude(double[] samples, double hertz)
    {
        var coefficient = 2 * Math.Cos(2 * Math.PI * hertz / Rate);
        var previous = 0.0;
        var older = 0.0;

        foreach (var sample in samples)
        {
            var current = sample + (coefficient * previous) - older;
            older = previous;
            previous = current;
        }

        return Math.Sqrt(Math.Max(0, (previous * previous) + (older * older) - (coefficient * previous * older)));
    }

    private static double Rms(ReadOnlySpan<double> samples)
    {
        var squared = 0.0;

        foreach (var sample in samples)
        {
            squared += sample * sample;
        }

        return samples.Length == 0 ? 0 : Math.Sqrt(squared / samples.Length);
    }

    [Fact]
    public void NoTreatmentHandsTheClipBack()
    {
        var line = Tone(440);

        Assert.Same(line, GuardianVoice.Apply(line, GuardianTreatment.None, BasePitch));
    }

    [Fact]
    public void AnEmptyClipIsHandedBackByEveryTreatment()
    {
        var empty = new AudioClip("nothing", ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);

        foreach (var treatment in EachTreatment)
        {
            Assert.Same(empty, GuardianVoice.Apply(empty, treatment, BasePitch));
        }
    }

    [Fact]
    public void PitchDownLowersAToneFourSemitonesAndKeepsItsLength()
    {
        var line = Tone(440);
        var treated = GuardianVoice.Apply(line, GuardianTreatment.PitchDown, BasePitch);

        Assert.Equal(line.Pcm.Length, treated.Pcm.Length);

        var middle = Middle(treated);
        var strongest = Enumerable.Range(0, 401).Select(step => 250 + (step * 0.5))
            .MaxBy(hertz => Magnitude(middle, hertz));

        Assert.InRange(strongest, 344, 354);
    }

    [Fact]
    public void OctaveDownAddsTheOctaveBelowAndKeepsTheNote()
    {
        var line = Tone(440);
        var treated = GuardianVoice.Apply(line, GuardianTreatment.OctaveDown, BasePitch);

        var dry = Middle(line);
        var wet = Middle(treated);

        Assert.True(Magnitude(dry, 220) < Magnitude(dry, 440) * 0.05);
        Assert.True(
            Magnitude(wet, 220) > Magnitude(wet, 440) * 0.3,
            $"220 Hz came out at {Magnitude(wet, 220) / Magnitude(wet, 440):P0} of 440 Hz");
        Assert.True(
            Magnitude(wet, 440) > Magnitude(dry, 440) * 0.5,
            $"440 Hz came out at {Magnitude(wet, 440) / Magnitude(dry, 440):P0} of what went in");
    }

    [Fact]
    public void RingModAddsSidebandsFortyFiveHertzEitherSide()
    {
        var line = Tone(1_000);
        var treated = Middle(GuardianVoice.Apply(line, GuardianTreatment.RingMod, BasePitch));
        var carrier = Magnitude(treated, 1_000);

        Assert.True(Magnitude(Middle(line), 955) < Magnitude(Middle(line), 1_000) * 0.05);
        Assert.True(Magnitude(treated, 955) > carrier * 0.15, "no lower sideband at 955 Hz");
        Assert.True(Magnitude(treated, 1_045) > carrier * 0.15, "no upper sideband at 1045 Hz");
        Assert.True(Magnitude(treated, 910) < carrier * 0.05, "energy at 910 Hz, where there should be none");
    }

    [Fact]
    public void CombPutsPeaksInNoiseEveryHundredAndElevenHertz()
    {
        var treated = Samples(GuardianVoice.Apply(WhiteNoise(), GuardianTreatment.Comb, BasePitch));
        var spacing = 1_000.0 / 9;

        var peaks = Enumerable.Range(3, 30).Average(k => Magnitude(treated, k * spacing));
        var troughs = Enumerable.Range(3, 30).Average(k => Magnitude(treated, (k + 0.5) * spacing));

        Assert.True(peaks > troughs * 2, $"the multiples of 111 Hz stand {peaks / troughs:F1}× above the gaps");
    }

    [Fact]
    public void CylonSpeaksAtTheCarrierWhateverPitchGoesIn()
    {
        var carrier = BasePitch * 0.85;

        foreach (var pitch in new[] { 120.0, 260.0 })
        {
            var treated = Middle(GuardianVoice.Apply(Voiced(pitch), GuardianTreatment.Cylon, BasePitch));
            var atCarrier = Magnitude(treated, carrier);

            Assert.True(
                atCarrier > Magnitude(treated, pitch) * 4,
                $"a {pitch} Hz input came out at {Magnitude(treated, pitch) / atCarrier:P0} of the carrier");
            Assert.True(
                atCarrier > Magnitude(treated, carrier / 2) * 4,
                $"a {pitch} Hz input has energy an octave below the carrier");
        }
    }

    [Fact]
    public void ReverbAddsAHalfSecondTailThatRingsAndEndsAtZero()
    {
        var line = Tone(440);
        var treated = GuardianVoice.Apply(line, GuardianTreatment.Reverb, BasePitch);

        Assert.Equal(line.Pcm.Length + (Rate / 2 * 2), treated.Pcm.Length);

        var samples = Samples(treated);
        var tail = samples.AsSpan(samples.Length - (Rate / 2), Rate / 4);

        Assert.True(Rms(tail) > 0.001, $"the tail is at {Rms(tail):F4} RMS");
        Assert.Equal(0, samples[^1]);
    }

    [Fact]
    public void EveryTreatmentButReverbKeepsTheLength()
    {
        var line = Voiced(150, seconds: 2.0);

        foreach (var treatment in EachTreatment.Where(t => t != GuardianTreatment.Reverb))
        {
            Assert.Equal(line.Pcm.Length, GuardianVoice.Apply(line, treatment, BasePitch).Pcm.Length);
        }
    }

    [Fact]
    public void TheSameLineTreatedTwiceIsTheSameBytes()
    {
        var all = EachTreatment.Aggregate(GuardianTreatment.None, (sum, t) => sum | t);

        foreach (var treatment in EachTreatment.Append(all))
        {
            var first = GuardianVoice.Apply(Voiced(150, seconds: 3.0), treatment, BasePitch);
            var second = GuardianVoice.Apply(Voiced(150, seconds: 3.0), treatment, BasePitch);

            Assert.Equal(first.Pcm.ToArray(), second.Pcm.ToArray());
        }
    }

    [Fact]
    public void GlitchChangesTheLine()
    {
        var line = Voiced(150, seconds: 3.0);
        var treated = GuardianVoice.Apply(line, GuardianTreatment.Glitch, BasePitch);

        Assert.NotEqual(line.Pcm.ToArray(), treated.Pcm.ToArray());
    }

    [Fact]
    public void EachTreatmentKeepsTheLineWithinADecibelOfItsLoudness()
    {
        var line = Voiced(150, seconds: 3.0);
        var dry = Samples(line);

        foreach (var treatment in EachTreatment)
        {
            var wet = Samples(GuardianVoice.Apply(line, treatment, BasePitch)).AsSpan(0, dry.Length);
            var decibels = 20 * Math.Log10(Rms(wet) / Rms(dry));

            Assert.True(Math.Abs(decibels) < 1, $"{treatment} moved the level {decibels:F2} dB");
        }
    }

    [Fact]
    public void AStereoClipIsTreatedOneChannelAtATime()
    {
        var format = new AudioFormat(Rate, 2);
        var frames = Rate * 2;
        var pcm = new byte[frames * 4];

        for (var frame = 0; frame < frames; frame++)
        {
            // A tone on the left, silence on the right.
            var value = (short)(Math.Sin(2 * Math.PI * 440 * frame / Rate) * 0.3 * short.MaxValue);

            pcm[frame * 4] = (byte)(value & 0xFF);
            pcm[(frame * 4) + 1] = (byte)((value >> 8) & 0xFF);
        }

        var stereo = new AudioClip("stereo", pcm, format);

        foreach (var treatment in EachTreatment)
        {
            var span = GuardianVoice.Apply(stereo, treatment, BasePitch).Pcm.Span;
            var left = 0;
            var right = 0;

            for (var frame = 0; frame < span.Length / 4; frame++)
            {
                left = Math.Max(left, Math.Abs((int)(short)(span[frame * 4] | (span[(frame * 4) + 1] << 8))));
                right = Math.Max(right, Math.Abs((int)(short)(span[(frame * 4) + 2] | (span[(frame * 4) + 3] << 8))));
            }

            Assert.True(left > 1_000, $"{treatment} silenced the left channel");
            Assert.True(right < 32, $"{treatment} put {right} into the silent channel");
        }
    }

    [Fact]
    public void EveryToggleOffCombinesToNoTreatment() =>
        Assert.Equal(GuardianTreatment.None, GuardianVoice.TreatmentsFrom(new SpeechSettings()));

    [Fact]
    public void EveryToggleOnCombinesToEveryTreatment()
    {
        var speech = new SpeechSettings
        {
            GuardianVoiceCylon = true,
            GuardianVoicePitchDown = true,
            GuardianVoiceOctaveDown = true,
            GuardianVoiceChorus = true,
            GuardianVoiceComb = true,
            GuardianVoiceRingMod = true,
            GuardianVoiceGlitch = true,
            GuardianVoiceReverb = true,
        };

        Assert.Equal(
            GuardianTreatment.Cylon
                | GuardianTreatment.PitchDown
                | GuardianTreatment.OctaveDown
                | GuardianTreatment.Chorus
                | GuardianTreatment.Comb
                | GuardianTreatment.RingMod
                | GuardianTreatment.Glitch
                | GuardianTreatment.Reverb,
            GuardianVoice.TreatmentsFrom(speech));
    }

    [Theory]
    [InlineData(VoiceGender.Male, 124)]
    [InlineData(VoiceGender.Female, 209)]
    [InlineData(VoiceGender.Unspecified, 165)]
    public void TheCarrierPitchComesFromTheCoresGender(VoiceGender gender, double expected) =>
        Assert.Equal(expected, GuardianVoice.BasePitchHz(gender));

    [Fact]
    public void NoToggleMeansNoTreatment() =>
        Assert.Null(GuardianVoice.ColourFor(new SpeechSettings(), VoiceGender.Unspecified));

    [Fact]
    public void OneToggleTreatsTheClip()
    {
        var speech = new SpeechSettings { GuardianVoiceCylon = true };

        var colour = GuardianVoice.ColourFor(speech, VoiceGender.Male);

        Assert.NotNull(colour);
        Assert.EndsWith("(guardian)", colour(Tone(220)).Name, StringComparison.Ordinal);
    }
}
