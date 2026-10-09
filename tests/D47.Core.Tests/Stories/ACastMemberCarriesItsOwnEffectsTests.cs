using D47.Core.Audio;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class ACastMemberCarriesItsOwnEffectsTests
{
    private static readonly StorySpeaker Plain = StoryFixtures.Secret.Cast[0];

    private static readonly StorySpeaker Static = Plain with
    {
        Link = 0.05,
        Effects = [new("glitch", 20)],
    };

    private static AudioClip Voice(params int[] hertz)
    {
        var format = AudioFormat.Standard;
        var samples = format.SampleRate;
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var sum = hertz.Sum(f => Math.Sin(2 * Math.PI * f * index / format.SampleRate));
            var value = (short)(sum * 0.2 * short.MaxValue);
            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("voice", pcm, format);
    }

    private static double Magnitude(AudioClip clip, int hertz)
    {
        var pcm = clip.Pcm.Span;
        var coefficient = 2 * Math.Cos(2 * Math.PI * hertz / clip.Format.SampleRate);
        double previous = 0, older = 0;

        for (var index = 0; index < pcm.Length / 2; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
            var current = sample + (coefficient * previous) - older;
            older = previous;
            previous = current;
        }

        return Math.Sqrt((previous * previous) + (older * older) - (coefficient * previous * older));
    }

    [Fact]
    public void ASpeakerWithNeitherFieldHasNoTreatment() =>
        Assert.Null(CastVoice.Treatment(Plain));

    [Fact]
    public void ALinkAloneBandLimitsTheVoice()
    {
        var heard = CastVoice.Treatment(Plain with { Link = 0.3 })!(Voice(1_000, 5_000));
        var below = 20 * Math.Log10(Magnitude(heard, 1_000) / Magnitude(heard, 5_000));

        Assert.True(below >= 14, $"5 kHz was only {below:0.0} dB below 1 kHz");
    }

    [Fact]
    public void EffectsAddToTheLinkRatherThanReplaceIt()
    {
        var voice = Voice(1_000);
        var linkOnly = RadioVoice.Apply(voice, 0.05);
        var heard = CastVoice.Treatment(Static)!(voice);

        Assert.NotEqual(linkOnly.Pcm.ToArray(), heard.Pcm.ToArray());
    }

    [Fact]
    public void ASpeakerSoundsTheSameEveryTimeWhateverTheCommanderTicked()
    {
        var first = CastVoice.Treatment(Static)!(Voice(1_000));
        var second = CastVoice.Treatment(Static)!(Voice(1_000));

        Assert.Equal(first.Pcm.ToArray(), second.Pcm.ToArray());
    }
}
