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
/// Ctrl and the scroll wheel, Ctrl+plus, Ctrl+minus, Ctrl+0 (Phase 9, "Zoom the desktop window").
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
    /// Draws a dialog at the size the window that opened it is drawn at (remediation.md 11, item 11).
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

        // Detached first: a control belongs to exactly one logical tree, and handing it straight to a new
        // parent throws rather than reparenting.
        dialog.Content = null;

        var viewport = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new LayoutTransformControl
            {
                LayoutTransform = new ScaleTransform(scale, scale),
                Child = content,
            },
        };

        // The same undoing FitToViewport performs for the panel, and for exactly the same reason — see #145.
        viewport.PropertyChanged += (_, change) =>
        {
            if (change.Property == ScrollViewer.ViewportProperty)
            {
                Fit(content, viewport.Viewport.Width, scale);
            }
        };

        dialog.Content = viewport;

        // The window was sized for its content at 100%, so it has to grow with it or the dialog opens showing
        // a scaled corner of itself.
        if (!double.IsNaN(dialog.Width))
        {
            dialog.Width *= scale;
        }

        if (!double.IsNaN(dialog.Height))
        {
            dialog.Height *= scale;
        }
    }

    /// <summary>Gives a scaled layout a width to lay out against.</summary>
    private static void Fit(Control content, double available, double scale)
    {
        if (available <= 0)
        {
            content.MaxWidth = double.PositiveInfinity;
            return;
        }

        var margin = content.Margin.Left + content.Margin.Right;

        content.MaxWidth = Math.Max(0, (available / scale) - margin);
    }

    /// <summary>Wraps the window's content in a scaling host and binds the four gestures.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, ZoomHost> Hosts = [];

    public static ZoomHost Attach(Window window, SettingsService settings)
    {
        var host = new ZoomHost(settings);

        Hosts.AddOrUpdate(window, host);

        if (window.Content is Control content)
        {
            // Detached first: a control belongs to exactly one logical tree, and handing it straight to the
            // new parent throws rather than reparenting.
            window.Content = null;

            // Horizontal only — vertical is left to whatever inside the window already scrolls, so a
            // transcript still scrolls itself and the settings footer stays put at the bottom.
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

            // A ScrollViewer that can scroll sideways measures its child with infinite width, and everything
            // downstream believes it.
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

    /// <summary>Steps one rung and persists it.</summary>
    public void Set(int percent)
    {
        var snapped = ZoomLadder.Snap(percent);

        // Through the settings service rather than straight onto the transform: this is a hotkey reaching a
        // settings row, which is a caller the service already knows about, and going around it would leave
        // the settings surface showing a stale number.
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
    /// Gives the layout a width to lay out against, which the scrolling host otherwise takes away.
    /// </summary>
    private void FitToViewport()
    {
        if (_viewport is null || _content is null)
        {
            return;
        }

        Fit(_content, _viewport.Viewport.Width, ZoomLadder.ScaleOf(Percent));
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

        // Both spellings of each key.
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
