using D47.Core;
using D47.Core.Diagnostics;
using Serilog;
using Serilog.Formatting.Compact;

namespace D47.App.Logging;

/// <summary>
/// Two sinks, both files beside the executable: one human-readable, one newline-delimited JSON
/// so an agent can parse a session (list.md Phase 1). Nothing here reaches the network. There
/// is no analytics sink, no metrics endpoint and no crash reporter, and provider egress is a
/// separate concern with its own settings row.
/// </summary>
public static class LoggingSetup
{
    private const string HumanTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static ILogger Create(AppPaths paths, SerilogVerbosityControl verbosity)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(verbosity.Default)
            .Enrich.FromLogContext();

        // One controllable target per subsystem. The switch is read per event, which is what
        // makes a level change take effect without a restart.
        foreach (var (subsystem, prefix) in Subsystems.SourcePrefixes)
        {
            configuration = configuration.MinimumLevel.Override(prefix, verbosity.SwitchFor(subsystem));
        }

        return configuration
            .WriteTo.File(
                Path.Combine(paths.Logs, "d47-.log"),
                outputTemplate: HumanTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(paths.Logs, "d47-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .WriteTo.Console(outputTemplate: HumanTemplate)
            .CreateLogger();
    }
}
