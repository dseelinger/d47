using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MirrorProbe;

/// <summary>
/// The two GDI capture paths, kept because they are the control.
/// <para>
/// A blit from the screen device context reads what the desktop compositor has on the glass. That
/// works for a borderless window and comes back black for a window that has taken the display
/// exclusively — which is not a defect here, it is the measurement: it is the only cheap way to
/// tell those two display modes apart from outside the game, and the spike has to report which of
/// them Elite was in.
/// </para>
/// </summary>
internal static class GdiCapture
{
    /// <summary>Blits the window's screen rectangle off the desktop.</summary>
    internal static Mat? FromScreen(Native.WindowInfo window)
    {
        if (!Native.GetWindowRect(window.Handle, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        var screen = Native.GetDC(IntPtr.Zero);

        try
        {
            return Blit(
                screen,
                rect.Width,
                rect.Height,
                (memoryDc, width, height) => Native.BitBlt(
                    memoryDc,
                    0,
                    0,
                    width,
                    height,
                    screen,
                    rect.Left,
                    rect.Top,
                    Native.SRCCOPY | Native.CAPTUREBLT));
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>
    /// Asks the window to render itself, including the parts DWM has cached rather than only what
    /// is visible. Direct3D content frequently declines and returns an empty frame; that outcome is
    /// reported rather than treated as an error.
    /// </summary>
    internal static Mat? FromPrintWindow(Native.WindowInfo window)
    {
        if (!Native.GetWindowRect(window.Handle, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        var screen = Native.GetDC(IntPtr.Zero);

        try
        {
            return Blit(
                screen,
                rect.Width,
                rect.Height,
                (memoryDc, _, _) => Native.PrintWindow(window.Handle, memoryDc, Native.PW_RENDERFULLCONTENT));
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private static Mat? Blit(IntPtr screen, int width, int height, Func<IntPtr, int, int, bool> draw)
    {
        var memoryDc = Native.CreateCompatibleDC(screen);
        var bitmap = Native.CreateCompatibleBitmap(screen, width, height);
        var previous = Native.SelectObject(memoryDc, bitmap);

        try
        {
            if (!draw(memoryDc, width, height))
            {
                return null;
            }

            return ReadBits(memoryDc, bitmap, width, height);
        }
        finally
        {
            Native.SelectObject(memoryDc, previous);
            Native.DeleteObject(bitmap);
            Native.DeleteDC(memoryDc);
        }
    }

    private static Mat? ReadBits(IntPtr dc, IntPtr bitmap, int width, int height)
    {
        var info = new Native.BITMAPINFO
        {
            bmiHeader = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                biWidth = width,

                // Negative for a top-down image, which is the row order OpenCV expects.
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Native.BI_RGB,
            },
        };

        var bgra = new Mat(height, width, MatType.CV_8UC4);

        var read = Native.GetDIBits(dc, bitmap, 0, (uint)height, bgra.Data, ref info, Native.DIB_RGB_COLORS);

        if (read == 0)
        {
            bgra.Dispose();
            return null;
        }

        try
        {
            var bgr = new Mat();
            Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
            return bgr;
        }
        finally
        {
            bgra.Dispose();
        }
    }
}
