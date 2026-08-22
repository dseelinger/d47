using Avalonia;
using Serilog;

namespace D47.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Before even the single-instance claim: the selftest is a headless plumbing check that
        // a release workflow runs while a Commander's own d47 may be open, and it must neither
        // fight that copy for the mutex nor surface it.
        if (args.Contains(SelfTest.Flag, StringComparer.Ordinal))
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }

        // A crash must not be silent. Without this the process dies, the ProcessExit handler
        // still runs, and the log ends "stopped cleanly" - so an unhandled exception is
        // indistinguishable from the Commander closing the window, and the only stack trace is
        // in the Windows Application event log. That is how 0.52.3's startup crash reached a
        // Commander before anybody could tell it apart from d47 doing nothing at all.
        //
        // Installed before anything that can throw, but Serilog is configured inside
        // AppHost.Start: a failure above that line still lands nowhere, and everything after it
        // is covered. Flushing here is what makes the record survive, and it also silences the
        // logger for the "stopped cleanly" line ProcessExit is about to write - which is the
        // right outcome, since that line would be a lie.
        AppDomain.CurrentDomain.UnhandledException += (_, crash) =>
        {
            if (crash.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "d47 is going down on an unhandled exception");
            }
            else
            {
                Log.Fatal("d47 is going down on {Thrown}, which is not an exception", crash.ExceptionObject);
            }

            Log.CloseAndFlush();
        };

        // Before anything else, and before the host in particular: the host tails the journal,
        // opens the microphone and registers global hotkeys, none of which a second copy should
        // be doing. Claiming the slot first means a second copy costs a mutex and exits, rather
        // than starting all of that and then discovering it was not wanted.
        using var only = SingleInstance.Claim();

        if (only is null)
        {
            // Somebody clicked the shortcut and something has to happen; showing them the copy
            // they already have is the only useful answer.
            SingleInstance.SurfaceRunningCopy();
            return;
        }

        using var host = AppHost.Start();

        // Handed over when an accepted update starts the build that replaces this one.
        host.ReleaseSingleInstance = only.ReleaseForSuccessor;

        Build(host).StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Parameterless overload the Avalonia designer resolves by name.</summary>
    public static AppBuilder BuildAvaloniaApp() => Build(host: null);

    private static AppBuilder Build(AppHost? host) =>
        AppBuilder.Configure(() => new App(host))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
