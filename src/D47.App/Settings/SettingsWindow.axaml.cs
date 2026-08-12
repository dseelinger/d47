using Avalonia.Controls;
using Avalonia.Input;
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
    public void Attach(SettingsService settings, ViewStateStore viewState) => View.Attach(settings, viewState);

    public static void Show(Window owner, SettingsService settings, ViewStateStore viewState)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var window = new SettingsWindow();
        window.Attach(settings, viewState);
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
