using System.Security.Cryptography;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#791: the effects that measure against the clip's peak run on a clip as it arrives, holding back 300 ms.</summary>
public class BitcrusherMonotoneAndSteppedPitchRunAsTheClipArrivesTests
{
    private const double BasePitch = 200;
    private const int Rate = 48_000;
    private const int PeakWindow = Rate * 300 / 1000;

    // 150 Hz divides the rate into whole periods, so the clip's loudest sample falls in its first period.
    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    private static readonly string[] Effects = ["bitcrusher", "monotone", "steppedPitch"];

    public static TheoryData<string, int> EachEffectAtEachLevel => [.. (
        from id in Effects
        from level in new[] { GuardianVoice.LowestLevel, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoice.HighestLevel }
        select (id, level)).Distinct()];

    private static IGuardianStage Start(string id, int level)
    {
        var effect = GuardianVoice.Find(id)!;

        return effect.Start(effect.Value(level), BasePitch, Rate);
    }

    private static List<double> Whole(string id, int level, double[] dry)
    {
        var output = new List<double>();
        var stage = Start(id, level);

        stage.Push(dry, output);
        stage.Finish(output);
        return output;
    }

    [Fact]
    public void TheLineBuiltForThePinsPeaksInItsFirstThreeHundredMilliseconds()
    {
        var dry = GuardianVoiceTests.Samples(Line);
        var loudest = dry.Max(Math.Abs);

        Assert.Contains(dry.Take(PeakWindow), sample => Math.Abs(sample) == loudest);
    }

    [Theory]
    [MemberData(nameof(EachEffectAtEachLevel))]
    public void FedInRandomChunksTheOutputMatchesOneChunk(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(Line);
        var whole = Whole(id, level, dry);

        var chunked = new List<double>();
        var piecewise = Start(id, level);
        var random = new Random(791 + level);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(0, 3_000));
            piecewise.Push(dry.AsSpan(at, size), chunked);
            at += size;
        }

        piecewise.Finish(chunked);

        Assert.Equal(dry.Length, whole.Count);
        Assert.Equal(whole, chunked);
    }

    [Theory]
    [InlineData("bitcrusher", "E7A32B79629F54EDA18650C4AC179BD818158B71C713C6E16749A142201C8DD9")]
    [InlineData("monotone", "6E6349CEB7E35B564EB21822EACB275CC8EAF01A58FBABBC7B511BB9B9F593DD")]
    [InlineData("steppedPitch", "8B2546F4AD87682AA4001BBFFACD67FAE844E4C2088C73F30D00B8E4EBD4A335")]
    public void AtDefaultLevelTheStageOutputIsWhatTheWholeClipFunctionGave(string id, string sha256)
    {
        var output = Whole(id, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoiceTests.Samples(Line));
        var bytes = output.SelectMany(sample => BitConverter.GetBytes(sample)).ToArray();

        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    [Theory]
    [InlineData("bitcrusher", 0)]
    [InlineData("monotone", Rate / 10)]
    [InlineData("steppedPitch", Rate / 10)]
    public void OneSampleAtATimeNoStageHoldsBackMoreThanThreeHundredMillisecondsAndItsLookAhead(string id, int lookAhead)
    {
        var dry = GuardianVoiceTests.Samples(Line);
        var output = new List<double>();
        var stage = Start(id, GuardianVoice.Find(id)!.DefaultLevel);
        var held = 0;

        for (var at = 0; at < dry.Length; at++)
        {
            stage.Push(dry.AsSpan(at, 1), output);
            held = Math.Max(held, at + 1 - output.Count);
        }

        Assert.True(held <= PeakWindow + lookAhead, $"{id} held back {held} samples of {PeakWindow + lookAhead}");
        Assert.True(held >= PeakWindow, $"{id} held back only {held} samples");
    }

    [Fact]
    public void ALouderSampleAfterTheWindowChangesNothingBeforeIt()
    {
        var quiet = GuardianVoiceTests.Samples(Line);
        var loud = (double[])quiet.Clone();
        var burst = Rate;

        for (var index = burst; index < burst + 480; index++)
        {
            loud[index] = index % 2 == 0 ? 0.9 : -0.9;
        }

        var level = GuardianVoice.Find("bitcrusher")!.DefaultLevel;
        var before = Whole("bitcrusher", level, quiet);
        var after = Whole("bitcrusher", level, loud);

        Assert.Equal(before.Take(burst), after.Take(burst));
        Assert.NotEqual(before.Skip(burst).Take(2_000), after.Skip(burst).Take(2_000));
    }
}
