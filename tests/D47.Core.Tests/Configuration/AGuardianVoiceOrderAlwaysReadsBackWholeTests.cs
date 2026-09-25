using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#478: whatever the effect list holds, it reads back as every effect exactly once.</summary>
public class AGuardianVoiceOrderAlwaysReadsBackWholeTests
{
    private static readonly string[] DefaultOrder = [.. GuardianVoice.Table.Select(effect => effect.Id)];

    private static IReadOnlyList<GuardianVoiceEffect> Read(params GuardianVoiceEffect[] stored) =>
        GuardianVoice.Effects(new GuardianVoiceSettings { Effects = stored });

    private static GuardianVoiceEffect Effect(string id, bool ticked = false, int level = 5) =>
        new() { Id = id, Ticked = ticked, Level = level };

    [Fact]
    public void AnAbsentListIsEveryEffectOffInTheDefaultOrderAtDefaultLevels()
    {
        var effects = GuardianVoice.Effects(new GuardianVoiceSettings());

        Assert.Equal(DefaultOrder, effects.Select(effect => effect.Id));
        Assert.All(effects, effect => Assert.False(effect.Ticked));
        Assert.Equal(GuardianVoice.Table.Select(effect => effect.DefaultLevel), effects.Select(effect => effect.Level));
    }

    [Fact]
    public void AnUnknownIdAndARepeatAreDropped()
    {
        var effects = Read(
            Effect("reverb", ticked: true),
            Effect("flangerFromTheFuture", ticked: true),
            Effect("reverb", ticked: false));

        Assert.Equal(DefaultOrder.Length, effects.Count);
        Assert.Equal("reverb", effects[^1].Id);
        Assert.True(effects[^1].Ticked);
    }

    [Fact]
    public void AMissingEffectIsInsertedUntickedAfterTheEffectBeforeItByDefault()
    {
        var effects = Read(Effect("reverb", ticked: true), Effect("chorus", ticked: true), Effect("cylon"));

        Assert.Equal(
            [
                "stutter", "monotone", "steppedPitch", "reverb", "chorus", "hive", "flanger", "phaser", "wah",
                "comb", "ringMod", "deepRingMod", "tremolo", "overdrive", "bitcrusher", "glitch", "reverseReverb",
                "shimmer", "cylon", "whisper", "pitchDown", "octaveDown",
            ],
            effects.Select(effect => effect.Id));

        var comb = effects.Single(effect => effect.Id == "comb");
        Assert.False(comb.Ticked);
        Assert.Equal(GuardianVoice.Find("comb")!.DefaultLevel, comb.Level);
    }

    [Fact]
    public void ALevelOutsideOneToTwentyReadsAsTheDefault()
    {
        var effects = Read(Effect("cylon", level: 0), Effect("reverb", level: 21), Effect("chorus", level: 20));

        Assert.Equal(GuardianVoice.Find("cylon")!.DefaultLevel, effects.Single(e => e.Id == "cylon").Level);
        Assert.Equal(GuardianVoice.Find("reverb")!.DefaultLevel, effects.Single(e => e.Id == "reverb").Level);
        Assert.Equal(20, effects.Single(e => e.Id == "chorus").Level);
    }

    [Fact]
    public void WritingAnOrderWithAnIdMissingOrUnknownReadsBackWhole()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.GuardianEffectKey("glitch"), "true", SettingsCaller.Panel);
        surface.Settings.Apply(SpeechCapability.GuardianLevelKey("glitch"), "3", SettingsCaller.Panel);
        surface.Settings.Apply(SpeechCapability.GuardianOrderKey, "reverb, nonsense, cylon", SettingsCaller.Panel);

        Assert.Equal(
            "stutter,monotone,steppedPitch,reverb,cylon,whisper,pitchDown,octaveDown,chorus,hive,flanger,phaser,wah,comb,"
            + "ringMod,deepRingMod,tremolo,overdrive,bitcrusher,glitch,reverseReverb,shimmer",
            surface.Settings.Read(SpeechCapability.GuardianOrderKey));

        // Reordering keeps what was ticked and the level it was set to.
        Assert.Equal("true", surface.Settings.Read(SpeechCapability.GuardianEffectKey("glitch")));
        Assert.Equal("3", surface.Settings.Read(SpeechCapability.GuardianLevelKey("glitch")));
    }

    [Fact]
    public void ALevelIsHeldWithinOneToTwenty()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.GuardianLevelKey("reverb"), "35", SettingsCaller.Panel);

        Assert.Equal("20", surface.Settings.Read(SpeechCapability.GuardianLevelKey("reverb")));
        Assert.Equal("false", surface.Settings.Read(SpeechCapability.GuardianEffectKey("reverb")));
    }
}
