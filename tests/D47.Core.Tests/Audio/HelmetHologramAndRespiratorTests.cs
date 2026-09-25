using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#242: Helmet, Hologram and Respirator each do what their row says, the same way every run.</summary>
public class HelmetHologramAndRespiratorTests
{
    private const int Rate = 48_000;

    private const double BasePitch = 200;

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

    private static AudioClip Tone(double hertz, double seconds = 1.0) =>
        Clip([.. Enumerable.Range(0, (int)(Rate * seconds)).Select(index => Math.Sin(2 * Math.PI * hertz * index / Rate) * 0.3)]);

    private static double[] Treated(AudioClip clip, string id, int level) =>
        GuardianVoiceTests.Samples(GuardianVoice.Apply(clip, TickingAt(id, level), BasePitch));

    /// <summary>Goertzel.</summary>
    private static double Magnitude(ReadOnlySpan<double> samples, double hertz)
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

    [Fact]
    public void HelmetAttenuatesFarAwayTonesByAtLeastTwentyDecibels()
    {
        var wet = Treated(Tone(1_000), "helmet", DefaultLevel("helmet"));
        var middle = wet.AsSpan(wet.Length / 4, wet.Length / 2);

        var carrier = Magnitude(middle, 1_000);
        var low = Magnitude(middle, 100);
        var high = Magnitude(middle, 5_000);

        Assert.True(20 * Math.Log10(low / carrier) < -20, $"100 Hz came out {20 * Math.Log10(low / carrier):F1} dB below 1 kHz");
        Assert.True(20 * Math.Log10(high / carrier) < -20, $"5 kHz came out {20 * Math.Log10(high / carrier):F1} dB below 1 kHz");
    }

    [Fact]
    public void HelmetAddsTwoClicksAndTheLinkTail()
    {
        var line = Tone(1_000);
        var wet = Treated(line, "helmet", DefaultLevel("helmet"));

        Assert.Equal(line.Pcm.Length / 2 + (int)(0.2 * Rate), wet.Length - (2 * (int)(0.012 * Rate)));
        Assert.NotEqual(0, wet[0]);
        Assert.NotEqual(0, wet[^1]);
    }

    [Fact]
    public void HologramLosesStretchesOfATone()
    {
        var wet = Treated(Tone(1_000, seconds: 6.0), "hologram", DefaultLevel("hologram"));
        var window = (int)(0.09 * Rate);
        var loudest = 0.0;
        var quietest = double.PositiveInfinity;

        for (var start = 0; start + window <= wet.Length; start += window)
        {
            var rms = GuardianVoiceTests.Rms(wet.AsSpan(start, window));
            loudest = Math.Max(loudest, rms);
            quietest = Math.Min(quietest, rms);
        }

        Assert.True(quietest < loudest * 0.45, $"the quietest 90 ms window ({quietest:E2}) never fell far below the loudest ({loudest:E2})");
    }

    [Fact]
    public void RespiratorAddsAHalfSecondBreathThatEndsAtZero()
    {
        var line = Tone(440);
        var dry = GuardianVoiceTests.Samples(line);
        var wet = Treated(line, "respirator", DefaultLevel("respirator"));
        var breathSamples = Rate / 2;

        Assert.Equal(dry.Length + breathSamples, wet.Length);

        var breath = wet.AsSpan(dry.Length, breathSamples);
        Assert.True(GuardianVoiceTests.Rms(breath) > 0.001, $"the breath is at {GuardianVoiceTests.Rms(breath):F4} RMS");
        Assert.Equal(0, wet[^1]);
    }

    [Theory]
    [InlineData("helmet")]
    [InlineData("hologram")]
    [InlineData("respirator")]
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

        Assert.Equal("helmet", ids[ids.IndexOf("glitch") + 1]);
        Assert.Equal("hologram", ids[ids.IndexOf("helmet") + 1]);
        Assert.Equal("respirator", ids[^1]);
    }

    [Fact]
    public void HelmetCommsTicksExactlyHelmet() =>
        Assert.Equal(["helmet"], GuardianPresets.Find("helmet-comms")!.TickedIds);

    [Fact]
    public void RespiratorPresetTicksPitchDownRespiratorAndReverb() =>
        Assert.Equal(["pitchDown", "respirator", "reverb"], GuardianPresets.Find("respirator")!.TickedIds);

    [Fact]
    public void HologramPresetTicksHologramAndGlitch() =>
        Assert.Equal(["hologram", "glitch"], GuardianPresets.Find("hologram")!.TickedIds);
}
