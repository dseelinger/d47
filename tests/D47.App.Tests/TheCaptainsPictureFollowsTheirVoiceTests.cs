using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The carrier captain's and the tower's picture is <c>man</c> or <c>woman</c> by the listed gender of the voice the
/// role speaks in, and none for a voice whose listing gives no gender (#770).
/// </summary>
public sealed class TheCaptainsPictureFollowsTheirVoiceTests
{
    private static VoiceCast Cast() => new()
    {
        Voices = new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["af_heart"] = new("af_heart", "Heart", "en-US", "Female"),
            ["am_michael"] = new("am_michael", "Michael", "en-US", "Male"),
            ["alloy"] = new("alloy", "Alloy", "en-US"),
        },
    };

    private static string? Picture(VoiceRole role, string voice)
    {
        var cast = Cast();
        cast.Assign(role, voice);

        return Announcer.ConversationPicture(
            new Announcement("carrier.jump", "Jump in fifteen minutes.") { Voice = role },
            "warden",
            spoken => cast.GenderOf(cast.For(spoken).VoiceId),
            null);
    }

    [Theory]
    [InlineData(VoiceRole.CarrierCaptain, "af_heart", "captain.woman")]
    [InlineData(VoiceRole.CarrierCaptain, "am_michael", "captain.man")]
    [InlineData(VoiceRole.CarrierCaptain, "alloy", null)]
    [InlineData(VoiceRole.TowerControl, "af_heart", "tower.woman")]
    [InlineData(VoiceRole.TowerControl, "am_michael", "tower.man")]
    [InlineData(VoiceRole.TowerControl, "alloy", null)]
    public void ThePictureIsTheGenderOfTheVoiceSpeaking(VoiceRole role, string voice, string? expected) =>
        Assert.Equal(expected, Picture(role, voice));
}
