using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The Voice row belongs to the core aboard.</summary>
public class VoiceFollowsTheCoreTests
{
    private static SettingApplyResult Choose(TestSurface surface, string key, string? value) =>
        surface.Settings.Apply(key, value, SettingsCaller.Panel);

    [Fact]
    public void AVoiceChosenIsStoredAgainstTheCoreAboard()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Choose(surface, PersonaCapability.PersonaKey, "cora");
        Choose(surface, SpeechCapability.VoiceKey, "en-US-AriaNeural");

        Assert.Equal("en-US-AriaNeural", surface.Settings.Current.Persona.Voices["cora"]);
        Assert.Equal("en-US-AriaNeural", surface.Settings.Read(SpeechCapability.VoiceKey));

        // Another core, and the row is that core's — empty here, because nothing has been chosen or paired
        // for it yet.
        Choose(surface, PersonaCapability.PersonaKey, "kex");
        Assert.Null(surface.Settings.Read(SpeechCapability.VoiceKey));

        Choose(surface, SpeechCapability.VoiceKey, "en-GB-RyanNeural");
        Assert.Equal("en-GB-RyanNeural", surface.Settings.Read(SpeechCapability.VoiceKey));

        // And Cora still has hers.
        Choose(surface, PersonaCapability.PersonaKey, "cora");
        Assert.Equal("en-US-AriaNeural", surface.Settings.Read(SpeechCapability.VoiceKey));
    }

    /// <summary>
    /// The reported fault, as a property of settings alone: with a voice in the old global slot and a
    /// pairing for the core aboard, it is the pairing that speaks.
    /// </summary>
    [Fact]
    public void APairingBeatsTheVoiceChosenBeforeVoicesWerePerCore()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Replace("a file from before voices were per core", current => current with
        {
            Speech = current.Speech with { Voice = "en-US-GuyNeural" },
            Persona = current.Persona with
            {
                Voices = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = "en-US-AriaNeural" },
            },
        });

        // Warden has no pairing, so the old value still stands in for him.
        Assert.Equal("en-US-GuyNeural", SpeechCapability.ShipVoiceFor(surface.Settings.Current, "warden"));

        // Cora does, and hers wins — which it did not before, so selecting her changed everything about the
        // companion except the one thing you hear.
        Assert.Equal("en-US-AriaNeural", SpeechCapability.ShipVoiceFor(surface.Settings.Current, "cora"));
    }

    [Fact]
    public void ClearingTheRowRemovesThePairingRatherThanStoringAnEmptyOne()
    {
        // "Let d47 choose again" has to be expressible, and an empty pairing would read as a choice already
        // made — the one state that stops it being asked for.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Choose(surface, SpeechCapability.VoiceKey, "en-GB-SoniaNeural");
        Assert.NotEmpty(surface.Settings.Current.Persona.Voices);

        Choose(surface, SpeechCapability.VoiceKey, null);

        Assert.Empty(surface.Settings.Current.Persona.Voices);
        Assert.Null(surface.Settings.Read(SpeechCapability.VoiceKey));
    }

    [Fact]
    public void TheOldSingleVoiceIsReadUntilACoreHasOneAndIsThenLetGo()
    {
        // A settings file written before voices were kept per core has one in the old place.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Replace(
            "a settings file from before voices were per core",
            current => current with { Speech = current.Speech with { Voice = "en-US-GuyNeural" } });

        Assert.Equal("en-US-GuyNeural", surface.Settings.Read(SpeechCapability.VoiceKey));

        Choose(surface, SpeechCapability.VoiceKey, "en-GB-SoniaNeural");

        Assert.Null(surface.Settings.Current.Speech.Voice);
        Assert.Equal("en-GB-SoniaNeural", surface.Settings.Current.Persona.Voices["warden"]);
    }
}
