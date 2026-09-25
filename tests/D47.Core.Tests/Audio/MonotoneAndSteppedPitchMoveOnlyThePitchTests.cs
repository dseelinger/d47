using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Monotone and Stepped pitch keep the voice and move only its pitch.</summary>
public class MonotoneAndSteppedPitchMoveOnlyThePitchTests
{
    private const int Rate = 48_000;

    private const double BasePitch = 165;

    private const int Window = Rate * 40 / 1000;

    private const int Hop = Rate * 10 / 1000;

    private static IReadOnlyList<GuardianVoiceEffect> TickingAt(string id, int level) =>
        [.. GuardianVoice.Defaults.Select(effect => effect.Id == id ? effect with { Ticked = true, Level = level } : effect)];

    private static int DefaultLevel(string id) => GuardianVoice.Find(id)!.DefaultLevel;

    private static AudioClip Clip(double[] samples)
    {
        var pcm = new byte[samples.Length * 2];

        for (var index = 0; index < samples.Length; index++)
        {
            var value = (short)Math.Clamp(Math.Round(samples[index] * short.MaxValue), short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("line", pcm, AudioFormat.Standard);
    }

    /// <summary>A sawtooth whose pitch rises linearly from 150 to 250 Hz over one second.</summary>
    internal static AudioClip Glide()
    {
        var samples = new double[Rate];
        var phase = 0.0;

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = ((2 * phase) - 1) * 0.3;
            phase += (150 + (100.0 * index / Rate)) / Rate;
            phase -= Math.Floor(phase);
        }

        return Clip(samples);
    }

    private static AudioClip WhiteNoise()
    {
        var state = 24680u;
        var samples = new double[Rate * 2];

        for (var index = 0; index < samples.Length; index++)
        {
            state = (state * 1664525u) + 1013904223u;
            samples[index] = (((state >> 8) / 16777215.0 * 2) - 1) * 0.2;
        }

        return Clip(samples);
    }

    /// <summary>
    /// The pitch of every 40 ms window lying wholly inside the clip, 10 ms apart, by YIN with parabolic
    /// interpolation; 0 where the window is aperiodic.
    /// </summary>
    private static double[] Pitches(double[] samples)
    {
        var minLag = Rate / 500;
        var maxLag = Rate / 60;
        var width = Window - maxLag - 1;
        var pitches = new List<double>();
        var normalised = new double[maxLag + 2];

        for (var start = 0; start + Window <= samples.Length; start += Hop)
        {
            var running = 0.0;
            normalised[0] = 1;

            for (var lag = 1; lag <= maxLag + 1; lag++)
            {
                var difference = 0.0;

                for (var index = 0; index < width; index++)
                {
                    var delta = samples[start + index] - samples[start + index + lag];
                    difference += delta * delta;
                }

                running += difference;
                normalised[lag] = running > 0 ? difference * lag / running : 1;
            }

            var found = 0;

            for (var lag = minLag; lag <= maxLag; lag++)
            {
                if (normalised[lag] < 0.2)
                {
                    while (lag < maxLag && normalised[lag + 1] < normalised[lag])
                    {
                        lag++;
                    }

                    found = lag;
                    break;
                }
            }

            if (found == 0)
            {
                pitches.Add(0);
                continue;
            }

            var before = normalised[found - 1];
            var after = normalised[found + 1];
            var curve = before - (2 * normalised[found]) + after;

            pitches.Add((double)Rate / (found + (curve > 0 ? (before - after) / (2 * curve) : 0)));
        }

        return [.. pitches];
    }

    private static double[] Treated(AudioClip clip, string id, int level) =>
        GuardianVoiceTests.Samples(GuardianVoice.Apply(clip, TickingAt(id, level), BasePitch));

    [Fact]
    public void MonotoneHoldsAGlideAtTheBasePitch()
    {
        var pitches = Pitches(Treated(Glide(), "monotone", DefaultLevel("monotone")));
        var voiced = pitches.Where(hertz => hertz > 0).ToList();

        Assert.True(voiced.Count >= pitches.Length * 9 / 10, $"only {voiced.Count} of {pitches.Length} windows voiced");
        Assert.All(voiced, hertz => Assert.InRange(hertz, BasePitch - 3, BasePitch + 3));
    }

    [Fact]
    public void SteppedPitchPutsAGlideOnSemitonesHeldAtLeastTheHold()
    {
        var holdMs = GuardianVoice.Find("steppedPitch")!.Value(DefaultLevel("steppedPitch"));
        var pitches = Pitches(Treated(Glide(), "steppedPitch", DefaultLevel("steppedPitch")));
        var semitones = pitches.Select(hertz => hertz > 0 ? 12 * Math.Log2(hertz / 440) : double.NaN).ToArray();
        var notes = semitones.Select(semitone => double.IsNaN(semitone) ? int.MinValue : (int)Math.Round(semitone)).ToArray();

        // A window within 20 ms of a step straddles two pitches; only windows whose neighbours 20 ms either side
        // read the same note are held to the 15 cents.
        var interior = Enumerable.Range(2, notes.Length - 4)
            .Where(index => notes[index] != int.MinValue && notes[index - 2] == notes[index] && notes[index + 2] == notes[index])
            .ToList();

        Assert.True(interior.Count >= notes.Length / 2, $"only {interior.Count} of {notes.Length} windows inside a step");
        Assert.All(interior, index => Assert.InRange(Math.Abs(semitones[index] - notes[index]) * 100, 0, 15));

        // A window straddling a jump can read as aperiodic, so steps are measured across the voiced windows only:
        // from the first window of one note to the first of the next, within one hop either end.
        var voiced = Enumerable.Range(0, notes.Length).Where(index => notes[index] != int.MinValue).ToList();
        var changes = Enumerable.Range(1, voiced.Count - 1)
            .Where(at => notes[voiced[at]] != notes[voiced[at - 1]])
            .Select(at => voiced[at])
            .ToList();

        Assert.True(changes.Count >= 5, $"the glide took only {changes.Count + 1} steps");

        for (var index = 1; index < changes.Count; index++)
        {
            var heldMs = (changes[index] - changes[index - 1]) * 10.0;
            Assert.True(heldMs >= holdMs - 20, $"a step was held {heldMs} ms");
        }
    }

    [Theory]
    [InlineData("monotone")]
    [InlineData("steppedPitch")]
    public void WhiteNoisePassesWithItsLoudnessAndNoPitchImposed(string id)
    {
        var noise = WhiteNoise();
        var dry = GuardianVoiceTests.Samples(noise);
        var wet = Treated(noise, id, DefaultLevel(id));
        var decibels = 20 * Math.Log10(GuardianVoiceTests.Rms(wet) / GuardianVoiceTests.Rms(dry));

        Assert.True(Math.Abs(decibels) < 1, $"{id} moved noise {decibels:F2} dB");
        Assert.All(Pitches(wet), hertz => Assert.Equal(0, hertz));
    }

    [Theory]
    [InlineData("monotone", 1)]
    [InlineData("monotone", 20)]
    [InlineData("steppedPitch", 1)]
    [InlineData("steppedPitch", 12)]
    [InlineData("steppedPitch", 20)]
    public void TheGlideKeepsItsLengthItsLoudnessAndItsBytesFromRunToRun(string id, int level)
    {
        var glide = Glide();
        var dry = GuardianVoiceTests.Samples(glide);
        var first = GuardianVoice.Apply(glide, TickingAt(id, level), BasePitch);
        var second = GuardianVoice.Apply(glide, TickingAt(id, level), BasePitch);
        var decibels = 20 * Math.Log10(GuardianVoiceTests.Rms(GuardianVoiceTests.Samples(first)) / GuardianVoiceTests.Rms(dry));

        Assert.Equal(glide.Pcm.Length, first.Pcm.Length);
        Assert.Equal(first.Pcm.ToArray(), second.Pcm.ToArray());
        Assert.True(Math.Abs(decibels) < 1, $"{id} at {level} moved the level {decibels:F2} dB");
    }

    [Fact]
    public void WithBothTickedMonotoneWinsAndSteppedPitchIsSkipped()
    {
        var glide = Glide();
        var both = GuardianVoice.Defaults
            .Select(effect => effect.Id is "monotone" or "steppedPitch" ? effect with { Ticked = true } : effect)
            .ToList();

        Assert.Equal(
            GuardianVoice.Apply(glide, TickingAt("monotone", DefaultLevel("monotone")), BasePitch).Pcm.ToArray(),
            GuardianVoice.Apply(glide, both, BasePitch).Pcm.ToArray());
    }

    [Fact]
    public void BothSitDirectlyBeforeCylonByDefault()
    {
        var ids = GuardianVoice.Table.Select(effect => effect.Id).ToList();
        var cylon = ids.IndexOf("cylon");

        Assert.Equal(["monotone", "steppedPitch"], ids.GetRange(cylon - 2, 2));
    }
}
