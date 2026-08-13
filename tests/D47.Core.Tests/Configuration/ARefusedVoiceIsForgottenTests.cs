using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A voice the provider refuses does not survive to be refused again.
/// <para>
/// Reported from a running build at 0.5.7, and the same fault as
/// <see cref="VoicesAreProviderScopedTests"/> seen from the other side. That one stops the state
/// arising while d47 is watching; this is what happens when it arises anyway — an older build
/// wrote the file, or the switch never reached the check — and a settings file that is
/// <em>already</em> mismatched is loaded on every launch. The Commander cannot repair it from
/// the panel either: the voice picker offers what the new provider lists, and a rejected key
/// makes that list empty. So every sentence fails, forever, while the cues keep playing.
/// </para>
/// </summary>
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

    /// <summary>
    /// Every place that could hold it, not the one that happened to be speaking. The ship AI's
    /// slot, both named roles and eleven cores can each hold the same id, and a copy left
    /// anywhere is the next turn failing exactly as before.
    /// </summary>
    [Fact]
    public void TheRefusedVoiceIsRemovedFromEveryPlaceThatHeldIt()
    {
        var after = SpeechCapability.WithoutTheVoice(Holding(), Refused);

        Assert.Null(after.Speech.Voice);
        Assert.Null(after.Speech.CarrierCaptainVoice);
        Assert.DoesNotContain(Refused, after.Persona.Voices.Values);
    }

    /// <summary>
    /// And nothing else goes with it. This is not a provider switch — the other voices were
    /// issued by the provider in use and still work, so dropping them would turn one bad value
    /// into a rebuilt cast.
    /// </summary>
    [Fact]
    public void TheVoicesThatStillWorkAreLeftAlone()
    {
        var after = SpeechCapability.WithoutTheVoice(Holding(), Refused);

        Assert.Equal("en-AU-NatashaNeural", after.Speech.TowerVoice);
        Assert.Equal("en-GB-SoniaNeural", after.Persona.Voices["archivist"]);
    }

    /// <summary>
    /// The pairing is not re-run. It produced eleven choices and ten of them are fine; clearing
    /// the flag would replace all of them because one stopped resolving, and on a provider whose
    /// voice list cannot be reached it would replace them with nothing.
    /// </summary>
    [Fact]
    public void ThePairingIsNotThrownAwayOverOneVoice()
    {
        Assert.True(SpeechCapability.WithoutTheVoice(Holding(), Refused).Persona.VoicesPaired);
    }

    /// <summary>
    /// A voice nothing holds changes nothing, so the repair can be applied without first
    /// checking whether it is needed.
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
    /// The provider is recorded when the voices are cleared, which is what lets the next launch
    /// tell a file whose voices match the selected provider from one whose voices do not. Without
    /// it the only way to notice was to watch the switch happen, and a launch never sees one.
    /// </summary>
    [Fact]
    public void ClearingTheVoicesRecordsWhichProviderTheNextOnesBelongTo()
    {
        var after = SpeechCapability.WithoutChosenVoices(Holding(), SpeechCapability.EdgeId);

        Assert.Equal(SpeechCapability.EdgeId, after.Speech.VoicesProvider);
    }

    /// <summary>
    /// The fault the repair hangs off. Everything else a provider can refuse is for the
    /// Commander to read and act on; this is the only one d47 acts on itself, so the default
    /// has to be the one that changes nothing.
    /// </summary>
    [Fact]
    public void AnOrdinaryFailureIsNotMistakenForARefusedVoice()
    {
        Assert.Equal(TtsFault.Unknown, new TtsException("the account is out of characters").Fault);
        Assert.Equal(
            TtsFault.VoiceRejected,
            new TtsException("an invalid ID has been received", fault: TtsFault.VoiceRejected).Fault);
    }
}
