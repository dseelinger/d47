using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModuleLineIsSpokenAsItsChosenSpeakerTests
{
    private static readonly (string, string)[] Cores = [("warden", "Warden"), ("cora", "Cora")];
    private static readonly (string, string)[] YourCores = [("vesper", "Vesper")];

    private static readonly CustomCharacter Haruka =
        new("0a1b2c3d", "Haruka Senda", new CharacterVoice("elevenlabs", "haruka-voice"), null, Aboard: true, System: null);

    private static readonly CustomCharacter Odell =
        new("1b2c3d4e", "Odell Marsh", new CharacterVoice("edge", "en-GB-RyanNeural"), null, Aboard: true, System: null);

    private static ModuleManifest Manifest(ModuleCharacter? character = null) =>
        new(1, "ledger", "Bounty ledger", "Ana Vey", "1.4.0", "ledger.exe", [], 120, [], character);

    private static readonly ModuleCharacter Tally =
        new("Tally", [new ModuleVoice("edge", "en-US-AriaNeural"), new ModuleVoice("elevenlabs", "tally-voice")]);

    private static D47Settings Settings(string provider = "elevenlabs", string aboard = "warden", Dictionary<string, string>? voices = null) =>
        new()
        {
            Speech = new SpeechSettings { Provider = provider },
            Persona = new PersonaSettings { Id = aboard, Voices = voices ?? new Dictionary<string, string>() },
        };

    private static ModuleVoicing Resolve(ModuleManifest manifest, string? speaksAs, D47Settings settings) =>
        ModuleSpeechCallout.Resolve(
            manifest,
            new ModuleSettings { Enabled = true, SpeaksAs = speaksAs },
            settings,
            "Warden",
            Cores,
            YourCores,
            [Haruka, Odell]);

    [Fact]
    public void AModuleWithNoCharacterSpeaksAsTheCoreAboard()
    {
        var voicing = Resolve(Manifest(), null, Settings());

        Assert.Equal(new ModuleVoicing("Warden", VoiceRole.ShipAi, null, null, null, InRoleVoice: false), voicing);
    }

    [Fact]
    public void AModuleWithACharacterSpeaksAsItInItsVoiceForTheCommandersProvider()
    {
        var voicing = Resolve(Manifest(Tally), null, Settings(provider: "edge"));

        Assert.Equal("Tally", voicing.Speaker);
        Assert.Equal(VoiceRole.Crew, voicing.Role);
        Assert.Equal(new D47.Core.Stories.PinnedVoice("edge", "en-US-AriaNeural"), voicing.Pinned);
        Assert.False(voicing.InRoleVoice);
    }

    [Fact]
    public void ItsCharacterWithNoVoiceForTheProviderSpeaksInTheCoreAboardsVoiceUnderItsOwnName()
    {
        var voicing = Resolve(Manifest(Tally), null, Settings(provider: "kokoro"));

        Assert.Equal(new ModuleVoicing("Tally", VoiceRole.Crew, "Tally", null, null, InRoleVoice: true), voicing);
    }

    [Fact]
    public void TheCoreAboardCanBeChosenOverTheModulesOwnCharacter()
    {
        var voicing = Resolve(Manifest(Tally), ModulePlace.Aboard, Settings());

        Assert.Equal(VoiceRole.ShipAi, voicing.Role);
        Assert.Null(voicing.Speaker);
    }

    [Fact]
    public void AnotherCoreSpeaksInItsOwnVoiceUnderItsName()
    {
        var voicing = Resolve(Manifest(), ModulePlace.CoreValue + "cora", Settings(voices: new() { ["cora"] = "cora-voice" }));

        Assert.Equal(
            new ModuleVoicing("Cora", VoiceRole.Crew, "Cora", new D47.Core.Stories.PinnedVoice("elevenlabs", "cora-voice"), SpeakerPictures.Core("cora"), InRoleVoice: false),
            voicing);
    }

    [Fact]
    public void ACommandersOwnCoreSpeaksInItsOwnVoiceUnderItsName()
    {
        var voicing = Resolve(Manifest(), ModulePlace.YourCoreValue + "vesper", Settings(voices: new() { ["vesper"] = "vesper-voice" }));

        Assert.Equal("Vesper", voicing.Speaker);
        Assert.Equal(new D47.Core.Stories.PinnedVoice("elevenlabs", "vesper-voice"), voicing.Pinned);
    }

    [Fact]
    public void ACoreWithNoVoiceOfItsOwnSpeaksInTheCoreAboardsVoiceUnderItsName()
    {
        var voicing = Resolve(Manifest(), ModulePlace.CoreValue + "cora", Settings());

        Assert.Equal("Cora", voicing.Speaker);
        Assert.Null(voicing.Pinned);
        Assert.True(voicing.InRoleVoice);
    }

    [Fact]
    public void ChoosingTheCoreThatIsAboardSpeaksAsTheCoreAboard()
    {
        var voicing = Resolve(Manifest(), ModulePlace.CoreValue + "warden", Settings(voices: new() { ["warden"] = "warden-voice" }));

        Assert.Equal(VoiceRole.ShipAi, voicing.Role);
        Assert.Null(voicing.Speaker);
        Assert.Null(voicing.Pinned);
    }

    [Fact]
    public void ACommandersCharacterSpeaksInTheirVoiceWhenTheProviderIssuedIt()
    {
        var voicing = Resolve(Manifest(), ModulePlace.CharacterValue + Haruka.Id, Settings());

        Assert.Equal(
            new ModuleVoicing("Haruka Senda", VoiceRole.Crew, "Haruka Senda", new D47.Core.Stories.PinnedVoice("elevenlabs", "haruka-voice"), Haruka.Picture, InRoleVoice: false),
            voicing);
    }

    /// <summary>The voice they are cast when the Commander talks to them, which the role and name decide.</summary>
    [Fact]
    public void ACommandersCharacterWhoseVoiceIsAnotherProvidersIsCastByName()
    {
        var voicing = Resolve(Manifest(), ModulePlace.CharacterValue + Odell.Id, Settings());

        Assert.Equal("Odell Marsh", voicing.Speaker);
        Assert.Null(voicing.Pinned);
        Assert.False(voicing.InRoleVoice);
    }

    [Fact]
    public void ADeletedCharacterFallsBackAsTheSpeaksAsRowShowsIt()
    {
        Assert.Equal(VoiceRole.ShipAi, Resolve(Manifest(), ModulePlace.CharacterValue + "ffffffff", Settings()).Role);
        Assert.Equal("Tally", Resolve(Manifest(Tally), ModulePlace.CharacterValue + "ffffffff", Settings()).Speaker);
    }
}
