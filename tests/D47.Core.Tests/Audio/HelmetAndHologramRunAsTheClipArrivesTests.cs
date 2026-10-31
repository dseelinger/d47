using System.Security.Cryptography;
using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#790: Helmet and Hologram run the radio link as the clip arrives.</summary>
public class HelmetAndHologramRunAsTheClipArrivesTests
{
    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    public static TheoryData<string, int> EachEffectAtEachLevel => GuardianStages.EachLevel("helmet", "hologram");

    private static IGuardianStage Start(string id, int level) =>
        GuardianStages.Start(id, level, 200, Line.Format.SampleRate);

    [Theory]
    [MemberData(nameof(EachEffectAtEachLevel))]
    public void FedInRandomChunksTheOutputMatchesOneChunk(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(Line);

        var (whole, chunked) = GuardianStages.WholeAndChunked(id, level, dry, 790 + level, 3_000);

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
