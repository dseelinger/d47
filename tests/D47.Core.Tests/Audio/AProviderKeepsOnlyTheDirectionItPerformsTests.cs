using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AProviderKeepsOnlyTheDirectionItPerformsTests
{
    [Fact]
    public void ADirectionTheProviderRefusesIsRemovedAndTheRestStays()
    {
        var tts = new FakeTtsProvider { PerformsOnly = new HashSet<string> { "laugh" } };

        Assert.Equal("Fine [laugh] then.", AudioTags.For("Fine [laugh] [sighs] then.", tts.Performs));
    }

    [Fact]
    public void AProviderThatPerformsNothingStripsEveryDirection()
    {
        Assert.Equal("Fine then.", AudioTags.For("Fine [laugh] [sighs] then.", new FakeTtsProvider().Performs));
    }

    [Fact]
    public void AProviderThatPerformsEverythingLeavesTheSentenceAlone()
    {
        var tts = new FakeTtsProvider { ReadsAudioTags = true };

        Assert.Equal("Fine [laugh]  [sighs] then.", AudioTags.For("Fine [laugh]  [sighs] then.", tts.Performs));
    }

    [Fact]
    public void TheMeterAsksTheWrappedProviderAboutEachDirection()
    {
        var inner = new FakeTtsProvider { PerformsOnly = new HashSet<string> { "laugh" } };
        ITtsProvider metered = new MeteredTtsProvider(inner, new SpeechSpend());

        Assert.True(metered.Performs("laugh"));
        Assert.False(metered.Performs("sighs"));
    }
}
