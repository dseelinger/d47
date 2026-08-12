using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Diagnostics;

/// <summary>
/// Runtime per-subsystem verbosity (list.md Phase 1, "Turn a subsystem up without
/// restarting"). Implemented in D47.App over Serilog level switches — Core needs to read
/// and change levels without knowing what a sink is.
/// </summary>
public interface ILogVerbosityControl
{
    IReadOnlyDictionary<string, LogLevel> Levels { get; }

    /// <summary>Takes effect on the next log call. No restart, no file reload.</summary>
    void Set(string subsystem, LogLevel level);

    /// <summary>The floor for anything outside a subsystem's own source prefix.</summary>
    void SetDefault(LogLevel level);
}

/// <summary>
/// The one place logging settings become live levels. Both the composition root and the tests
/// call <see cref="FollowSettings"/>, so "changing a log level takes effect immediately" has a
/// single implementation rather than one per surface that remembered to do it.
/// </summary>
public static class LogVerbosity
{
    public const string SettingKeyPrefix = "logging.";

    public static void Apply(this ILogVerbosityControl control, LoggingSettings settings)
    {
        control.SetDefault(settings.Default);

        foreach (var subsystem in Subsystems.All)
        {
            control.Set(
                subsystem,
                settings.Subsystems.TryGetValue(subsystem, out var level) ? level : settings.Default);
        }
    }

    /// <summary>
    /// Re-applies the logging settings every time they change, from whichever caller changed
    /// them. Apply the current settings first: this only subscribes.
    /// </summary>
    public static void FollowSettings(this ILogVerbosityControl control, SettingsService settings) =>
        settings.Changed += change =>
        {
            if (change.Key.StartsWith(SettingKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                control.Apply(change.Settings.Logging);
            }
        };
}
