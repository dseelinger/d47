using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#237: the four built-in Guardian voice presets, and matching against them and saved ones.</summary>
public class GuardianPresetsTests
{
    private static SpeechSettings With(IReadOnlyList<GuardianVoiceEffect>? effects = null, string? basis = null) =>
        new()
        {
            GuardianVoice = new GuardianVoiceSettings { Effects = effects, Basis = basis },
        };

    /// <summary>Reverb alone, ticked by itself, matches none of the four built-ins.</summary>
    private static SpeechSettings Custom() =>
        With(GuardianVoice.Effects(new GuardianVoiceSettings())
            .Select(effect => effect.Id == "reverb" ? effect with { Ticked = true } : effect).ToList());

    [Fact]
    public void TheDefaultSettingsMatchOff()
    {
        Assert.Equal("off", GuardianPresets.Preset(With()));
    }

    [Fact]
    public void TheCustomHelperReadsCustom()
    {
        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(Custom()));
    }

    [Theory]
    [InlineData("vocoder", "cylon", "chorus", "reverb")]
    [InlineData("deep-core", "pitchDown", "octaveDown", "reverb")]
    [InlineData("damaged-core", "comb", "ringMod", "glitch")]
    [InlineData("ring-mod-rasp", "deepRingMod")]
    [InlineData("8-bit-computer", "bitcrusher")]
    [InlineData("flanged-vocoder", "cylon", "flanger")]
    [InlineData("stutter-host", "stutter", "chorus")]
    public void WritingABuiltinTicksExactlyItsEffectsAtDefaultOrderAndLevels(string id, params string[] ticked)
    {
        var builtin = GuardianPresets.Find(id)!;
        var effects = GuardianPresets.EffectsFor(builtin);

        Assert.Equal(GuardianVoice.Table.Select(effect => effect.Id), effects.Select(effect => effect.Id));
        Assert.Equal(ticked, effects.Where(effect => effect.Ticked).Select(effect => effect.Id));
        Assert.Equal(GuardianVoice.Table.Select(effect => effect.DefaultLevel), effects.Select(effect => effect.Level));

        Assert.Equal(id, GuardianPresets.Preset(With(effects)));
    }

    [Fact]
    public void ChangingALevelAfterPickingABuiltinReadsCustom()
    {
        var effects = GuardianPresets.EffectsFor(GuardianPresets.Find("vocoder")!)
            .Select(effect => effect.Id == "cylon" ? effect with { Level = effect.Level - 1 } : effect)
            .ToList();

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(With(effects)));
    }

    [Fact]
    public void ChangingTheOrderAfterPickingABuiltinReadsCustom()
    {
        var effects = GuardianPresets.EffectsFor(GuardianPresets.Find("vocoder")!).ToList();
        (effects[0], effects[1]) = (effects[1], effects[0]);

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(With(effects)));
    }

    [Fact]
    public void SaveThenChangeThenWritingTheSavedPresetRestoresItExactly()
    {
        var speech = Custom();
        var change = GuardianPresets.Save(speech, "Comm damage");
        Assert.Null(change.Refusal);
        speech = change.Settings!;

        Assert.Equal("saved:Comm damage", GuardianPresets.Preset(speech));
        Assert.Equal("Comm damage", speech.GuardianVoice.Basis);

        var saved = speech.GuardianVoice.SavedPresets!.Single().Effects;

        // Change the ticks, order and levels away from the saved preset.
        var mutated = GuardianVoice.Effects(speech.GuardianVoice)
            .Select(effect => effect with { Ticked = !effect.Ticked, Level = 3 })
            .ToList();
        (mutated[0], mutated[1]) = (mutated[1], mutated[0]);
        speech = speech with { GuardianVoice = speech.GuardianVoice with { Effects = mutated } };

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(speech));

        var restored = GuardianPresets.Select(speech, "saved:Comm damage");
        var restoredEffects = GuardianVoice.Effects(restored.GuardianVoice);

        Assert.Equal(saved.Select(e => e.Id), restoredEffects.Select(e => e.Id));
        Assert.Equal(saved.Select(e => e.Ticked), restoredEffects.Select(e => e.Ticked));
        Assert.Equal(saved.Select(e => e.Level), restoredEffects.Select(e => e.Level));
        Assert.Equal("Comm damage", restored.GuardianVoice.Basis);
        Assert.Equal("saved:Comm damage", GuardianPresets.Preset(restored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SaveIsRefusedForAnEmptyName(string name)
    {
        Assert.Equal("Enter a name.", GuardianPresets.Save(Custom(), name).Refusal);
    }

    [Fact]
    public void SaveIsRefusedForA33CharacterName()
    {
        Assert.Equal("Enter a name.", GuardianPresets.Save(Custom(), new string('x', 33)).Refusal);
    }

    [Theory]
    [InlineData("vocoder")]
    [InlineData("CUSTOM")]
    public void SaveIsRefusedForABuiltinOrCustomName(string name)
    {
        Assert.Equal("A preset with that name already exists.", GuardianPresets.Save(Custom(), name).Refusal);
    }

    [Fact]
    public void SaveIsRefusedForAnExistingSavedNameInADifferentCase()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;

        // Back to a different Custom combination, so Save is reachable again.
        var another = speech with
        {
            GuardianVoice = speech.GuardianVoice with
            {
                Effects = GuardianVoice.Effects(speech.GuardianVoice)
                    .Select(effect => effect.Id == "glitch" ? effect with { Ticked = true } : effect).ToList(),
            },
        };

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(another));
        Assert.Equal("A preset with that name already exists.", GuardianPresets.Save(another, "HULL BREACH").Refusal);
    }

    [Fact]
    public void SaveIsRefusedWhenTheCurrentPresetIsNotCustom()
    {
        Assert.NotNull(GuardianPresets.Save(With(), "Anything").Refusal);
    }

    [Fact]
    public void UpdateAfterChangingALoadedPresetMakesTheRowReadThatPresetAgain()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;
        speech = GuardianPresets.Select(speech, "saved:Hull breach");

        var changed = speech with
        {
            GuardianVoice = speech.GuardianVoice with
            {
                Effects = GuardianVoice.Effects(speech.GuardianVoice)
                    .Select(effect => effect.Id == "glitch" ? effect with { Ticked = true } : effect).ToList(),
            },
        };

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(changed));

        var updated = GuardianPresets.Update(changed);
        Assert.Null(updated.Refusal);
        Assert.Equal("saved:Hull breach", GuardianPresets.Preset(updated.Settings!));
    }

    [Fact]
    public void UpdateIsRefusedWhenTheBasisNamesAPresetThatNoLongerExists()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;
        speech = GuardianPresets.Delete(GuardianPresets.Select(speech, "saved:Hull breach")).Settings!;

        // Custom (deleting left the effects as they were, matching nothing) but with a stale basis forced back in.
        speech = speech with { GuardianVoice = speech.GuardianVoice with { Basis = "Hull breach" } };

        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(speech));
        Assert.Equal("There is no preset to update.", GuardianPresets.Update(speech).Refusal);
    }

    [Fact]
    public void RenameToADifferentCaseOfItsOwnNameSucceeds()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;
        speech = GuardianPresets.Select(speech, "saved:Hull breach");

        var renamed = GuardianPresets.Rename(speech, "hull breach");

        Assert.Null(renamed.Refusal);
        Assert.Equal("saved:hull breach", GuardianPresets.Preset(renamed.Settings!));
        Assert.Equal("hull breach", renamed.Settings!.GuardianVoice.Basis);
    }

    [Fact]
    public void RenameToTheSameNameIsRefused()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;
        speech = GuardianPresets.Select(speech, "saved:Hull breach");

        Assert.Equal("That is already its name.", GuardianPresets.Rename(speech, "Hull breach").Refusal);
    }

    [Fact]
    public void DeleteLeavesTheEffectsAndClearsTheBasisAndReadsCustom()
    {
        var speech = GuardianPresets.Save(Custom(), "Hull breach").Settings!;
        speech = GuardianPresets.Select(speech, "saved:Hull breach");
        var effectsBefore = GuardianVoice.Effects(speech.GuardianVoice);

        var deleted = GuardianPresets.Delete(speech);

        Assert.Null(deleted.Refusal);
        Assert.Null(deleted.Settings!.GuardianVoice.Basis);
        Assert.Empty(deleted.Settings!.GuardianVoice.SavedPresets!);
        Assert.Equal(effectsBefore.Select(e => e.Id), GuardianVoice.Effects(deleted.Settings!.GuardianVoice).Select(e => e.Id));
        Assert.Equal(GuardianPresets.CustomId, GuardianPresets.Preset(deleted.Settings!));
    }

    [Fact]
    public void DeleteAndRenameAreRefusedOnABuiltin()
    {
        Assert.Equal("Only a saved preset can be deleted.", GuardianPresets.Delete(With()).Refusal);
        Assert.Equal("Only a saved preset can be renamed.", GuardianPresets.Rename(With(), "Anything").Refusal);
    }

    [Fact]
    public void OffAndResetBothClearEveryTickAndRestoreTheDefaultOrderAndLevels()
    {
        var speech = With(
            GuardianVoice.Effects(new GuardianVoiceSettings())
                .Select(effect => effect with { Ticked = true, Level = 3 }).ToList(),
            basis: "whatever");

        var reset = GuardianPresets.Reset(speech).Settings!;
        Assert.Equal("off", GuardianPresets.Preset(reset));
        Assert.Null(reset.GuardianVoice.Basis);

        var off = GuardianPresets.Select(speech, "off");
        Assert.Equal("off", GuardianPresets.Preset(off));
        Assert.Null(off.GuardianVoice.Basis);
    }
}
