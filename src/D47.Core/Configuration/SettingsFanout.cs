using D47.Core.Capabilities.Builtin;

namespace D47.Core.Configuration;

/// <summary>Which part of the app a settings key is downstream of.</summary>
public enum SettingsSubsystem
{
    /// <summary>A key nothing re-reads.</summary>
    None,
    LanguageModel,
    Speech,
    Audio,
    Callouts,
    Listening,
    Persona,
}

/// <summary>What one settings change asks the composition root to re-apply.</summary>
public sealed record SettingsFanoutPlan
{
    public required SettingsSubsystem Subsystem { get; init; }

    /// <summary>Whether the core aboard should be given a voice as well.</summary>
    public required bool ChooseVoiceForCoreAboard { get; init; }
}

/// <summary>Which subsystems a settings key reaches (Phase 4, "Apply every setting without a restart").</summary>
public static class SettingsFanout
{
    private static readonly (string Prefix, SettingsSubsystem Subsystem)[] Routes =
    [
        ("llm.", SettingsSubsystem.LanguageModel),
        ("speech.", SettingsSubsystem.Speech),
        ("audio.", SettingsSubsystem.Audio),
        ("callouts.", SettingsSubsystem.Callouts),
        ("listening.", SettingsSubsystem.Listening),
        ("persona.", SettingsSubsystem.Persona),
    ];

    /// <summary>The plan for one changed key.</summary>
    public static SettingsFanoutPlan For(string? key)
    {
        var subsystem = SettingsSubsystem.None;

        foreach (var (prefix, routed) in Routes)
        {
            if (key is not null && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                subsystem = routed;
                break;
            }
        }

        return new SettingsFanoutPlan
        {
            Subsystem = subsystem,

            // Only the Voice row itself, not every speech row: a Commander moving the rate slider has not
            // asked d47 to go and pick a voice for the core aboard.
            ChooseVoiceForCoreAboard = subsystem == SettingsSubsystem.Speech
                && string.Equals(key, SpeechCapability.VoiceKey, StringComparison.OrdinalIgnoreCase),
        };
    }
}
