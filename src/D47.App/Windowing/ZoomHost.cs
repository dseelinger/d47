using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using System.Globalization;

namespace D47.App.Windowing;

/// <summary>
/// Ctrl and the scroll wheel, Ctrl+plus, Ctrl+minus, Ctrl+0 (Phase 9, "Zoom the
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

    private ScrollViewer? _viewport;
    private Control? _content;

    private ZoomHost(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>The level as it currently stands, snapped to the ladder.</summary>
    public int Percent => ZoomLadder.Snap(_settings.Current.Ui.ZoomPercent);

    /// <summary>
    /// Draws a dialog at the size the window that opened it is drawn at
    /// (remediation.md 11, item 11).
    /// <para>
    /// <b>The class comment already said this and only one window ever got it.</b> Zoom was
    /// attached to a window rather than built into one precisely so it would not stop at the
    /// panel's edge — and then <c>MainWindow</c> was the only caller, so every dialog opened over
    /// a zoomed panel came up at 100% and read as a different application's window. Settings
    /// escaped it by becoming a tab rather than by being fixed.
    /// </para>
    /// <para>
    /// The level is copied rather than followed: a dialog is modal, so nothing can change the
    /// zoom while one is open, and a dialog that bound to the setting would be a second thing to
    /// keep in step for a state that cannot arise. The gestures are not bound either — Ctrl and
    /// the wheel inside a modal would change a level the Commander cannot see the effect of until
    /// they close it.
    /// </para>
    /// </summary>
    public static void Match(Window dialog, Window? owner)
    {
        if (owner is null
            || !Hosts.TryGetValue(owner, out var host)
            || host.Percent == 100
            || dialog.Content is not Control content)
        {
            return;
        }

        var scale = host.Percent / 100d;

        // Detached first: a control belongs to exactly one logical tree, and handing it straight
        // to a new parent throws rather than reparenting.
        dialog.Content = null;

        dialog.Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new LayoutTransformControl
            {
                LayoutTransform = new ScaleTransform(scale, scale),
                Child = content,
            },
        };

        // The window was sized for its content at 100%, so it has to grow with it or the dialog
        // opens showing a scaled corner of itself.
        if (!double.IsNaN(dialog.Width))
        {
            dialog.Width *= scale;
        }

        if (!double.IsNaN(dialog.Height))
        {
            dialog.Height *= scale;
        }
    }

    /// <summary>
    /// Wraps the window's content in a scaling host and binds the four gestures.
    /// <para>
    /// Handlers are added at the tunnel stage on purpose. A <see cref="ScrollViewer"/> handles
    /// the wheel and a <see cref="TextBox"/> handles the keys, and both sit below the window in
    /// the tree — bubbling would mean the transcript scrolls instead of zooming, which is the
    /// exact gesture collision this borrows the browser's answer for.
    /// </para>
    /// </summary>
    /// <summary>
    /// Which window carries which host, so a dialog can be drawn at the same size as the window
    /// that opened it (remediation.md 11, item 11).
    /// <para>
    /// Weak, so a closed window is collected rather than kept alive by a table nobody empties.
    /// </para>
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, ZoomHost> Hosts = [];

    public static ZoomHost Attach(Window window, SettingsService settings)
    {
        var host = new ZoomHost(settings);

        Hosts.AddOrUpdate(window, host);

        if (window.Content is Control content)
        {
            // Detached first: a control belongs to exactly one logical tree, and handing it
            // straight to the new parent throws rather than reparenting.
            window.Content = null;

            // Horizontal only — vertical is left to whatever inside the window already
            // scrolls, so a transcript still scrolls itself and the settings footer stays put
            // at the bottom. The sideways scroll is the escape hatch for content with a real
            // minimum width, not the normal case: see FitToViewport.
            var viewport = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new LayoutTransformControl
                {
                    LayoutTransform = host._scale,
                    Child = content,
                },
            };

            host._viewport = viewport;
            host._content = content;

            // A ScrollViewer that can scroll sideways measures its child with infinite width,
            // and everything downstream believes it. That is what this has to undo on every
            // viewport change.
            viewport.PropertyChanged += (_, change) =>
            {
                if (change.Property == ScrollViewer.ViewportProperty)
                {
                    host.FitToViewport();
                }
            };

            window.Content = viewport;
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

        FitToViewport();
    }

    /// <summary>
    /// Gives the layout a width to lay out against, which the scrolling host otherwise takes
    /// away.
    /// <para>
    /// <b>This is what makes the panel responsive at all.</b> A <see cref="ScrollViewer"/> with
    /// horizontal scrolling enabled measures its child with infinite available width — that is
    /// what "may scroll sideways" means to a measure pass — so <c>TextWrapping="Wrap"</c> below
    /// it has nothing to wrap against and never wraps. The transcript becomes one line as long
    /// as the longest thing said, the content is as wide as that line, and the window is a
    /// window you can drag wider forever without ever seeing the end of the sentence. Reported
    /// as "at 100% zoom the main window doesn't fit in HD", which is the visible half of it.
    /// </para>
    /// <para>
    /// Divided by the scale because the constraint belongs on the *unscaled* layout: at 150%
    /// the child is laid out at two thirds of the viewport and then drawn half again as large,
    /// which is a browser's text zoom — bigger text, rewrapped, still inside the window. What
    /// survives is the sideways scroll for content that genuinely will not fit, since a child
    /// with a MinWidth above this simply exceeds it and the scrollbar comes back.
    /// </para>
    /// </summary>
    private void FitToViewport()
    {
        if (_viewport is null || _content is null)
        {
            return;
        }

        var available = _viewport.Viewport.Width;

        // Before the first layout pass there is no viewport to fit. Constraining to zero would
        // measure the panel at nothing at all, which is not a smaller panel but an absent one.
        _content.MaxWidth = available > 0
            ? available / ZoomLadder.ScaleOf(Percent)
            : double.PositiveInfinity;
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
