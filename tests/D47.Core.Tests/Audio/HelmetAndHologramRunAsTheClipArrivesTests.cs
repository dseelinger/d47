using System.Security.Cryptography;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#790: Helmet and Hologram run the radio link as the clip arrives.</summary>
public class HelmetAndHologramRunAsTheClipArrivesTests
{
    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    public static TheoryData<string, int> EachEffectAtEachLevel => [.. (
        from id in new[] { "helmet", "hologram" }
        from level in new[] { GuardianVoice.LowestLevel, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoice.HighestLevel }
        select (id, level)).Distinct()];

    private static IGuardianStage Start(string id, int level)
    {
        var effect = GuardianVoice.Find(id)!;

        return effect.Start(effect.Value(level), 200, Line.Format.SampleRate);
    }

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
        var random = new Random(790 + level);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(0, 3_000));
            piecewise.Push(dry.AsSpan(at, size), chunked);
            at += size;
        }

        piecewise.Finish(chunked);

        Assert.NotEmpty(whole);
        Assert.Equal(whole, chunked);
    }

    [Fact]
    public void HelmetOpensWithItsClickBeforeAnyInputArrives()
    {
        var output = new List<double>();

        Start("helmet", GuardianVoice.Find("helmet")!.DefaultLevel).Push([], output);

        Assert.NotEmpty(output);
    }

    [Theory]
    [InlineData("helmet", "80A2FC98D1BC8F233B53D75E391C12DDA6F965BBF9C16A197E1DC58D74C90EBF")]
    [InlineData("hologram", "5F81F02A5DBF2C7859337FC89F3D38D1C3048E0464E3F80611F343F0C6B03532")]
    public void AtDefaultLevelTheStageOutputIsWhatTheWholeClipFunctionGave(string id, string sha256)
    {
        var output = new List<double>();
        var stage = Start(id, GuardianVoice.Find(id)!.DefaultLevel);

        stage.Push(GuardianVoiceTests.Samples(Line), output);
        stage.Finish(output);

        var bytes = output.SelectMany(sample => BitConverter.GetBytes(sample)).ToArray();

        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
