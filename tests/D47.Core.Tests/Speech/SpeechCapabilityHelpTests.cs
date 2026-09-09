using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Speech;

/// <summary>
/// The "Other voices" toggles read a lot alike from their labels alone — System chat and Local chat
/// both describe "strangers" or "nearby" without saying which in-game channel is which.
/// </summary>
public class SpeechCapabilityTests
{
    private static SpeechCapability.SpeechSurface Surface() =>
        new() { Silence = () => { }, Beds = () => [] };

    [Theory]
    [InlineData(SpeechCapability.SpeakSystemChatKey, "System")]
    [InlineData(SpeechCapability.SpeakLocalChatKey, "Local")]
    [InlineData(SpeechCapability.SpeakWingChatKey, "Wing")]
    [InlineData(SpeechCapability.SpeakSquadronKey, "Squadron")]
    [InlineData(SpeechCapability.SpeakDirectMessagesKey, "Direct Messages")]
    public void OtherVoicesToggleNamesItsCommsPanelTab(string key, string tab)
    {
        var capability = SpeechCapability.Create(Surface());
        var row = Assert.Single(capability.Settings, r => r.Key == key);

        Assert.Contains($"comms panel's {tab} tab", row.Help);
    }
}
