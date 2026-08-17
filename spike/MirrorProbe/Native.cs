using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MirrorProbe;

/// <summary>
/// Window enumeration and the GDI capture path. Nothing here is WinRT, which is the point of
/// having it: it is the control against which the Windows Graphics Capture result is read.
/// </summary>
internal static partial class Native
{
    // Window styles, only the ones that say something about a display mode.
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_THICKFRAME = 0x00040000;
    private const long WS_POPUP = 0x80000000;
    private const long WS_BORDER = 0x00800000;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    internal const int SRCCOPY = 0x00CC0020;
    internal const int CAPTUREBLT = 0x40000000;

    /// <summary>Renders the whole window including what DWM has cached, not only what is on top.</summary>
    internal const uint PW_RENDERFULLCONTENT = 2;

    internal const uint DIB_RGB_COLORS = 0;
    internal const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;

        public override readonly string ToString() => $"{Left},{Top} {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int bmiColors;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hwnd);

    // These two stay on DllImport. LibraryImport will not marshal a char[] without the whole
    // assembly opting out of runtime marshalling, which is a large change to make for two calls
    // that copy a window title.
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, [Out] char[] text, int count);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, [Out] char[] text, int count);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(IntPtr hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetDC(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);

    [LibraryImport("shcore.dll")]
    internal static partial int SetProcessDpiAwareness(int value);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateCompatibleDC(IntPtr dc);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(IntPtr dc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(
        IntPtr destDc,
        int x,
        int y,
        int width,
        int height,
        IntPtr sourceDc,
        int sourceX,
        int sourceY,
        int rop);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetDIBits(
        IntPtr dc,
        IntPtr bitmap,
        uint start,
        uint lines,
        IntPtr bits,
        ref BITMAPINFO info,
        uint usage);

    /// <summary>One top-level window, with everything that bears on how it can be captured.</summary>
    internal sealed record WindowInfo(
        IntPtr Handle,
        string Title,
        string ClassName,
        string ProcessName,
        uint ProcessId,
        RECT WindowRect,
        RECT ClientRect,
        RECT MonitorRect,
        long Style,
        long ExStyle)
    {
        /// <summary>
        /// A description of how the window is framed, which is as far as styles and rectangles can
        /// honestly go. They cannot tell borderless fullscreen from exclusive fullscreen — both are
        /// a popup covering the monitor exactly. What separates those two is whether a GDI screen
        /// blit comes back black, and that is a capture result rather than a window property, so it
        /// is reported beside the frames instead of being guessed at here.
        /// </summary>
        public string Framing
        {
            get
            {
                var decorated = (Style & (WS_CAPTION | WS_THICKFRAME | WS_BORDER)) != 0;

                var covers = WindowRect.Left <= MonitorRect.Left
                    && WindowRect.Top <= MonitorRect.Top
                    && WindowRect.Right >= MonitorRect.Right
                    && WindowRect.Bottom >= MonitorRect.Bottom;

                return (decorated, covers) switch
                {
                    (false, true) => "undecorated, covering the monitor (borderless or exclusive)",
                    (false, false) => "undecorated, not covering the monitor",
                    (true, true) => "decorated, covering the monitor",
                    (true, false) => "decorated window",
                };
            }
        }

        public bool IsPopup => (Style & WS_POPUP) != 0;
    }

    internal static IReadOnlyList<WindowInfo> TopLevelWindows()
    {
        var found = new List<WindowInfo>();
        var title = new char[512];
        var className = new char[256];

        EnumWindows(
            (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                if (!GetWindowRect(hwnd, out var rect) || rect.Width <= 1 || rect.Height <= 1)
                {
                    return true;
                }

                var titleLength = GetWindowText(hwnd, title, title.Length);
                var classLength = GetClassName(hwnd, className, className.Length);

                GetClientRect(hwnd, out var client);
                GetWindowThreadProcessId(hwnd, out var pid);

                var monitor = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref monitor);

                found.Add(new WindowInfo(
                    hwnd,
                    new string(title, 0, titleLength),
                    new string(className, 0, classLength),
                    ProcessName(pid),
                    pid,
                    rect,
                    client,
                    monitor.rcMonitor,
                    GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64(),
                    GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64()));

                return true;
            },
            IntPtr.Zero);

        return found;
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "?";
        }
        catch (InvalidOperationException)
        {
            return "?";
        }
    }

    internal static string Describe(WindowInfo window) => new StringBuilder()
        .Append(window.ProcessName)
        .Append("  \"")
        .Append(window.Title)
        .Append("\"  [")
        .Append(window.ClassName)
        .Append("]  ")
        .Append(window.WindowRect)
        .Append("  ")
        .Append(window.Framing)
        .ToString();
}
