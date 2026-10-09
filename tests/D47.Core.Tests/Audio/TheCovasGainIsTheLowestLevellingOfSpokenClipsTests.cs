using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// <see cref="CovasVoice.Gain"/> is measured, not chosen: the lowest whole-clip RMS levelling over the speech clips
/// named here, rounded down, so none of them reaches the ceiling.
/// </summary>
[Trait("Category", "Integration")]
public class TheCovasGainIsTheLowestLevellingOfSpokenClipsTests
{
    private const int Rate = 48_000;

    /// <summary>
    /// The Kokoro-rendered stand-in whole and cut to 1.5 s and 3 s, and the twelve LibriTTS-R speech clips bundled as
    /// Chatterbox reference voices: 1.5 s to 7 s.
    /// </summary>
    public static TheoryData<string> Clips =>
    [
        "stand-in",
        "stand-in, first 1.5 s",
        "stand-in, first 3 s",
        "alder",
        "benedict",
        "corwin",
        "emrys",
        "isolde",
        "linnea",
        "lucan",
        "marlow",
        "odette",
        "tamsin",
        "tobias",
        "verity",
    ];

    private static AudioClip Load(string name)
    {
        if (name.StartsWith("stand-in", StringComparison.Ordinal))
        {
            var whole = PcmConverter.ToStandard(StandInVoice.Clip);
            var seconds = name switch
            {
                "stand-in, first 1.5 s" => 1.5,
                "stand-in, first 3 s" => 3.0,
                _ => double.MaxValue,
            };
            var bytes = (int)Math.Min(whole.Pcm.Length, seconds * Rate * 2);

            return whole with { Pcm = whole.Pcm[..bytes] };
        }

        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox", $"{name}.wav"));
        return WavReader.ReadStandard(stream, name);
    }

    private static double[] Samples(AudioClip clip)
    {
        var span = clip.Pcm.Span;
        var samples = new double[span.Length / 2];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(span[index * 2] | (span[(index * 2) + 1] << 8)) / 32768.0;
        }

        return samples;
    }

    private static double Rms(double[] samples, int count)
    {
        var sum = 0.0;

        for (var index = 0; index < count; index++)
        {
            sum += samples[index] * samples[index];
        }

        return Math.Sqrt(sum / count);
    }

    /// <summary>
    /// The gain that brings this clip's reverb to the dry clip's RMS over the dry clip's length, lowered where the
    /// reverb's peak would pass <see cref="GuardianVoice.Ceiling"/>.
    /// </summary>
    private static double Levelling(AudioClip clip)
    {
        var dry = Samples(clip);
        var wet = CovasVoice.Reverberate(dry, Rate);
        var gain = Rms(dry, dry.Length) / Rms(wet, dry.Length);
        var peak = wet.Max(Math.Abs);

        return peak * gain > GuardianVoice.Ceiling ? GuardianVoice.Ceiling / peak : gain;
    }

    [Fact]
    public void TheGainIsTheLowestLevellingOverTheNamedClips() =>
        Assert.Equal(CovasVoice.Gain, Math.Floor(Clips.Min(row => Levelling(Load(row.Data))) * 1_000) / 1_000);

    [Theory]
    [MemberData(nameof(Clips))]
    public void EachClipKeepsItsLoudnessWithinOneDecibel(string name)
    {
        var clip = Load(name);
        var dry = Samples(clip);
        var treated = Samples(CovasVoice.Apply(clip));

        Assert.InRange(20 * Math.Log10(Rms(treated, dry.Length) / Rms(dry, dry.Length)), -1, 1);
    }

    [Theory]
    [MemberData(nameof(Clips))]
    public void NoSampleReachesTheCeiling(string name) =>
        Assert.True(
            Samples(CovasVoice.Apply(Load(name))).Max(Math.Abs) < GuardianVoice.Ceiling,
            $"{name} reaches {GuardianVoice.Ceiling}");
}
