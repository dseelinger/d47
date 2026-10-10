using System.Security.Cryptography;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#789: the effects that work on Hann frames run on a clip as it arrives, holding back only their look-ahead.</summary>
public class SixGuardianFrameEffectsRunAsTheClipArrivesTests
{
    private const double BasePitch = 200;
    private const int Frame = 2048;

    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    private static readonly string[] Effects = ["cylon", "whisper", "pitchDown", "octaveDown", "hive", "shimmer"];

    public static TheoryData<string, int> EachEffectAtEachLevel => [.. (
        from id in Effects
        from level in new[] { GuardianVoice.LowestLevel, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoice.HighestLevel }
        select (id, level)).Distinct()];

    private static IGuardianStage Start(string id, int level)
    {
        var effect = GuardianVoice.Find(id)!;

        return effect.Start(effect.Value(level), BasePitch, Line.Format.SampleRate);
    }

    private static double Factor(string id, int level) =>
        Math.Pow(2, GuardianVoice.Find(id)!.Value(level) / 12);

    [Theory]
    [MemberData(nameof(EachEffectAtEachLevel))]
    public void FedInRandomChunksTheOutputMatchesOneChunk(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(Line);

        var whole = new List<double>();
        var once = Start(id, level);
        once.Push(dry, whole);
        once.Finish(whole);

        var chunked = new List<double>();
        var piecewise = Start(id, level);
        var random = new Random(789 + level);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(0, 3_000));
            piecewise.Push(dry.AsSpan(at, size), chunked);
            at += size;
        }

        piecewise.Finish(chunked);

        Assert.Equal(dry.Length + (id == "shimmer" ? (int)Math.Round(0.5 * Line.Format.SampleRate) : 0), whole.Count);
        Assert.Equal(whole, chunked);
    }

    [Theory]
    [InlineData("cylon", "E5C40A8132EA6E4730FB2BB99EAA2DE0C2A585B791BFB46FED735A421988F361")]
    [InlineData("whisper", "5D8A1E8455182EA50EB08B9740B8F76F1332FB306B851C6689D8F222910DEAB3")]
    [InlineData("pitchDown", "C194D0FC09A342C1A40C5167379DC0F2454BCCD1B99AA00181BA872DAE439D4F")]
    [InlineData("octaveDown", "61B057E1B540443DCC88D502543FFD0305D5B86D18513315D35F153F03CF0935")]
    [InlineData("hive", "91836394715A92EDEF2972613B9C6690D9C9859A345D08365B0F130E6CB559CE")]
    [InlineData("shimmer", "A0FBA8169A4D3222EEEEF0AC69053680DF4685A56D6DF166BBE10F7B5B0F0F60")]
    public void AtDefaultLevelTheStageOutputIsWhatTheWholeClipFunctionGave(string id, string sha256)
    {
        var output = new List<double>();
        var stage = Start(id, GuardianVoice.Find(id)!.DefaultLevel);

        stage.Push(GuardianVoiceTests.Samples(Line), output);
        stage.Finish(output);

        var bytes = output.SelectMany(sample => BitConverter.GetBytes(sample)).ToArray();

        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Whisper is held to the end of the clip, where the running leveller gives it the whole-clip gain.</summary>
    [Theory]
    [InlineData("cylon")]
    [InlineData("pitchDown")]
    [InlineData("octaveDown")]
    [InlineData("hive")]
    [InlineData("shimmer")]
    public void OneSampleAtATimeNoStageHoldsBackMoreThanItsLookAheadAndAnAnalysisHop(string id)
    {
        var level = GuardianVoice.Find(id)!.DefaultLevel;
        var dry = GuardianVoiceTests.Samples(GuardianVoiceTests.Voiced(150, seconds: 0.5));
        var output = new List<double>();
        var stage = Start(id, level);
        var held = 0;

        for (var at = 0; at < dry.Length; at++)
        {
            stage.Push(dry.AsSpan(at, 1), output);
            held = Math.Max(held, at + 1 - output.Count);
        }

        Assert.True(held <= Allowed(id, level), $"{id} held back {held} samples of {Allowed(id, level)}");
        Assert.True(held > 0, $"{id} held back nothing, so it was not measured");
    }

    /// <summary>The look-ahead in samples at 48 kHz plus one analysis hop.</summary>
    private static int Allowed(string id, int level)
    {
        static double Shift(double factor) => (Frame / 2.0) + (Frame / 2.0 / factor);
        static double Hop(double factor) => Math.Round(Frame / 4 / factor);

        return (int)Math.Ceiling(id switch
        {
            "cylon" => Frame + (Frame / 4.0),
            "pitchDown" => Shift(Factor(id, level)) + Hop(Factor(id, level)),
            "octaveDown" => Shift(0.5) + Hop(0.5),
            "hive" => new[] { (-5.0, 13.0), (-2.0, 23.0), (3.0, 37.0) }
                .Max(copy => Shift(Math.Pow(2, copy.Item1 / 12)) - (copy.Item2 * 48) + Hop(Math.Pow(2, copy.Item1 / 12))),
            "shimmer" => Shift(2) + Hop(2),
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        });
    }
}
