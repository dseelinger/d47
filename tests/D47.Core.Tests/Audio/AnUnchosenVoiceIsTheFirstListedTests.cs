using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>With no voice stored, a line is spoken in the provider's first listed voice; a stored voice always wins (#1050).</summary>
public class AnUnchosenVoiceIsTheFirstListedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoStoredVoiceGivesTheFirstListed(string? stored)
    {
        Assert.Equal("alloy", InterimVoice.For(stored, ["alloy", "ash", "ballad"]));
    }

    [Fact]
    public void AStoredVoiceWins()
    {
        Assert.Equal("onyx", InterimVoice.For("onyx", ["alloy", "ash"]));
    }

    [Fact]
    public void AStoredVoiceTheListDoesNotHoldStillWins()
    {
        Assert.Equal("a-library-voice", InterimVoice.For("a-library-voice", ["alloy"]));
    }

    [Fact]
    public void AnEmptyListGivesNoVoice()
    {
        Assert.Null(InterimVoice.For(null, []));
    }

    [Fact]
    public async Task AnUnchosenSelectionTakesTheProvidersFirstVoiceAndKeepsItsRate()
    {
        var provider = new FakeTtsProvider
        {
            Catalogue = VoiceCatalogue.Of([new VoiceInfo("first", "First", "en-GB"), new VoiceInfo("second", "Second", "en-US")]),
        };

        var voice = await provider.OrFirstListedAsync(
            new VoiceSelection(null, 1.2) { Role = VoiceRole.CarrierCaptain },
            TestContext.Current.CancellationToken);

        Assert.Equal("first", voice.VoiceId);
        Assert.Equal(1.2, voice.Rate);
        Assert.Equal(VoiceRole.CarrierCaptain, voice.Role);
    }

    [Fact]
    public async Task AChosenSelectionIsLeftAlone()
    {
        var chosen = new VoiceSelection("second");

        Assert.Same(chosen, await new FakeTtsProvider().OrFirstListedAsync(chosen, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AProviderListingNothingLeavesTheSelectionUnchosen()
    {
        var provider = new FakeTtsProvider { Catalogue = VoiceCatalogue.Unreachable("no answer") };

        var voice = await provider.OrFirstListedAsync(VoiceSelection.Default, TestContext.Current.CancellationToken);

        Assert.Null(voice.VoiceId);
    }
}
