using D47.Core;
using D47.Core.Capabilities.Builtin;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace D47.App.Timekeeping;

/// <summary>
/// The timers and alarms capability's services, composed only for a run started with the switch (#90).
/// </summary>
public sealed class TimersAndAlarms
{
    /// <summary>Set this to <c>1</c> to register timers and alarms for one run.</summary>
    public const string EnvironmentVariable = "D47_UTILITIES";

    /// <summary>The same switch on the command line.</summary>
    public const string Flag = "--utilities";

    /// <summary>Whether the command line carried <see cref="Flag"/>.</summary>
    internal static bool Switched { get; private set; }

    /// <summary>Reads the command line for <see cref="Flag"/>.</summary>
    public static void ReadCommandLine(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Switched = args.Contains(Flag, StringComparer.Ordinal);
    }

    /// <summary>Whether timers and alarms are on for this process, by the switch or the variable.</summary>
    public static bool Enabled =>
        Switched || Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    private TimersAndAlarms(AlarmStore alarms)
    {
        Alarms = alarms;
        Timekeeper = new Timekeeper(alarms);
    }

    /// <summary>Where the alarms are kept, for the panel to follow and for a hand edit to reach.</summary>
    public AlarmStore Alarms { get; }

    public Timekeeper Timekeeper { get; }

    /// <summary>
    /// The stores, with <c>alarms.json</c> read once; null when <see cref="Enabled"/> is false, in which
    /// case nothing is read or written.
    /// </summary>
    public static TimersAndAlarms? Create(AppPaths paths, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(loggers);

        if (!Enabled)
        {
            return null;
        }

        var alarms = new AlarmStore(
            Path.Combine(paths.Data, "alarms.json"),
            loggers.CreateLogger<AlarmStore>());

        alarms.Poll();

        return new TimersAndAlarms(alarms);
    }

    /// <summary>Both dates and the running reminders for the game-state block; null when there are no clocks.</summary>
    public static string? Live(TimersAndAlarms? clocks, DateTimeOffset now, TimeZoneInfo zone) =>
        clocks is null ? null : UtilitiesCapability.Live(clocks.Timekeeper, now, zone);
}
