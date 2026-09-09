using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A voice the provider refuses does not survive to be refused again.</summary>
public class ARefusedVoiceIsForgottenTests
{
    private const string Refused = "en-US-RogerNeural";

    private static D47Settings Holding() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = SpeechCapability.ElevenLabsId,
            Voice = Refused,
            CarrierCaptainVoice = Refused,
            TowerVoice = "en-AU-NatashaNeural",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["warden"] = Refused,
                ["sentinel"] = Refused,
                ["archivist"] = "en-GB-SoniaNeural",
            },
        },
    };

    /// <summary>Every place that could hold it, not the one that happened to be speaking.</summary>
    [Fact]
    public void TheRefusedVoiceIsRemovedFromEveryPlaceThatHeldIt()
    {
        var after = SpeechCapability.WithoutTheVoice(Holding(), Refused);

        Assert.Null(after.Speech.Voice);
        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.DoesNotContain(Refused, after.Persona.Voices.Values);
    }

    /// <summary>And nothing else goes with it.</summary>
    [Fact]
    public void TheVoicesThatStillWorkAreLeftAlone()
    {
        var after = SpeechCapability.WithoutTheVoice(Holding(), Refused);

        Assert.Equal("en-AU-NatashaNeural", after.Speech.TowerVoice);
        Assert.Equal("en-GB-SoniaNeural", after.Persona.Voices["archivist"]);
    }

    /// <summary>The pairing is not re-run.</summary>
    [Fact]
    public void ThePairingIsNotThrownAwayOverOneVoice()
    {
        Assert.True(SpeechCapability.WithoutTheVoice(Holding(), Refused).Persona.VoicesPaired);
    }

    /// <summary>
    /// A voice nothing holds changes nothing, so the repair can be applied without first checking
    /// whether it is needed.
    /// </summary>
    [Fact]
    public void ForgettingAVoiceNobodyUsesLeavesTheSettingsAlone()
    {
        var before = Holding();
        var after = SpeechCapability.WithoutTheVoice(before, "en-IE-ConnorNeural");

        Assert.Equal(before.Speech.Voice, after.Speech.Voice);
        Assert.Equal(before.Persona.Voices, after.Persona.Voices);
    }

    /// <summary>
    /// The provider is recorded when the voices are cleared, which is what lets the next launch tell a
    /// file whose voices match the selected provider from one whose voices do not.
    /// </summary>
    [Fact]
    public void ClearingTheVoicesRecordsWhichProviderTheNextOnesBelongTo()
    {
        var after = SpeechCapability.WithoutChosenVoices(Holding(), SpeechCapability.EdgeId);

        Assert.Equal(SpeechCapability.EdgeId, after.Speech.VoicesProvider);
    }

    /// <summary>The fault the repair hangs off.</summary>
    [Fact]
    public void AnOrdinaryFailureIsNotMistakenForARefusedVoice()
    {
        Assert.Equal(TtsFault.Unknown, new TtsException("the account is out of characters").Fault);
        Assert.Equal(
            TtsFault.VoiceRejected,
            new TtsException("an invalid ID has been received", fault: TtsFault.VoiceRejected).Fault);
    }
}
