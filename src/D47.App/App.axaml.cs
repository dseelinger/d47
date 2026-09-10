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
        // Before the first window: the palette is application-level, and a window that opens ahead of it
        // would paint once in the wrong colours.
        if (host is not null)
        {
            new ThemeManager(this, host.Loggers.CreateLogger<ThemeManager>()).FollowSettings(host.Settings);
        }

        MainWindow? window = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = window = new MainWindow(host);

            // Closing the window is how d47 is quit — there is no tray icon — and that has been true
            // only by accident: the default is to shut down when the last window closes, and d47 had
            // exactly one.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }

        // After the framework is up, because the headset path rasterises a widget tree and needs a dispatcher
        // to do it on.
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

                // The same builder the window uses, which is the point: two of them would be two lists of
                // what a settings surface needs wired to it, and the headset's would be the one that silently
                // fell behind.
                window is null ? null : window.BuildSettingsPage,

                // And the checklist, which the headset copy needs more than the window does: a Window cannot
                // appear here, so this is the only way a Commander in VR sees it (Phase 25).
                host.Checklists,

                // And the clocks, timers and alarms (Phase 24).
                host.Timekeeper,
                host.Alarms,

                // And the fleet (Phase 26), with the suits and the gap beside it (Phase 27).
                host.Ships,
                () => host.GameState.Active,
                host.OnFootPlans,

                // And who to go and unlock next (Phase 28), which is the page whose whole design is for being
                // read where there is no second monitor.
                host.Unlocks,

                // And the long arcs, which ride the checklist tab and therefore reach the headset on exactly
                // the same terms it does (Phase 34).
                host.Goals?.Book,
                host.Goals?.Backfill,

                // And the stories (Phase 47), from 2026-08-22.
                window?.Adventures,

                // And where to buy what a build still needs (Phase 50), on the same terms as the window's copy
                // (#54).
                host.Capabilities,
                host.Sourcing,
                host.Carrier,

                // And where the Commander is going (Phase 37), from 2026-09-09 (#52): the window's own
                // record, so the headset's copy of the tab cannot fall behind it.
                window?.Routing,

                // And the fleet's own arithmetic (Phase 27) and its hull-art switch (#53), the same store the
                // window's copy reads so the switch is not left in two places at once.
                () => host.ModulePower,
                new Panel.ShipsDrawingsMemory(host.ViewState));

            // And the headset's copy of the panel can be the one asking for a spoken value (Phase 25), or
            // the one with a keyboard up for a value to be spelled onto (#51).
            var prompts = host.Vr.Prompts;
            var board = host.Vr.Board;

            host.RoutePrompts(heard =>
            {
                var taking = prompts.IsListening ? (D47.Core.Interface.IHearsText)prompts
                    : board.IsListening ? board
                    : null;

                if (taking is null)
                {
                    return false;
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => taking.Hear(heard));
                return true;
            });

            // And a spoken phrase moves the headset panel too.
            var ui = Avalonia.Threading.Dispatcher.UIThread;
            host.RouteNavigation(host.Vr.Nav, move => ui.Post(move));

            // And a spoken "page down" moves the headset panel (#34) — the surface the request was made from,
            // where a ray on a twelve-pixel bar is the only alternative.
            host.RouteScrolling(host.Vr.Scroll);

            // And the third surface: the mini panel on a monitor, for a Commander with no headset (Phase 48).
            host.Overlay = Windowing.OverlayPanel.Attach(
                host.Panel,
                host.Settings,
                host.ViewState,
                host.Tick,
                host.Elite,
                host.Loggers.CreateLogger<Windowing.OverlayPanel>(),
                host.Avatars,
                window?.Adventures,

                // The headset's pages, minus Settings (asked for 2026-08-24: "it should have the same tabs as
                // the VR mini panel, including Checklist").
                new Windowing.OverlayTabs
                {
                    Checklists = host.Checklists,
                    Goals = host.Goals?.Book,
                    BackfillGoals = host.Goals?.Backfill,
                    Unlocks = host.Unlocks,
                    Ships = host.Ships,
                    GameState = () => host.GameState.Active,
                    OnFoot = host.OnFootPlans,
                    Timekeeper = host.Timekeeper,
                    Alarms = host.Alarms,
                });

            // Through the same route as the other two (Phase 45).
            host.RouteNavigation(host.Overlay.Nav, move => ui.Post(move));

            // And the strip, which has no other way to scroll at all: the pointer goes straight through it,
            // so the wheel does too (#34).
            host.RouteScrolling(host.Overlay.Scroll);

            // Last, because it reports the headset and the headset is brought up above.
            host.RecordStartup();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
