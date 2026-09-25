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

    private static readonly string[] EachTreatment = [.. GuardianVoice.Table.Select(effect => effect.Id)];

    /// <summary>The effects that add to the clip's length.</summary>
    internal static readonly string[] Lengthening =
        ["stutter", "reverseReverb", "shimmer", "reverb", "helmet", "hologram", "respirator"];

    /// <summary>
    /// The effects that put static onto a silent channel of their own accord, rather than leaking it
    /// from another channel, because a comms link has a noise floor whatever the input is.
    /// </summary>
    private static readonly string[] AlwaysAudible = ["helmet", "hologram", "respirator"];

    /// <summary>These effects ticked, in the default order, at default levels.</summary>
    internal static IReadOnlyList<GuardianVoiceEffect> Ticking(params string[] ids) =>
        [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = ids.Contains(effect.Id) })];

    /// <summary>One effect ticked at a level, everything else off at its default.</summary>
    private static IReadOnlyList<GuardianVoiceEffect> TickingAt(string id, int level) =>
        [.. GuardianVoice.Defaults.Select(effect => effect.Id == id ? effect with { Ticked = true, Level = level } : effect)];

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
    internal static AudioClip Voiced(double hertz, double seconds = 1.0, double amplitude = 0.1) =>
        Clip(Enumerable.Range(0, (int)(Rate * seconds))
            .Select(index => Enumerable.Range(1, (int)(4_000 / hertz))
                .Sum(n => Math.Sin(2 * Math.PI * hertz * n * index / Rate) / n) * amplitude)
            .ToArray());

    private static AudioClip WhiteNoise(double seconds = 2.0, double amplitude = 0.2)
    {
        var state = 12345u;

        return Clip(Enumerable.Range(0, (int)(Rate * seconds))
            .Select(_ =>
            {
                state = (state * 1664525u) + 1013904223u;
                return (((state >> 8) / 16777215.0 * 2) - 1) * amplitude;
            })
            .ToArray());
    }

    /// <summary><paramref name="count"/> tone bursts, each <paramref name="burstMs"/> long, separated by silence.</summary>
    private static AudioClip ToneBursts(int count, double hertz, double burstMs, double gapMs)
    {
        var burst = (int)(Rate * burstMs / 1000);
        var gap = (int)(Rate * gapMs / 1000);
        var samples = new double[(count * burst) + ((count - 1) * gap)];

        for (var b = 0; b < count; b++)
        {
            var at = b * (burst + gap);

            for (var index = 0; index < burst; index++)
            {
                samples[at + index] = Math.Sin(2 * Math.PI * hertz * index / Rate) * 0.3;
            }
        }

        return Clip(samples);
    }

    /// <summary>The magnitude-weighted mean frequency across the given bins.</summary>
    private static double Centroid(double[] samples, IEnumerable<double> bins)
    {
        var weighted = 0.0;
        var total = 0.0;

        foreach (var hertz in bins)
        {
            var magnitude = Magnitude(samples, hertz);
            weighted += hertz * magnitude;
            total += magnitude;
        }

        return total > 0 ? weighted / total : 0;
    }

    internal static double[] Samples(AudioClip clip)
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

    internal static double Rms(ReadOnlySpan<double> samples)
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

        Assert.Same(line, GuardianVoice.Apply(line, GuardianVoice.Defaults, BasePitch));
    }

    [Fact]
    public void AnEmptyClipIsHandedBackByEveryTreatment()
    {
        var empty = new AudioClip("nothing", ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);

        foreach (var treatment in EachTreatment)
        {
            Assert.Same(empty, GuardianVoice.Apply(empty, Ticking(treatment), BasePitch));
        }
    }

    [Fact]
    public void PitchDownLowersAToneFourSemitonesAndKeepsItsLength()
    {
        var line = Tone(440);
        var treated = GuardianVoice.Apply(line, Ticking("pitchDown"), BasePitch);

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
        var treated = GuardianVoice.Apply(line, Ticking("octaveDown"), BasePitch);

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
        var treated = Middle(GuardianVoice.Apply(line, Ticking("ringMod"), BasePitch));
        var carrier = Magnitude(treated, 1_000);

        Assert.True(Magnitude(Middle(line), 955) < Magnitude(Middle(line), 1_000) * 0.05);
        Assert.True(Magnitude(treated, 955) > carrier * 0.15, "no lower sideband at 955 Hz");
        Assert.True(Magnitude(treated, 1_045) > carrier * 0.15, "no upper sideband at 1045 Hz");
        Assert.True(Magnitude(treated, 910) < carrier * 0.05, "energy at 910 Hz, where there should be none");
    }

    [Fact]
    public void CombPutsPeaksInNoiseEveryHundredAndElevenHertz()
    {
        var treated = Samples(GuardianVoice.Apply(WhiteNoise(), Ticking("comb"), BasePitch));
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
            var treated = Middle(GuardianVoice.Apply(Voiced(pitch), Ticking("cylon"), BasePitch));
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
        var treated = GuardianVoice.Apply(line, Ticking("reverb"), BasePitch);

        Assert.Equal(line.Pcm.Length + (Rate / 2 * 2), treated.Pcm.Length);

        var samples = Samples(treated);
        var tail = samples.AsSpan(samples.Length - (Rate / 2), Rate / 4);

        Assert.True(Rms(tail) > 0.001, $"the tail is at {Rms(tail):F4} RMS");
        Assert.Equal(0, samples[^1]);
    }

    [Fact]
    public void DeepRingModPutsSidebandsThirtyHertzEitherSideWellAboveTheCarrier()
    {
        var treated = Middle(GuardianVoice.Apply(Tone(1_000), Ticking("deepRingMod"), BasePitch));

        var carrier = Magnitude(treated, 1_000);
        var lower = Magnitude(treated, 970);
        var upper = Magnitude(treated, 1_030);

        Assert.True(lower > carrier * 31.6, $"970 Hz is only {20 * Math.Log10(lower / carrier):F1} dB above 1 kHz");
        Assert.True(upper > carrier * 31.6, $"1030 Hz is only {20 * Math.Log10(upper / carrier):F1} dB above 1 kHz");
    }

    [Fact]
    public void OverdriveAddsAThirdHarmonic()
    {
        var line = Tone(440);
        var dry = Middle(line);
        var treated = Middle(GuardianVoice.Apply(line, Ticking("overdrive"), BasePitch));

        Assert.True(Magnitude(dry, 1_320) < Magnitude(dry, 440) * 0.02, "the dry tone already had a 1320 Hz component");
        Assert.True(
            Magnitude(treated, 1_320) > Magnitude(dry, 440) * 0.02,
            $"1320 Hz came out at {Magnitude(treated, 1_320):F5}, no louder than the dry tone's noise floor");
    }

    [Fact]
    public void TremoloMakesRmsRiseAndFallSixTimesASecond()
    {
        var treated = Samples(GuardianVoice.Apply(Tone(1_000, seconds: 1.0), Ticking("tremolo"), BasePitch));
        var windowSamples = (int)(0.02 * Rate);
        var windows = treated.Length / windowSamples;

        var rms = Enumerable.Range(0, windows)
            .Select(w => Rms(treated.AsSpan(w * windowSamples, windowSamples)))
            .ToArray();

        var minima = 0;

        for (var index = 1; index < rms.Length - 1; index++)
        {
            if (rms[index] < rms[index - 1] && rms[index] < rms[index + 1])
            {
                minima++;
            }
        }

        Assert.InRange(minima, 5, 7);
    }

    [Fact]
    public void BitcrusherHoldsSixSampleRunsWithAtMostThirtyTwoDistinctValues()
    {
        var treated = Samples(GuardianVoice.Apply(Tone(2, seconds: 1.0, amplitude: 0.9), Ticking("bitcrusher"), BasePitch));

        Assert.True(treated.Distinct().Count() <= 32, $"{treated.Distinct().Count()} distinct values came out");

        var runStart = 0;

        for (var index = 1; index <= treated.Length; index++)
        {
            if (index == treated.Length || treated[index] != treated[runStart])
            {
                Assert.True((index - runStart) % 6 == 0, $"a run of {index - runStart} samples starting at {runStart}");
                runStart = index;
            }
        }
    }

    [Theory]
    [InlineData("flanger")]
    [InlineData("phaser")]
    public void SweepsMoveTheNotchOverASecond(string treatment)
    {
        var treated = Samples(GuardianVoice.Apply(WhiteNoise(seconds: 2.0), Ticking(treatment), BasePitch));
        var candidates = Enumerable.Range(4, 56).Select(step => step * 50.0).ToArray();

        var early = treated.AsSpan(0, (int)(0.1 * Rate)).ToArray();
        var late = treated.AsSpan(Rate, (int)(0.1 * Rate)).ToArray();

        var earlyNotch = candidates.MinBy(hertz => Magnitude(early, hertz));
        var lateNotch = candidates.MinBy(hertz => Magnitude(late, hertz));

        Assert.NotEqual(earlyNotch, lateNotch);
    }

    [Fact]
    public void WahsCentreFollowsLoudness()
    {
        var bins = Enumerable.Range(4, 19).Select(step => step * 100.0).ToArray();

        var loud = Middle(GuardianVoice.Apply(WhiteNoise(seconds: 1.0, amplitude: 0.8), Ticking("wah"), BasePitch));
        var quiet = Middle(GuardianVoice.Apply(WhiteNoise(seconds: 1.0, amplitude: 0.008), Ticking("wah"), BasePitch));

        Assert.True(
            Centroid(loud, bins) > Centroid(quiet, bins),
            $"the loud burst centred at {Centroid(loud, bins):F0} Hz, the quiet one at {Centroid(quiet, bins):F0} Hz");
    }

    [Fact]
    public void EveryTreatmentButStutterAndTheReverbsKeepsTheLength()
    {
        var line = Voiced(150, seconds: 2.0);

        foreach (var treatment in EachTreatment.Where(t => !Lengthening.Contains(t)))
        {
            Assert.Equal(line.Pcm.Length, GuardianVoice.Apply(line, Ticking(treatment), BasePitch).Pcm.Length);
        }
    }

    [Fact]
    public void TheSameLineTreatedTwiceIsTheSameBytes()
    {
        foreach (var treatment in EachTreatment.Select(id => Ticking(id)).Append(Ticking(EachTreatment)))
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
        var treated = GuardianVoice.Apply(line, Ticking("glitch"), BasePitch);

        Assert.NotEqual(line.Pcm.ToArray(), treated.Pcm.ToArray());
    }

    [Fact]
    public void StutterAtFullChanceChangesTheLine()
    {
        var line = ToneBursts(3, 440, burstMs: 300, gapMs: 200);
        var treated = GuardianVoice.Apply(line, TickingAt("stutter", GuardianVoice.HighestLevel), BasePitch);

        Assert.NotEqual(line.Pcm.ToArray(), treated.Pcm.Span[..line.Pcm.Length].ToArray());
    }

    /// <summary>
    /// A repeated word start adds (repeats − 1) × the 60–120 ms burst, less a 10 ms crossfade at each of
    /// the <c>repeats</c> joins it makes.
    /// </summary>
    private static (double Min, double Max) StutterAdditionRange(int onsets) =>
        (onsets * (((2 - 1) * 0.060 * Rate) - (2 * 0.010 * Rate)),
         onsets * (((3 - 1) * 0.120 * Rate) - (3 * 0.010 * Rate)));

    [Fact]
    public void StutterAtFullChanceLengthensThreeSeparateBursts()
    {
        var line = ToneBursts(3, 440, burstMs: 300, gapMs: 200);
        var treated = GuardianVoice.Apply(line, TickingAt("stutter", GuardianVoice.HighestLevel), BasePitch);

        Assert.True(treated.Pcm.Length > line.Pcm.Length, "the repeats should have lengthened the clip");

        var addedSamples = (treated.Pcm.Length - line.Pcm.Length) / 2;
        var (min, max) = StutterAdditionRange(onsets: 3);

        // Generous margin either side for the crossfades the three word-to-word joins add on top.
        Assert.InRange(addedSamples, min - (3 * 0.010 * Rate), max);
    }

    [Fact]
    public void OneContinuousToneStuttersOnlyOnceAtFullChance()
    {
        var line = Tone(440, seconds: 1.0);
        var treated = GuardianVoice.Apply(line, TickingAt("stutter", GuardianVoice.HighestLevel), BasePitch);

        var addedSamples = (treated.Pcm.Length - line.Pcm.Length) / 2;
        var (min, max) = StutterAdditionRange(onsets: 1);

        Assert.InRange(addedSamples, min, max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(20)]
    public void StutterKeepsTheLineWithinADecibelOfItsLoudness(int level)
    {
        var line = Voiced(150, seconds: 3.0);
        var dry = Samples(line);

        var wet = Samples(GuardianVoice.Apply(line, TickingAt("stutter", level), BasePitch)).AsSpan(0, dry.Length);
        var decibels = 20 * Math.Log10(Rms(wet) / Rms(dry));

        Assert.True(Math.Abs(decibels) < 1, $"level {level} moved the level {decibels:F2} dB");
    }

    [Fact]
    public void EachTreatmentKeepsTheLineWithinADecibelOfItsLoudness()
    {
        var line = Voiced(150, seconds: 3.0);
        var dry = Samples(line);

        foreach (var treatment in EachTreatment)
        {
            var wet = Samples(GuardianVoice.Apply(line, Ticking(treatment), BasePitch)).AsSpan(0, dry.Length);
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
            var span = GuardianVoice.Apply(stereo, Ticking(treatment), BasePitch).Pcm.Span;
            var left = 0;
            var right = 0;

            for (var frame = 0; frame < span.Length / 4; frame++)
            {
                left = Math.Max(left, Math.Abs((int)(short)(span[frame * 4] | (span[(frame * 4) + 1] << 8))));
                right = Math.Max(right, Math.Abs((int)(short)(span[(frame * 4) + 2] | (span[(frame * 4) + 3] << 8))));
            }

            Assert.True(left > 1_000, $"{treatment} silenced the left channel");

            if (!AlwaysAudible.Contains(treatment))
            {
                Assert.True(right < 32, $"{treatment} put {right} into the silent channel");
            }
        }
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
        var speech = new SpeechSettings { GuardianVoice = new GuardianVoiceSettings { Effects = Ticking("cylon") } };

        var colour = GuardianVoice.ColourFor(speech, VoiceGender.Male);

        Assert.NotNull(colour);
        Assert.EndsWith("(guardian)", colour(Tone(220)).Name, StringComparison.Ordinal);
    }
}
