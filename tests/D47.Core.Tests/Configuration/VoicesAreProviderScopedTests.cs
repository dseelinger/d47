using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A voice id belongs to the provider that issued it.</summary>
public class VoicesAreProviderScopedTests
{
    private static D47Settings Chosen() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = SpeechCapability.EdgeId,
            Voice = "en-US-RogerNeural",
            CarrierCaptainVoice = "en-GB-RyanNeural",
            TowerVoice = "en-AU-NatashaNeural",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sentinel"] = "en-US-RogerNeural",
                ["archivist"] = "en-GB-SoniaNeural",
            },
        },
    };

    [Fact]
    public void SwitchingProviderDropsTheShipsVoiceAndBothNamedRoles()
    {
        var after = SpeechCapability.WithoutChosenVoices(Chosen(), SpeechCapability.ElevenLabsId);

        Assert.Null(after.Speech.Voice);
        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.Null(after.Speech.TowerVoice);
    }

    /// <summary>
    /// The pairings are the ones that actually bit: the ship's voice was never set by hand, so it fell
    /// through to the persona's, which was an Edge id chosen automatically.
    /// </summary>
    [Fact]
    public void SwitchingProviderDropsEveryPersonaPairing()
    {
        var after = SpeechCapability.WithoutChosenVoices(Chosen(), SpeechCapability.ElevenLabsId);

        Assert.Empty(after.Persona.Voices);
    }

    /// <summary>
    /// And clears the flag that says pairing has already happened, or the cores are re-paired never and
    /// keep pointing at nothing.
    /// </summary>
    [Fact]
    public void PairingIsAllowedToRunAgainForTheNewProvider()
    {
        Assert.True(Chosen().Persona.VoicesPaired);
        Assert.False(SpeechCapability.WithoutChosenVoices(Chosen(), SpeechCapability.ElevenLabsId).Persona.VoicesPaired);
    }

    /// <summary>Only the voices.</summary>
    [Fact]
    public void NothingElseAboutSpeechIsTouched()
    {
        var before = Chosen() with
        {
            Speech = Chosen().Speech with
            {
                Rate = 1.15,
                OutputDevice = "Headphones",
                CuesEnabled = false,
                ThinkingBedEnabled = true,
                ThinkingBed = "thinking-hum",
            },
        };

        var after = SpeechCapability.WithoutChosenVoices(before, SpeechCapability.ElevenLabsId);

        Assert.Equal(before.Speech.Rate, after.Speech.Rate);
        Assert.Equal(before.Speech.OutputDevice, after.Speech.OutputDevice);
        Assert.Equal(before.Speech.CuesEnabled, after.Speech.CuesEnabled);
        Assert.Equal(before.Speech.ThinkingBedEnabled, after.Speech.ThinkingBedEnabled);
        Assert.Equal(before.Speech.ThinkingBed, after.Speech.ThinkingBed);

        // And nothing outside speech and the pairings.
        Assert.Equal(before.Ui, after.Ui);
        Assert.Equal(before.Llm, after.Llm);
    }

    /// <summary>
    /// Applying it to settings that have no voices chosen is not an error and changes nothing, so the
    /// caller does not have to ask first.
    /// </summary>
    [Fact]
    public void ClearingWhenNothingWasChosenIsHarmless()
    {
        var fresh = new D47Settings();

        var after = SpeechCapability.WithoutChosenVoices(fresh, SpeechCapability.ElevenLabsId);

        Assert.Null(after.Speech.Voice);
        Assert.Empty(after.Persona.Voices);
        Assert.False(after.Persona.VoicesPaired);
    }
}
