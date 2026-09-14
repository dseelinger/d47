using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The Guardian voice group's Test button never pays for a clip (#226).</summary>
public class TestNeverBillsAProviderTests
{
    private static TtsProviderInfo Provider(string id, bool billed, bool offersFreePreviews = false) => new()
    {
        Id = id,
        Name = id,
        Label = id,
        Egress = "test",
        Destination = "test",
        Billed = billed,
        OffersFreePreviews = offersFreePreviews,
    };

    [Fact]
    public void AFreeProviderIsSynthesised()
    {
        var provider = Provider("edge", billed: false);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: false, hasCachedAudition: false);

        Assert.Equal(GuardianVoiceTest.Source.Synthesize, source);
    }

    [Fact]
    public void AFreeProviderIsSynthesisedEvenWithACachedAudition()
    {
        var provider = Provider("kokoro", billed: false);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: false, hasCachedAudition: true);

        Assert.Equal(GuardianVoiceTest.Source.Synthesize, source);
    }

    [Fact]
    public void ABilledProviderWithAFreeSamplePlaysIt()
    {
        var provider = Provider("elevenlabs", billed: true, offersFreePreviews: true);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: true, hasCachedAudition: true);

        Assert.Equal(GuardianVoiceTest.Source.FreeSample, source);
    }

    [Fact]
    public void ABilledProviderWithNoSampleForThisVoiceFallsPastTheFreeSample()
    {
        var provider = Provider("elevenlabs", billed: true, offersFreePreviews: true);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: false, hasCachedAudition: true);

        Assert.Equal(GuardianVoiceTest.Source.CachedAudition, source);
    }

    [Fact]
    public void ABilledProviderWithNoFreeSamplesAtAllUsesACachedAudition()
    {
        var provider = Provider("openai", billed: true, offersFreePreviews: false);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: false, hasCachedAudition: true);

        Assert.Equal(GuardianVoiceTest.Source.CachedAudition, source);
    }

    [Fact]
    public void ABilledProviderWithNothingFreeOrCachedIsTheStandIn()
    {
        var provider = Provider("openai", billed: true, offersFreePreviews: false);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: false, hasCachedAudition: false);

        Assert.Equal(GuardianVoiceTest.Source.StandIn, source);
    }

    [Fact]
    public void NoProviderSelectedIsTheStandIn()
    {
        var provider = Provider(TtsProviderCatalog.NoneId, billed: false);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample: true, hasCachedAudition: true);

        Assert.Equal(GuardianVoiceTest.Source.StandIn, source);
    }

    /// <summary>
    /// A billed provider never comes back as <see cref="GuardianVoiceTest.Source.Synthesize"/> — the
    /// one case AppHost.GuardianTestAsync calls SynthesizeAsync for — whatever else is true of it.
    /// </summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ABilledProviderNeverSynthesises(bool offersFreePreviews, bool hasFreeSample, bool hasCachedAudition)
    {
        var provider = Provider("fake-billed", billed: true, offersFreePreviews);

        var source = GuardianVoiceTest.SourceFor(provider, hasFreeSample, hasCachedAudition);

        Assert.NotEqual(GuardianVoiceTest.Source.Synthesize, source);
    }
}
