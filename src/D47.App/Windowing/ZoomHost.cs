using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using System.Globalization;

namespace D47.App.Windowing;

/// <summary>
/// Ctrl and the scroll wheel, Ctrl+plus, Ctrl+minus, Ctrl+0 (list.md Phase 9, "Zoom the
/// desktop window"). Attached to a window rather than built into one, because the settings
/// window is the same widget tree and shipping a zoom that stops at the panel's edge would be
/// a zoom the Commander has to remember the boundaries of.
/// <para>
/// A <see cref="LayoutTransformControl"/>, not a render transform. The difference is the whole
/// requirement: a render transform scales the finished picture, so at 150% the panel is drawn
/// larger and then clipped by a window that never heard about it. A layout transform re-runs
/// measure and arrange at the scaled size, which is what a browser does — text rewraps,
/// spacing scales with it, and the window's own scrollbars still mean what they meant.
/// </para>
/// <para>
/// The level lives in settings, so it is one value shared by every window and it survives a
/// restart. Nothing here holds a level of its own to disagree with it.
/// </para>
/// </summary>
public sealed class ZoomHost
{
    private readonly SettingsService _settings;
    private readonly ScaleTransform _scale = new();

    private ZoomHost(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>The level as it currently stands, snapped to the ladder.</summary>
    public int Percent => ZoomLadder.Snap(_settings.Current.Ui.ZoomPercent);

    /// <summary>
    /// Wraps the window's content in a scaling host and binds the four gestures.
    /// <para>
    /// Handlers are added at the tunnel stage on purpose. A <see cref="ScrollViewer"/> handles
    /// the wheel and a <see cref="TextBox"/> handles the keys, and both sit below the window in
    /// the tree — bubbling would mean the transcript scrolls instead of zooming, which is the
    /// exact gesture collision this borrows the browser's answer for.
    /// </para>
    /// </summary>
    public static ZoomHost Attach(Window window, SettingsService settings)
    {
        var host = new ZoomHost(settings);

        if (window.Content is Control content)
        {
            // Detached first: a control belongs to exactly one logical tree, and handing it
            // straight to the new parent throws rather than reparenting.
            window.Content = null;

            window.Content = new LayoutTransformControl
            {
                LayoutTransform = host._scale,
                Child = content,
            };
        }

        host.ApplyCurrent();

        settings.Changed += change =>
        {
            if (change.Key == InterfaceCapability.ZoomKey)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(host.ApplyCurrent);
            }
        };

        window.AddHandler(InputElement.PointerWheelChangedEvent, host.OnWheel, RoutingStrategies.Tunnel);
        window.AddHandler(InputElement.KeyDownEvent, host.OnKeyDown, RoutingStrategies.Tunnel);

        return host;
    }

    /// <summary>Steps one rung and persists it. Public so a caller with no gesture can use it.</summary>
    public void Set(int percent)
    {
        var snapped = ZoomLadder.Snap(percent);

        // Through the settings service rather than straight onto the transform: this is a
        // hotkey reaching a settings row, which is a caller the service already knows about,
        // and going around it would leave the settings surface showing a stale number.
        _settings.Apply(
            InterfaceCapability.ZoomKey,
            snapped.ToString(CultureInfo.InvariantCulture),
            SettingsCaller.Hotkey);

        ApplyCurrent();
    }

    private void ApplyCurrent()
    {
        var factor = ZoomLadder.ScaleOf(Percent);
        _scale.ScaleX = factor;
        _scale.ScaleY = factor;
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0)
        {
            return;
        }

        e.Handled = true;
        Set(e.Delta.Y > 0 ? ZoomLadder.In(Percent) : ZoomLadder.Out(Percent));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        // Both spellings of each key. OemPlus/OemMinus are the main row, Add/Subtract are the
        // numeric keypad, and a Commander who reaches for the keypad is not doing something
        // different from one who does not.
        var next = e.Key switch
        {
            Key.OemPlus or Key.Add => ZoomLadder.In(Percent),
            Key.OemMinus or Key.Subtract => ZoomLadder.Out(Percent),
            Key.D0 or Key.NumPad0 => ZoomLadder.Default,
            _ => 0,
        };

        if (next == 0)
        {
            return;
        }

        e.Handled = true;
        Set(next);
    }
}
