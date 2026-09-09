using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.Core.Diagnostics;

/// <summary>Runtime per-subsystem verbosity (Phase 1, "Turn a subsystem up without restarting").</summary>
public interface ILogVerbosityControl
{
    IReadOnlyDictionary<string, LogLevel> Levels { get; }

    /// <summary>Takes effect on the next log call.</summary>
    void Set(string subsystem, LogLevel level);

    /// <summary>The floor for anything outside a subsystem's own source prefix.</summary>
    void SetDefault(LogLevel level);
}

/// <summary>The one place logging settings become live levels.</summary>
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
    /// Re-applies the logging settings every time they change, from whichever caller changed them.
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
