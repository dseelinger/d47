using System.Security.Cryptography;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#793: stutter measures its onsets against the reference peak, so it runs on a clip as it arrives.</summary>
public class StutterRunsAsTheClipArrivesTests
{
    private const double BasePitch = 200;
    private const int Rate = 48_000;
    private const int PeakWindow = Rate * 300 / 1000;

    // Both lines peak in their first 300 ms, so the reference peak is the whole clip's peak.
    private static readonly AudioClip Bursts = GuardianVoiceTests.ToneBursts(4, 440, burstMs: 300, gapMs: 200);
    private static readonly AudioClip Voice = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    private static IGuardianStage Start(int level)
    {
        var effect = GuardianVoice.Find("stutter")!;

        return effect.Start(effect.Value(level), BasePitch, Rate);
    }

    private static List<double> Whole(int level, double[] dry)
    {
        var output = new List<double>();
        var stage = Start(level);

        stage.Push(dry, output);
        stage.Finish(output);
        return output;
    }

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    public void FedInRandomChunksTheOutputMatchesOneChunk(int level)
    {
        foreach (var clip in new[] { Bursts, Voice })
        {
            var dry = GuardianVoiceTests.Samples(clip);
            var chunked = new List<double>();
            var stage = Start(level);
            var random = new Random(793 + level);

            for (var at = 0; at < dry.Length;)
            {
                var size = Math.Min(dry.Length - at, random.Next(0, 3_000));
                stage.Push(dry.AsSpan(at, size), chunked);
                at += size;
            }

            stage.Finish(chunked);

            Assert.Equal(Whole(level, dry), chunked);
        }
    }

    [Theory]
    [InlineData(7, "053ABB1C1561DD8C74544071D56969CEF3F2FEA3529A8F494F1AC19321A920FB")]
    [InlineData(20, "0A98208692F102CC9870B33A2B46620EB6B26C36B3FEF20018D5A4B91263902B")]
    public void AtTheTestedChancesTheBurstsComeOutAsTheWholeClipFunctionGaveThem(int level, string sha256) =>
        Assert.Equal(sha256, Hash(Whole(level, GuardianVoiceTests.Samples(Bursts))));

    [Theory]
    [InlineData(7, "FA8F8D784E740C8C04E2125866186180DF007B4FB3B829377CDD1C11AF82B5E4")]
    [InlineData(20, "500254117FA3C06E18A36645EB0D2FFA27ABF7C55B01452F3485A9F17F6A7CFC")]
    public void AtTheTestedChancesTheVoiceComesOutAsTheWholeClipFunctionGaveIt(int level, string sha256) =>
        Assert.Equal(sha256, Hash(Whole(level, GuardianVoiceTests.Samples(Voice))));

    [Theory]
    [InlineData(7)]
    [InlineData(20)]
    public void OneSampleAtATimeNoMoreThanThreeHundredMillisecondsIsHeldBack(int level)
    {
        foreach (var clip in new[] { Bursts, Voice })
        {
            var dry = GuardianVoiceTests.Samples(clip);
            var output = new List<double>();
            var stage = Start(level);
            var held = 0;

            for (var at = 0; at < dry.Length; at++)
            {
                stage.Push(dry.AsSpan(at, 1), output);
                held = Math.Max(held, at + 1 - output.Count);
            }

            Assert.InRange(held, PeakWindow - 1, PeakWindow);
        }
    }

    [Fact]
    public void ALouderSampleAfterTheWindowDoesNotMoveAnOnsetBeforeIt()
    {
        var quiet = GuardianVoiceTests.Samples(Bursts);
        var loud = (double[])quiet.Clone();
        var spike = quiet.Length - 2_000;

        loud[spike] = 0.95;

        var before = Whole(7, quiet);
        var after = Whole(7, loud);

        Assert.Equal(before.Take(spike - Rate / 10), after.Take(spike - Rate / 10));
    }

    private static string Hash(List<double> output) =>
        Convert.ToHexString(SHA256.HashData(output.SelectMany(sample => BitConverter.GetBytes(sample)).ToArray()));
}
