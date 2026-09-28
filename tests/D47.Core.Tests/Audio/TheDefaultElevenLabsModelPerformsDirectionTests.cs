using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>v4 Turbo, the default, performs bracketed direction and takes no speaking rate (#628).</summary>
public class TheDefaultElevenLabsModelPerformsDirectionTests
{
    [Fact]
    public void NobodyChoosingMeansV4Turbo() =>
        Assert.Equal("eleven_v4_turbo", ElevenLabsModels.Named(null));

    [Fact]
    public void ItReadsTagsAndIsHandedTextInGroups()
    {
        Assert.True(ElevenLabsModels.ReadsTags(ElevenLabsModels.V4Turbo));
        Assert.Equal(300, ElevenLabsModels.GroupsSentencesUpTo(ElevenLabsModels.V4Turbo));
    }

    [Fact]
    public void ItIsOfferedNoSpeakingRate() =>
        Assert.False(ElevenLabsModels.ReadsRate(ElevenLabsModels.V4Turbo));

    [Fact]
    public void ItIsFirstInTheRow() =>
        Assert.Equal(ElevenLabsModels.V4Turbo, ElevenLabsModels.All[0].Id);
}
