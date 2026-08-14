using Avalonia;

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
