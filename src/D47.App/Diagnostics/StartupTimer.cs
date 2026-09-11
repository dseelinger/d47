using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace D47.App.Diagnostics;

/// <summary>
/// How long each step of startup took. A step at or over <see cref="Notable"/> is logged at
/// Information and one below it at Debug (#141).
/// </summary>
internal static class StartupTimer
{
    /// <summary>At or over this, a step is logged at Information rather than Debug.</summary>
    public static readonly TimeSpan Notable = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long this process has been running, which covers the bundle extraction, the JIT and whatever
    /// the virus scanner did before any of d47's own code ran.
    /// </summary>
    public static TimeSpan SinceProcessStart
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return DateTime.Now - process.StartTime;
        }
    }

    /// <summary>Times one step. Dispose it where the step ends.</summary>
    public static IDisposable Step(string name) => new Running(name);

    /// <summary>The same, around a step that produces a value.</summary>
    public static T Time<T>(string name, Func<T> work)
    {
        using (Step(name))
        {
            return work();
        }
    }

    private sealed class Running(string name) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();

        public void Dispose()
        {
            var took = Stopwatch.GetElapsedTime(_started);

            // Resolved on dispose rather than captured, because the step around the host starts before
            // the logger has been configured and ends after.
            Log.ForContext(typeof(StartupTimer)).Write(
                took >= Notable ? LogEventLevel.Information : LogEventLevel.Debug,
                "Startup step {Step} took {Ms} ms",
                name,
                (long)took.TotalMilliseconds);
        }
    }
}
