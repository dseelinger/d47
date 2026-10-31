using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;
using static D47.Core.Tests.Audio.GuardianVoiceTests;

namespace D47.Core.Tests.Audio;

/// <summary>#241: Hive, Whisper, Reverse reverb and Shimmer each do what their row says, the same way every run.</summary>
public class HiveWhisperReverseAndShimmerTests
{
    private const int Rate = 48_000;

    private const double BasePitch = 200;

    private static double[] Treated(AudioClip clip, string id, int level) =>
        GuardianVoiceTests.Samples(GuardianVoice.Apply(clip, TickingAt(id, level), BasePitch));

    /// <summary>The geometric mean of the power spectrum over its arithmetic mean, from a direct DFT of the samples.</summary>
    private static double Flatness(ReadOnlySpan<double> samples)
    {
        var logSum = 0.0;
        var sum = 0.0;
        var bins = samples.Length / 2;

        for (var bin = 1; bin < bins; bin++)
        {
            var re = 0.0;
            var im = 0.0;

            for (var index = 0; index < samples.Length; index++)
            {
                var angle = 2 * Math.PI * bin * index / samples.Length;
                re += samples[index] * Math.Cos(angle);
                im -= samples[index] * Math.Sin(angle);
            }

            var power = (re * re) + (im * im) + 1e-20;
            logSum += Math.Log(power);
            sum += power;
        }

        return Math.Exp(logSum / (bins - 1)) / (sum / (bins - 1));
    }

    [Fact]
    public void HiveAddsThreeShiftedVoicesAndKeepsTheNote()
    {
        var dry = GuardianVoiceTests.Samples(Tone(440));
        var wet = Treated(Tone(440), "hive", DefaultLevel("hive"));
        var dryMiddle = dry.AsSpan(Rate / 4, Rate / 2);
        var wetMiddle = wet.AsSpan(Rate / 4, Rate / 2);
        var note = Magnitude(dryMiddle, 440);

        foreach (var hertz in new[] { 329.63, 392.00, 523.25 })
        {
            Assert.True(
                Magnitude(wetMiddle, hertz) > note * 0.2,
                $"{hertz} Hz reached {Magnitude(wetMiddle, hertz) / note:F3} of the note");
            Assert.True(Magnitude(dryMiddle, hertz) < note * 0.02, $"the dry tone already had {hertz} Hz");
        }

        Assert.True(Magnitude(wetMiddle, 440) > note * 0.5, $"440 Hz fell to {Magnitude(wetMiddle, 440) / note:F3}");
    }

    [Fact]
    public void WhisperFlattensATonesSpectrumAndKeepsItsLoudness()
    {
        var dry = GuardianVoiceTests.Samples(Tone(440));
        var wet = Treated(Tone(440), "whisper", DefaultLevel("whisper"));
        var window = Rate / 10;
        var dryFlatness = Flatness(dry.AsSpan(Rate / 2, window));
        var wetFlatness = Flatness(wet.AsSpan(Rate / 2, window));
        var decibels = 20 * Math.Log10(GuardianVoiceTests.Rms(wet) / GuardianVoiceTests.Rms(dry));

        Assert.True(wetFlatness > dryFlatness * 10, $"flatness went from {dryFlatness:E2} to {wetFlatness:E2}");
        Assert.True(Math.Abs(decibels) < 1, $"whisper moved the level {decibels:F2} dB");
    }

    [Fact]
    public void ReverseReverbSwellsIntoTheToneFromSilence()
    {
        var line = Tone(440);
        var dry = GuardianVoiceTests.Samples(line);
        var wet = Treated(line, "reverseReverb", DefaultLevel("reverseReverb"));
        var pad = (int)(0.3 * Rate);

        Assert.Equal(dry.Length + pad, wet.Length);
        Assert.Equal(0, wet[0]);

        var slice = pad / 6;
        var rms = Enumerable.Range(0, 6).Select(k => GuardianVoiceTests.Rms(wet.AsSpan(k * slice, slice))).ToArray();

        for (var k = 1; k < rms.Length; k++)
        {
            Assert.True(rms[k] > rms[k - 1], $"before the onset, slice {k} ({rms[k]:E2}) was not louder than {k - 1} ({rms[k - 1]:E2})");
        }
    }

    [Fact]
    public void ShimmersTailRingsAnOctaveUpAndEndsAtZero()
    {
        var line = Tone(440);
        var dry = GuardianVoiceTests.Samples(line);
        var shimmer = Treated(line, "shimmer", DefaultLevel("shimmer"));
        var reverb = Treated(line, "reverb", DefaultLevel("reverb"));

        Assert.Equal(dry.Length + (Rate / 2), shimmer.Length);
        Assert.Equal(0, shimmer[^1]);

        var tail = shimmer.AsSpan(dry.Length, Rate / 4);
        var plainTail = reverb.AsSpan(dry.Length, Rate / 4);

        Assert.True(
            Magnitude(tail, 880) > Magnitude(plainTail, 880) * 10,
            $"880 Hz in the tail: {Magnitude(tail, 880):E2} with shimmer, {Magnitude(plainTail, 880):E2} without");
        Assert.True(Magnitude(tail, 880) > Magnitude(tail, 440) * 0.1, "the octave is lost under the note in the tail");
    }

    [Theory]
    [InlineData("hive")]
    [InlineData("whisper")]
    [InlineData("reverseReverb")]
    [InlineData("shimmer")]
    public void TheSameLineGivesTheSameBytesAtEveryLevel(string id)
    {
        var line = GuardianVoiceTests.Voiced(150, seconds: 1.0);

        foreach (var level in new[] { GuardianVoice.LowestLevel, DefaultLevel(id), GuardianVoice.HighestLevel })
        {
            Assert.Equal(
                GuardianVoice.Apply(line, TickingAt(id, level), BasePitch).Pcm.ToArray(),
                GuardianVoice.Apply(line, TickingAt(id, level), BasePitch).Pcm.ToArray());
        }
    }

    [Fact]
    public void EachSitsAtItsDefaultPosition()
    {
        var ids = GuardianVoice.Table.Select(effect => effect.Id).ToList();

        Assert.Equal("whisper", ids[ids.IndexOf("cylon") + 1]);
        Assert.Equal("hive", ids[ids.IndexOf("chorus") + 1]);
        Assert.Equal(["reverseReverb", "shimmer", "reverb"], ids[(ids.IndexOf("reverb") - 2)..(ids.IndexOf("reverb") + 1)]);
    }
}
