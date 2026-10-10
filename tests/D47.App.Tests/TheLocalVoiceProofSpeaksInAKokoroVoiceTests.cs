using D47.App.Voice;
using D47.Core.Speech;
using Xunit;

namespace D47.App.Tests;

public class TheLocalVoiceProofSpeaksInAKokoroVoiceTests
{
    [Fact]
    public void AKokoroVoiceAlreadyChosenIsKept()
    {
        Assert.Equal("bm_george", SpeechClients.LocalVoiceProofVoice("bm_george"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en-GB-SoniaNeural")]
    public void AnythingElseFallsBackToAKokoroVoice(string? shipVoice)
    {
        Assert.Contains(SpeechClients.LocalVoiceProofVoice(shipVoice), KokoroAssets.VoiceIds);
    }
}
