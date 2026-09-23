using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Windowing;

/// <summary>
/// Paints the native 1px window border in a theme colour and repaints it when the theme changes.
/// Windows draws no border on a maximised window.
/// </summary>
public static class WindowBorder
{
    /// <summary>DWMWA_BORDER_COLOR, Windows 11 build 22000 and later.</summary>
    private const uint BorderColor = 34;

    /// <summary>Paints the border in the brush the resource <paramref name="colourKey"/> names.</summary>
    public static void Apply(Window window, string colourKey)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        window.Opened += (_, _) => Paint(window, colourKey);
        window.GetResourceObservable(colourKey)
            .Subscribe(new Avalonia.Reactive.AnonymousObserver<object?>(_ => Paint(window, colourKey)));
    }

    private static void Paint(Window window, string colourKey)
    {
        if (window.TryGetPlatformHandle() is not { } handle
            || !window.TryFindResource(colourKey, out var value)
            || value is not ISolidColorBrush brush)
        {
            return;
        }

        // COLORREF is 0x00BBGGRR.
        var colour = brush.Color;
        var colorref = (uint)(colour.B << 16 | colour.G << 8 | colour.R);

        _ = DwmSetWindowAttribute(handle.Handle, BorderColor, ref colorref, sizeof(uint));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref uint value, int size);
}
