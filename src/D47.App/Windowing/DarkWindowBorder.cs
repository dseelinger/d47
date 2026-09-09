using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Windowing;

/// <summary>
/// Paints the native frame's border to match the theme (reported 2026-08-31: a bright line under a
/// dialog's buttons, there at every size and gone only maximised — which is the tell, because maximised
/// is the one state that sheds the resize frame).
/// </summary>
public static class DarkWindowBorder
{
    /// <summary>DWMWA_BORDER_COLOR, Windows 11 build 22000 and later.</summary>
    private const uint BorderColor = 34;

    /// <summary>Matches the border to the window.</summary>
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

        // COLORREF is 0x00BBGGRR.
        var colour = (window.Background as ISolidColorBrush)?.Color ?? Color.FromRgb(16, 16, 16);
        var colorref = (uint)(colour.B << 16 | colour.G << 8 | colour.R);

        _ = DwmSetWindowAttribute(handle.Handle, BorderColor, ref colorref, sizeof(uint));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref uint value, int size);
}
