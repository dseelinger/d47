using D47.Core;
using D47.Core.Diagnostics;
using Serilog;

namespace D47.App.Logging;

/// <summary>One readable log file beside the executable, and the console.</summary>
public static class LoggingSetup
{
    private const string HumanTemplate =
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>How long the readable log is kept.</summary>
    public static readonly TimeSpan ReadableLogLife = TimeSpan.FromDays(90);

    /// <summary>The most the log may write in one day.</summary>
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
            // Every sink writes on the wrapper's own thread, never the caller's: a shared-file write waits up
            // to ten seconds on a named mutex any process can hold, and the tick logs (#915). A full buffer
            // drops events rather than block. Disposing the logger drains the buffer.
            .WriteTo.Async(sinks =>
            {
                sinks.File(
                    Path.Combine(paths.Logs, "d47-.log"),
                    outputTemplate: HumanTemplate,
                    fileSizeLimitBytes: MostBytesPerDay,
                    rollOnFileSizeLimit: false,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: null,
                    retainedFileTimeLimit: ReadableLogLife,
                    shared: true);
                sinks.Console(outputTemplate: HumanTemplate);
            })
            .CreateLogger();
    }
}
