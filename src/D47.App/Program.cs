using Avalonia;

namespace D47.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = AppHost.Start();
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
