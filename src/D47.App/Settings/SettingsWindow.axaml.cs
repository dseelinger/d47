using Avalonia.Controls;
using Avalonia.Input;
using D47.Core;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// The desktop host for <see cref="SettingsView"/>. One window at a time — a second copy of a
/// surface with no save button is two views of the same live state, which is confusing rather
/// than useful.
/// </summary>
public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    /// <summary>Binds the hosted view. Public for the headless UI tests, which host it the same way.</summary>
    public void Attach(
        SettingsService settings,
        ViewStateStore viewState,
        AppPaths paths,
        Func<D47.Core.Coverage.CoverageReport>? coverage = null) =>
        View.Attach(settings, viewState, paths, coverage);

    public static void Show(
        Window owner,
        SettingsService settings,
        ViewStateStore viewState,
        AppPaths paths,
        Func<D47.Core.Coverage.CoverageReport>? coverage = null)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths, coverage);

        // The same widget tree, so the same zoom. A level that applied to the panel and not to
        // settings would be a level the Commander has to discover the edge of.
        var zoom = Windowing.ZoomHost.Attach(window, settings);

        // Opens at a size that fits the screen and remembers where it was left, the same as the
        // main window. Without this it asked for its declared size unconditionally: 720 tall is
        // more than the work area of a 1080p screen at 150% scaling, so the window opened taller
        // than the desktop and the last rows were unreachable. Attached before Show, because
        // resizing afterwards is a visible jump.
        Windowing.WindowPlacementMemory.Attach(
            window, viewState, D47.Core.Configuration.WindowSlot.Settings, () => zoom.Percent);
        window.Closed += (_, _) => _open = null;

        _open = window;
        window.Show(owner);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Escape closes. There is nothing to discard: every change on this surface has already
        // been applied and persisted.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }

        base.OnKeyDown(e);
    }
}
