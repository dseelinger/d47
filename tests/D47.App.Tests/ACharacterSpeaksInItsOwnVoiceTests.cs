using D47.App;
using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>A custom character is heard in the voice stored for them when the provider speaking their role issued it.</summary>
public class ACharacterSpeaksInItsOwnVoiceTests
{
    private static readonly CharacterVoice Adam = new("kokoro", "am_adam");

    private static VoiceCast Cast() => new()
    {
        DefaultVoice = "af_heart",
        Pool = ["af_heart", "am_adam", "bf_emma", "bm_george"],
        Voices = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["af_heart"] = new("af_heart", "Heart", "en-US", "Female"),
            ["am_adam"] = new("am_adam", "Adam", "en-US", "Male"),
            ["bf_emma"] = new("bf_emma", "Emma", "en-GB", "Female"),
            ["bm_george"] = new("bm_george", "George", "en-GB", "Male"),
        },
    };

    [Theory]
    [InlineData(VoiceRole.Crew)]
    [InlineData(VoiceRole.Comms)]
    public void AStoredVoiceIsSpokenAboardAndOverTheRadio(VoiceRole role)
    {
        var said = AppHost.AddressedVoiceOf(new TurnEvent.Addressed(role, "Mara", 1, Voice: Adam), Cast(), "kokoro");

        Assert.Equal("am_adam", said.VoiceId);
        Assert.Equal(role, said.Role);
    }

    [Theory]
    [InlineData(VoiceRole.Crew)]
    [InlineData(VoiceRole.Comms)]
    public void NoStoredVoiceIsSpokenInTheRolesVoice(VoiceRole role)
    {
        var said = AppHost.AddressedVoiceOf(new TurnEvent.Addressed(role, "Mara", 1), Cast(), "kokoro");

        Assert.Equal(Cast().ForSender("Mara", isPlayer: false, role).VoiceId, said.VoiceId);
        Assert.NotEqual(Cast().DefaultVoice, said.VoiceId);
    }

    [Fact]
    public void AVoiceFromAnotherProviderIsSpokenInTheRolesVoice()
    {
        var said = AppHost.AddressedVoiceOf(new TurnEvent.Addressed(VoiceRole.Crew, "Mara", 1, Voice: Adam), Cast(), "elevenlabs");

        Assert.Equal(Cast().ForSender("Mara", isPlayer: false, VoiceRole.Crew).VoiceId, said.VoiceId);
    }

    [Fact]
    public void AVoiceTheProviderNoLongerListsIsSpokenInTheRolesVoice()
    {
        var gone = new TurnEvent.Addressed(VoiceRole.Crew, "Mara", 1, Voice: new CharacterVoice("kokoro", "am_gone"));

        Assert.Equal(Cast().ForSender("Mara", isPlayer: false, VoiceRole.Crew).VoiceId, AppHost.AddressedVoiceOf(gone, Cast(), "kokoro").VoiceId);
    }

    [Fact]
    public void ARolesPinnedVoiceIsUsedWhenNoneIsStored()
    {
        var cast = Cast();
        cast.Assign(VoiceRole.Crew, "bm_george");

        Assert.Equal("bm_george", AppHost.AddressedVoiceOf(new TurnEvent.Addressed(VoiceRole.Crew, "Mara", 1), cast, "kokoro").VoiceId);
    }
}
