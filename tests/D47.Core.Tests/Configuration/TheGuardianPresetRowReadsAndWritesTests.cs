using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#237: the <c>speech.guardianVoice.preset</c> row through the settings surface.</summary>
public class TheGuardianPresetRowReadsAndWritesTests
{
    [Fact]
    public void ItStartsAtOffAndWritingVocoderTicksExactlyItsEffects()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal("off", surface.Settings.Read(SpeechCapability.GuardianPresetKey));

        surface.Settings.Apply(SpeechCapability.GuardianPresetKey, "vocoder", SettingsCaller.Panel);

        Assert.Equal("vocoder", surface.Settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("true", surface.Settings.Read(SpeechCapability.GuardianEffectKey("cylon")));
        Assert.Equal("true", surface.Settings.Read(SpeechCapability.GuardianEffectKey("chorus")));
        Assert.Equal("true", surface.Settings.Read(SpeechCapability.GuardianEffectKey("reverb")));
        Assert.Equal("false", surface.Settings.Read(SpeechCapability.GuardianEffectKey("glitch")));
    }

    [Fact]
    public void ChangingALevelAfterPickingABuiltinReadsCustom()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.GuardianPresetKey, "vocoder", SettingsCaller.Panel);
        surface.Settings.Apply(SpeechCapability.GuardianLevelKey("cylon"), "3", SettingsCaller.Panel);

        Assert.Equal("custom", surface.Settings.Read(SpeechCapability.GuardianPresetKey));
    }

    [Fact]
    public void WritingCustomChangesNothing()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.GuardianEffectKey("reverb"), "true", SettingsCaller.Panel);
        Assert.Equal(GuardianPresets.CustomId, surface.Settings.Read(SpeechCapability.GuardianPresetKey));

        surface.Settings.Apply(SpeechCapability.GuardianPresetKey, "custom", SettingsCaller.Panel);

        Assert.Equal("true", surface.Settings.Read(SpeechCapability.GuardianEffectKey("reverb")));
    }

    [Fact]
    public void NoRowInTheGroupIsAdvanced()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var rows = surface.Registry.Find(SpeechCapability.Id)!.Descriptor.Settings
            .Where(row => row.Group == "Guardian Voice Effects");

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.False(row.Advanced));
    }
}
