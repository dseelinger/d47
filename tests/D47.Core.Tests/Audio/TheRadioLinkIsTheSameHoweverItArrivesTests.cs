using D47.Core.Audio;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The radio link as a running filter gives the same bytes however its input is cut, at a level its gain control sets.</summary>
public class TheRadioLinkIsTheSameHoweverItArrivesTests
{
    private const int Rate = 48_000;

    private static readonly AudioClip Speech = PcmConverter.ToStandard(StandInVoice.Clip);

    private static byte[] Whole(IPcmFilter filter, ReadOnlySpan<byte> pcm) => [.. filter.Push(pcm), .. filter.Finish()];

    /// <summary>Chunks of 1 to 4,999 bytes, odd sizes included, so samples are split across pushes.</summary>
    private static IEnumerable<int> RandomCuts(int length, int seed)
    {
        var random = new Random(seed);

        for (var at = 0; at < length;)
        {
            var size = Math.Min(length - at, random.Next(1, 5_000));
            yield return size;
            at += size;
        }
    }

    public static TheoryData<int, double, bool> Links => new()
    {
        { 1, 1.0, false },
        { 785, 0.2, false },
        { 2026, 0.6, true },
    };

    [Theory]
    [MemberData(nameof(Links))]
    public void AClipFedInRandomChunksIsByteIdenticalToTheClipFedWhole(int seed, double strength, bool overheard)
    {
        var pcm = Speech.Pcm.ToArray();
        var chunked = RadioVoice.Filter(strength, overheard);
        var output = new List<byte>();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, seed))
        {
            output.AddRange(chunked.Push(pcm.AsSpan(at, size)));
            at += size;
        }

        output.AddRange(chunked.Finish());

        Assert.Equal(Whole(RadioVoice.Filter(strength, overheard), pcm), output.ToArray());
    }

    [Theory]
    [MemberData(nameof(Links))]
    public void ApplyIsTheFilterOverTheWholeClip(int seed, double strength, bool overheard)
    {
        _ = seed;

        Assert.Equal(
            Whole(RadioVoice.Filter(strength, overheard), Speech.Pcm.Span),
            RadioVoice.Apply(Speech, strength, overheard).Pcm.ToArray());
    }

    [Fact]
    public void ApplyIsTheDryLengthPlusAFifthOfASecondTail() =>
        Assert.Equal(Speech.Pcm.Length + (Rate / 5 * 2), RadioVoice.Apply(Speech, 0.4).Pcm.Length);

    [Fact]
    public async Task AnArrivingClipThroughTheFilterEndsAsApplyWouldHaveIt()
    {
        var source = new ArrivingClip("line");
        var treated = source.Through(RadioVoice.Filter(0.5, overheard: false));
        var pcm = Speech.Pcm.ToArray();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, 47))
        {
            source.Append(pcm.AsSpan(at, size));
            at += size;
        }

        Assert.False(treated.IsComplete);

        source.Complete();

        Assert.Equal(RadioVoice.Apply(Speech, 0.5).Pcm.ToArray(), (await treated.Whole).Pcm.ToArray());
    }

    [Fact]
    public void ARoleHasARunningLinkExactlyWhereItHasALink()
    {
        foreach (var role in Enum.GetValues<VoiceRole>())
        {
            foreach (var overheard in new[] { false, true })
            {
                Assert.Equal(RadioVoice.Colours(role, 0.5, overheard) is null, RadioVoice.RunningColours(role, 0.5, overheard) is null);
            }
        }
    }

    [Fact]
    public void ACastMemberOnALinkWithNoEffectsRunsAFilter()
    {
        Assert.NotNull(CastVoice.Running(new PinnedVoice("kokoro", "bm_george") { Link = 0.5 }));
        Assert.Null(CastVoice.Running(new PinnedVoice("kokoro", "bm_george")));
    }

    /// <summary>
    /// Speech clips of different loudness: the Kokoro stand-in at four levels 18 dB apart, and four LibriTTS-R clips
    /// bundled as Chatterbox reference voices.
    /// </summary>
    public static TheoryData<string, double> Clips => new()
    {
        { "stand-in", 0.25 },
        { "stand-in", 0.5 },
        { "stand-in", 1.0 },
        { "stand-in", 2.0 },
        { "alder", 1.0 },
        { "isolde", 1.0 },
        { "marlow", 1.0 },
        { "verity", 0.5 },
    };

    private static AudioClip Load(string name, double scale)
    {
        AudioClip clip;

        if (name == "stand-in")
        {
            clip = Speech;
        }
        else
        {
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox", $"{name}.wav"));
            clip = WavReader.ReadStandard(stream, name);
        }

        var span = clip.Pcm.Span;
        var pcm = new byte[span.Length];

        for (var at = 0; at + 1 < span.Length; at += 2)
        {
            var sample = (short)(span[at] | (span[at + 1] << 8)) * scale;
            var value = (short)Math.Clamp(Math.Round(sample), short.MinValue, short.MaxValue);

            pcm[at] = (byte)(value & 0xFF);
            pcm[at + 1] = (byte)((value >> 8) & 0xFF);
        }

        return clip with { Pcm = pcm };
    }

    private static double Rms(ReadOnlySpan<byte> pcm, int samples)
    {
        var sum = 0.0;

        for (var index = 0; index < samples; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples);
    }

    [Theory]
    [MemberData(nameof(Clips))]
    public void EachClipArrivesWithinTwoDecibelsOfTheTarget(string name, double scale)
    {
        var clip = Load(name, scale);
        var treated = RadioVoice.Apply(clip);
        var level = 20 * Math.Log10(Rms(treated.Pcm.Span, clip.Pcm.Length / 2) / RadioVoice.Target);

        Assert.InRange(level, -2, 2);
    }
}
