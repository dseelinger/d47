using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class PickingAVoiceNamesTheVoiceToFetchTests
{
    [Fact]
    public void TheShipsVoiceRowNamesTheCoreAboardsVoice()
    {
        var settings = SpeechCapability.Create(new SpeechCapability.SpeechSurface { Silence = () => { } }).Settings
            .Single(row => row.Key == SpeechCapability.VoiceKey)
            .Binding!.Write!(D47Settings.Defaults, "wren");

        Assert.Equal((VoiceGroup.Aboard, "wren"), SpeechCapability.PickedVoice(SpeechCapability.VoiceKey, settings));
    }

    [Fact]
    public void TheTowerNamesItsOwnVoice()
    {
        var settings = D47Settings.Defaults with
        {
            Speech = D47Settings.Defaults.Speech with
            {
                TowerVoice = "bram",
            },
        };

        Assert.Equal((VoiceGroups.Of(VoiceRole.TowerControl), "bram"), SpeechCapability.PickedVoice(SpeechCapability.TowerVoiceKey, settings));
    }

    [Fact]
    public void AClearedRowOrAnotherKeyPicksNothing()
    {
        Assert.Null(SpeechCapability.PickedVoice(SpeechCapability.NarratorVoiceKey, D47Settings.Defaults));
        Assert.Null(SpeechCapability.PickedVoice(SpeechCapability.RateKey, D47Settings.Defaults));
    }

    [Fact]
    public void ACastLineCarriesItsRoleToTheProvider()
    {
        var cast = new VoiceCast { DefaultVoice = "marlow", Pool = ["wren", "bram"] };

        Assert.Equal(VoiceRole.ShipAi, cast.For(VoiceRole.ShipAi).Role);
        Assert.Equal(VoiceRole.Comms, cast.ForSender("Cmdr Jameson", isPlayer: true).Role);
    }
}
