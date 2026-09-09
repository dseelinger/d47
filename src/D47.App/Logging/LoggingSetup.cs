using D47.Core;
using D47.Core.Diagnostics;
using Serilog;
using Serilog.Formatting.Compact;

namespace D47.App.Logging;

/// <summary>
/// Two sinks, both files beside the executable: one human-readable, one newline-delimited JSON so an
/// agent can parse a session (Phase 1).
/// </summary>
public static class LoggingSetup
{
    private const string HumanTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>How long the readable log is kept.</summary>
    public static readonly TimeSpan ReadableLogLife = TimeSpan.FromDays(90);

    /// <summary>How long the machine-parsing copy is kept.</summary>
    public static readonly TimeSpan MachineLogLife = TimeSpan.FromDays(14);

    /// <summary>The most either sink may write in one day.</summary>
    public const long MostBytesPerDay = 4L * 1024 * 1024;

    public static ILogger Create(AppPaths paths, SerilogVerbosityControl verbosity)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(verbosity.Default)
            .Enrich.FromLogContext();

        // One controllable target per subsystem, over however many namespaces that subsystem spans.
        foreach (var (subsystem, prefixes) in Subsystems.SourcePrefixes)
        {
            var level = verbosity.SwitchFor(subsystem);

            foreach (var prefix in prefixes)
            {
                configuration = configuration.MinimumLevel.Override(prefix, level);
            }
        }

        // **By time rather than by count** (#168). `retainedFileCountLimit: 14` stood here since Phase 1 and
        // meant fourteen *files* — a fortnight of daily play, and less than that whenever the sink rolled
        // more often than the calendar.
        return configuration
            .WriteTo.File(
                Path.Combine(paths.Logs, "d47-.log"),
                outputTemplate: HumanTemplate,
                fileSizeLimitBytes: MostBytesPerDay,
                rollOnFileSizeLimit: false,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                retainedFileTimeLimit: ReadableLogLife,
                shared: true)
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(paths.Logs, "d47-.jsonl"),
                fileSizeLimitBytes: MostBytesPerDay,
                rollOnFileSizeLimit: false,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                retainedFileTimeLimit: MachineLogLife,
                shared: true)
            .WriteTo.Console(outputTemplate: HumanTemplate)
            .CreateLogger();
    }
}
