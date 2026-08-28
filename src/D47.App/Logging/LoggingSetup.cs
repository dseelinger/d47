using D47.Core;
using D47.Core.Diagnostics;
using Serilog;
using Serilog.Formatting.Compact;

namespace D47.App.Logging;

/// <summary>
/// Two sinks, both files beside the executable: one human-readable, one newline-delimited JSON
/// so an agent can parse a session (Phase 1). Nothing here reaches the network. There
/// is no analytics sink, no metrics endpoint and no crash reporter, and provider egress is a
/// separate concern with its own settings row.
/// </summary>
public static class LoggingSetup
{
    private const string HumanTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <param name="technical">
    /// Forwards speech-loop errors to the Technical page as well as to the files. Added here
    /// rather than wrapped around the factory afterwards, because a sink has to be part of the
    /// pipeline to see an event at all — and it is pointed at a panel later, since logging is
    /// built before there is one.
    /// </param>
    public static ILogger Create(
        AppPaths paths,
        SerilogVerbosityControl verbosity,
        TechnicalLogBridge? technical = null)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(verbosity.Default)
            .Enrich.FromLogContext();

        // One controllable target per subsystem, over however many namespaces that subsystem
        // spans. The switch is shared across a subsystem's prefixes rather than copied, so the
        // whole of it moves together — and it is read per event, which is what makes a level
        // change take effect without a restart.
        foreach (var (subsystem, prefixes) in Subsystems.SourcePrefixes)
        {
            var level = verbosity.SwitchFor(subsystem);

            foreach (var prefix in prefixes)
            {
                configuration = configuration.MinimumLevel.Override(prefix, level);
            }
        }

        if (technical is not null)
        {
            configuration = configuration.WriteTo.Sink(technical);
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
