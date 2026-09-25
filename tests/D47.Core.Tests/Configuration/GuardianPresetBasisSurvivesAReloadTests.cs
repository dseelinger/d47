using D47.Core.Audio;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#237: a saved preset and the basis it sets both come back after a save and reload.</summary>
public class GuardianPresetBasisSurvivesAReloadTests
{
    [Fact]
    public void TheBasisAndSavedPresetSurviveASaveAndReload()
    {
        using var install = new TempInstall();
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        // Reverb alone, ticked by itself, matches none of the four built-ins.
        var custom = store.Load().Speech;
        custom = custom with
        {
            GuardianVoice = custom.GuardianVoice with
            {
                Effects = GuardianVoice.Effects(custom.GuardianVoice)
                    .Select(effect => effect.Id == "reverb" ? effect with { Ticked = true } : effect).ToList(),
            },
        };

        var saved = GuardianPresets.Save(custom, "Hull breach");
        Assert.Null(saved.Refusal);

        store.Save(store.Load() with { Speech = saved.Settings! });

        var reloaded = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();

        Assert.Equal("Hull breach", reloaded.Speech.GuardianVoice.Basis);
        Assert.Equal(["Hull breach"], reloaded.Speech.GuardianVoice.SavedPresets!.Select(preset => preset.Name));
        Assert.Equal("saved:Hull breach", GuardianPresets.Preset(reloaded.Speech));
    }
}
