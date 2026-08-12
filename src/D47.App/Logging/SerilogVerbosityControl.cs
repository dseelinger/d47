using D47.Core.Configuration;
using D47.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace D47.App.Logging;

/// <summary>
/// Runtime per-subsystem verbosity, implemented over Serilog level switches. Serilog reads a
/// switch on every event, so changing one takes effect on the next log call — no restart, no
/// reconfiguration (list.md Phase 1, "Turn a subsystem up without restarting").
/// </summary>
public sealed class SerilogVerbosityControl : ILogVerbosityControl
{
    private readonly Dictionary<string, LoggingLevelSwitch> _switches;

    public SerilogVerbosityControl()
    {
        Default = new LoggingLevelSwitch(LogEventLevel.Information);
        _switches = Subsystems.All.ToDictionary(
            subsystem => subsystem,
            _ => new LoggingLevelSwitch(LogEventLevel.Information),
            StringComparer.Ordinal);
    }

    /// <summary>The floor for anything without a subsystem of its own.</summary>
    public LoggingLevelSwitch Default { get; }

    public IReadOnlyDictionary<string, LogLevel> Levels =>
        _switches.ToDictionary(pair => pair.Key, pair => ToLogLevel(pair.Value.MinimumLevel), StringComparer.Ordinal);

    /// <summary>The switch Serilog binds to this subsystem's source prefix.</summary>
    public LoggingLevelSwitch SwitchFor(string subsystem) => _switches[subsystem];

    public void Set(string subsystem, LogLevel level)
    {
        var canonical = Subsystems.Canonical(subsystem)
                        ?? throw new ArgumentException($"'{subsystem}' is not a subsystem.", nameof(subsystem));

        _switches[canonical].MinimumLevel = ToEventLevel(level);
    }

    /// <summary>Applies levels loaded from settings. Absent subsystems fall back to the default.</summary>
    public void Apply(LoggingSettings settings)
    {
        Default.MinimumLevel = ToEventLevel(settings.Default);

        foreach (var subsystem in Subsystems.All)
        {
            _switches[subsystem].MinimumLevel = settings.Subsystems.TryGetValue(subsystem, out var level)
                ? ToEventLevel(level)
                : ToEventLevel(settings.Default);
        }
    }

    private static LogEventLevel ToEventLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        LogLevel.None => LevelAlias.Off,
        _ => LogEventLevel.Information,
    };

    private static LogLevel ToLogLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Trace,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.None,
    };
}
