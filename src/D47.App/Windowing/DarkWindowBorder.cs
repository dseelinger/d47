using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Windowing;

/// <summary>
/// Paints the native frame's border to match the theme (reported 2026-08-31: a bright line
/// under a dialog's buttons, there at every size and gone only maximised — which is the tell,
/// because maximised is the one state that sheds the resize frame).
/// <para>
/// Windows 11 draws a window's one-pixel border in its own colour rather than the app's, and
/// against d47's dark chrome it reads as a stray divider <em>inside</em> the window.
/// <c>DWMWA_BORDER_COLOR</c> is the documented dial for exactly this. On Windows 10 the
/// attribute is unknown and the call fails without consequence, which is the whole of the
/// error handling this needs — a window that keeps the system border is the situation today.
/// </para>
/// </summary>
public static class DarkWindowBorder
{
    /// <summary>DWMWA_BORDER_COLOR, Windows 11 build 22000 and later.</summary>
    private const uint BorderColor = 34;

    /// <summary>
    /// Matches the border to the window. Subscribes rather than acting, because the native
    /// handle does not exist until the window opens — and paints at once as well, for a caller
    /// whose window already has.
    /// </summary>
    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened += (_, _) => Paint(window);
        Paint(window);
    }

    private static void Paint(Window window)
    {
        if (window.TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        // COLORREF is 0x00BBGGRR. The window's own background where it is a plain colour, so
        // the border continues the surface rather than approximating it; the fallback is the
        // dark the theme is built around.
        var colour = (window.Background as ISolidColorBrush)?.Color ?? Color.FromRgb(16, 16, 16);
        var colorref = (uint)(colour.B << 16 | colour.G << 8 | colour.R);

        _ = DwmSetWindowAttribute(handle.Handle, BorderColor, ref colorref, sizeof(uint));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref uint value, int size);
}
