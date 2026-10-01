using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Persona;

public sealed class ABeaconScanWakesTheCoresTests
{
    [Fact]
    public async Task ThePickerShowsAHeldCoreLockedNamesTheStoryAndRefusesIt()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        using var install = new TempInstall();
        var surface = TestSurface.For(install, personas: new PersonaHost(cores: fixtures.Cores("F1")));
        var row = surface.Settings.Find(PersonaCapability.PersonaKey)!;

        Assert.Equal("LOCKED", row.StatusFor("warden", surface.Settings.Current)?.Text);
        Assert.Null(row.StatusFor("covas", surface.Settings.Current));

        var refused = surface.Settings.Apply(PersonaCapability.PersonaKey, "warden", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, refused.Status);
        Assert.Equal(
            "Warden is held back while The Test Story runs, until you scan a Guardian beacon. Pause or abandon the story to have it back now.",
            refused.Message);

        fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1));

        Assert.Null(row.StatusFor("warden", surface.Settings.Current));
        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(PersonaCapability.PersonaKey, "warden", SettingsCaller.Panel).Status);

        Assert.Equal("warden", surface.Settings.Read(PersonaCapability.PersonaKey));

        var heretic = surface.Settings.Apply(PersonaCapability.PersonaKey, "heretic", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, heretic.Status);
        Assert.Contains("until you scan a Guardian beacon in a second system.", heretic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePickerShowsTheStockCoreWhileTheChosenOneIsHeld()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        using var install = new TempInstall();
        var surface = TestSurface.For(install, personas: new PersonaHost(cores: fixtures.Cores("F1")));
        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(PersonaCapability.PersonaKey, "kex", SettingsCaller.Panel).Status);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal("covas", surface.Settings.Read(PersonaCapability.PersonaKey));
        Assert.Equal("kex", surface.Settings.Current.Persona.Id);

        fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1));

        Assert.Equal("kex", surface.Settings.Read(PersonaCapability.PersonaKey));
    }
}
