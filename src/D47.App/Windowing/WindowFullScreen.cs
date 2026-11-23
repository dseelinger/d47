using Avalonia;
using Avalonia.Controls;
using D47.App.Panel;

namespace D47.App.Windowing;

/// <summary>
/// Puts the window over the whole monitor it is on while its panel draws a full-screen control, and back to the
/// state, size and position it had when the control goes.
/// </summary>
public static class WindowFullScreen
{
    public static void Follow(Window window, PanelView panel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(panel);

        var before = WindowState.Normal;
        var position = default(PixelPoint);
        var size = default(Size);

        panel.FullScreenChanged += on =>
        {
            if (on)
            {
                if (window.WindowState == WindowState.FullScreen)
                {
                    return;
                }

                before = window.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
                position = window.Position;
                size = new Size(window.Width, window.Height);
                window.WindowState = WindowState.FullScreen;
            }
            else if (window.WindowState == WindowState.FullScreen)
            {
                window.WindowState = before;

                if (before == WindowState.Normal)
                {
                    window.Position = position;
                    window.Width = size.Width;
                    window.Height = size.Height;
                }
            }
        };
    }
}
