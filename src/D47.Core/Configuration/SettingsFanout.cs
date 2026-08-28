using D47.Core.Capabilities.Builtin;

namespace D47.Core.Configuration;

/// <summary>Which part of the app a settings key is downstream of.</summary>
public enum SettingsSubsystem
{
    /// <summary>A key nothing re-reads. Most of the file: window placement, view state, checklists.</summary>
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

    /// <summary>
    /// Whether the core aboard should be given a voice as well.
    /// <para>
    /// Clearing the Voice row is how a Commander asks for the voice d47 chose for this core back,
    /// so one is chosen now rather than the next time this core happens to be selected — which,
    /// for the core already aboard, is a wait with no end in sight.
    /// </para>
    /// </summary>
    public required bool ChooseVoiceForCoreAboard { get; init; }
}

/// <summary>
/// Which subsystems a settings key reaches (Phase 4, "Apply every setting without a
/// restart").
/// <para>
/// The root's job is that every row takes effect immediately and that the startup path and the
/// change path cannot drift. That made this dispatch load-bearing and left it as an if/else
/// chain of string prefixes inside an event handler, where a key routed to the wrong subsystem —
/// or to none — is a row that silently does nothing until the app is restarted. Exactly the
/// class of fault the composition-root harness exists to reach (Phase 19).
/// </para>
/// <para>
/// Pure. The root still does the applying; this only says what to apply.
/// </para>
/// </summary>
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

    /// <summary>
    /// The plan for one changed key. Case-insensitive on the prefix, as the chain it replaces
    /// was — a capability naming its row <c>Speech.Rate</c> still reaches the speech wiring.
    /// </summary>
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

            // Only the Voice row itself, not every speech row: a Commander moving the rate
            // slider has not asked d47 to go and pick a voice for the core aboard.
            ChooseVoiceForCoreAboard = subsystem == SettingsSubsystem.Speech
                && string.Equals(key, SpeechCapability.VoiceKey, StringComparison.OrdinalIgnoreCase),
        };
    }
}
