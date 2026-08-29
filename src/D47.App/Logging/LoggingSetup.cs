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
/// <para>
/// <b>The retention here is one of the four rules the retention policy states</b>
/// (<a href="https://github.com/dseelinger/d47/issues/168">#168</a>,
/// <c>docs/data-retention.md</c>). The numbers live in this file and the document quotes them;
/// changing one here is changing the promise there.
/// </para>
/// </summary>
public static class LoggingSetup
{
    private const string HumanTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// How long the readable log is kept. <b>This is the file a bug report quotes and the file an
    /// incident excerpt cuts its log half out of</b> — <see cref="LogTail"/> reads
    /// <c>d47-*.log</c> and nothing reads the JSON copy — so it is the half whose reach is worth
    /// buying, and 0.19 MB a day makes it nearly free.
    /// </summary>
    public static readonly TimeSpan ReadableLogLife = TimeSpan.FromDays(90);

    /// <summary>
    /// How long the machine-parsing copy is kept. <b>Deliberately shorter than the readable one.</b>
    /// Measured over a real install it is 63% of the bytes the two sinks hold between them, and it
    /// is the copy nobody reads on a bug report; keeping <c>.log</c> long and <c>.jsonl</c> short
    /// buys most of the diagnostic reach for a third of the pile.
    /// </summary>
    public static readonly TimeSpan MachineLogLife = TimeSpan.FromDays(14);

    /// <summary>
    /// The most either sink may write in one day.
    /// <para>
    /// <b>A time limit alone is not a bound.</b> A median day is 0.49 MB across both sinks and the
    /// largest measured day is 0.36 MB, but <see cref="LogTail"/> already carries a comment about
    /// "a day with a runaway loop in it" and one has happened — the implausible
    /// <c>Heard 4181.5s</c>. Ninety days multiplied by an unbounded day is unbounded, so each day
    /// stops at roughly thirty times the largest real one and the pile has a ceiling that can be
    /// stated: 360 MB of <c>.log</c> and 56 MB of <c>.jsonl</c>, against a typical 16 and 4.
    /// </para>
    /// <para>
    /// <b>It does not roll on to a second file</b>, which is the point. Rolling would let a
    /// runaway day multiply itself instead of stopping, and a day limit that a day can exceed is
    /// the thing this exists to prevent.
    /// </para>
    /// </summary>
    public const long MostBytesPerDay = 4L * 1024 * 1024;

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

        // **By time rather than by count** (#168). `retainedFileCountLimit: 14` stood here since
        // Phase 1 and meant fourteen *files* — a fortnight of daily play, and less than that
        // whenever the sink rolled more often than the calendar. A time limit says what was meant.
        // The count limit goes rather than sitting alongside: two retention rules on one sink is
        // two numbers to keep in step, and the one that would bite first is the one nobody wrote
        // down.
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
