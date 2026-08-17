using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using D47.App.Theming;
using Microsoft.Extensions.Logging;

namespace D47.App;

public partial class App(AppHost? host) : Application
{
    /// <summary>The designer constructs the application with no host.</summary>
    public App() : this(host: null)
    {
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Before the first window: the palette is application-level, and a window that opens
        // ahead of it would paint once in the wrong colours.
        if (host is not null)
        {
            new ThemeManager(this, host.Loggers.CreateLogger<ThemeManager>()).FollowSettings(host.Settings);
        }

        MainWindow? window = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = window = new MainWindow(host);
        }

        // After the framework is up, because the headset path rasterises a widget tree and
        // needs a dispatcher to do it on. Unconditional: a machine with no headset gets the
        // same code path and the Unavailable state, rather than a branch that only runs for
        // Commanders who have one (list.md Phase 9, "Order agnostic Overlay").
        if (host is not null)
        {
            host.Vr = Headset.VrHost.Start(
                host.Panel,
                host.Audio,
                host.Settings,
                host.ViewState,
                host.Tick,
                host.Paths,
                host.Loggers,
                host.Avatars,
                host.Paths.Data,

                // The same builder the window uses, which is the point: two of them would be two
                // lists of what a settings surface needs wired to it, and the headset's would be
                // the one that quietly fell behind. It hands back a new instance per call, which
                // it has to — a Visual belongs to exactly one visual tree.
                window is null ? null : window.BuildSettingsPage);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
