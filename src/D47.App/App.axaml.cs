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
                window is null ? null : window.BuildSettingsPage,

                // And the checklist, which the headset copy needs more than the window does: a
                // Window cannot appear here, so this is the only way a Commander in VR sees it
                // (list.md Phase 25).
                host.Checklists,

                // And the clocks, timers and alarms (list.md Phase 24).
                host.Timekeeper,
                host.Alarms,

                // And the fleet (list.md Phase 26), with the suits and the gap beside it
                // (list.md Phase 27).
                host.Ships,
                () => host.GameState.Active,
                host.OnFootPlans,

                // And who to go and unlock next (list.md Phase 28), which is the page whose
                // whole design is for being read where there is no second monitor.
                host.Unlocks,

                // And the long arcs, which ride the checklist tab and therefore reach the headset
                // on exactly the same terms it does (list.md Phase 34).
                host.Goals?.Book,
                host.Goals?.Backfill);

            // And the headset's copy of the panel can be the one asking for a spoken value
            // (list.md Phase 25). Registered beside the window's rather than instead of it: two
            // surfaces, two navigators, and either can have a prompt open.
            var prompts = host.Vr.Prompts;

            host.RoutePrompts(heard =>
            {
                if (!prompts.IsListening)
                {
                    return false;
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => prompts.Hear(heard));
                return true;
            });

            // And a spoken phrase moves the headset panel too. Both surfaces, because a phrase
            // has no surface attached to it - see AppHost.Navigate.
            host.RouteNavigation(host.Vr.Nav);

            // Last, because it reports the headset and the headset is brought up above. One line
            // saying what this build is and what it is pointed at, so a log can answer "what was
            // it running" without the Commander being asked (remediation.md 10, item 7).
            host.RecordStartup();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
