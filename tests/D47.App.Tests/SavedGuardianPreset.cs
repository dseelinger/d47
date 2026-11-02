using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.App.Tests;

/// <summary>Seeds the settings with one saved Guardian voice preset.</summary>
internal static class SavedGuardianPreset
{
    /// <summary>Saves a preset with one effect ticked at a level and every other effect at its default.</summary>
    public static void WithSaved(SettingsService settings, string name, string tickedId, int level)
    {
        var effects = GuardianVoice.Table
            .Select(effect => new GuardianVoiceEffect
            {
                Id = effect.Id,
                Ticked = effect.Id == tickedId,
                Level = effect.Id == tickedId ? level : effect.DefaultLevel,
            })
            .ToList();

        settings.Replace("test preset", s => s with
        {
            Speech = s.Speech with
            {
                GuardianVoice = s.Speech.GuardianVoice with
                {
                    SavedPresets = [new GuardianVoicePreset { Name = name, Effects = effects }],
                },
            },
        });
    }
}
