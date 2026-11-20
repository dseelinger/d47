using System.Reflection;
using System.Runtime.InteropServices;
using static D47.App.Windowing.SplashInterop;

namespace D47.App.Windowing;

/// <summary>
/// The splash as a Win32 window on a thread of its own. It never touches Avalonia, so it can be up before the
/// main thread reaches a dispatcher.
/// </summary>
internal sealed class NativeSplash : ISplashSurface
{
    private const string ClassName = "D47Splash";
    private const string FontFace = "JetBrains Mono";
    internal const int DesignWidth = 480;
    internal const int DesignHeight = 360;
    internal const int VersionTop = 278;
    private const int VersionLine = 16;
    private const int VersionSize = 12;
    private const uint VersionColour = 0x00949BA0;

    private readonly string _version;
    private readonly object _gate = new();
    private readonly WndProc _proc;

    private nint _hwnd;
    private bool _closed;
    private nint _surface;
    private nint _font;
    private nint _fontResource;
    private int _width;
    private int _height;
    private double _scale = 1;

    public NativeSplash(string version)
    {
        _version = version;
        _proc = Handle;
    }

    public void Show()
    {
        new Thread(Run) { IsBackground = true, Name = "d47 splash" }.Start();
    }

    public void Close()
    {
        lock (_gate)
        {
            _closed = true;

            if (_hwnd != 0)
            {
                PostMessageW(_hwnd, WmClose, 0, 0);
            }
        }
    }

    public void Dispose()
    {
    }

    private static byte[]? Embedded(string name)
    {
        using var stream = typeof(NativeSplash).Assembly.GetManifestResourceStream(name);

        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static string ImageFor(double scale) => scale <= 1.0 ? "100" : scale <= 1.5 ? "150" : "200";

    private void Run()
    {
        nint gdiplus = 0;
        nint bitmap = 0;
        nint image = 0;

        try
        {
            SetThreadDpiAwarenessContext(PerMonitorAwareV2);

            var primary = MonitorFromPoint(default, MonitorDefaultToPrimary);
            var area = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };

            if (!GetMonitorInfoW(primary, ref area))
            {
                return;
            }

            GetDpiForMonitor(primary, MdtEffectiveDpi, out var dpi, out _);
            _scale = Math.Max(1.0, dpi / 96.0);
            _width = (int)Math.Round(DesignWidth * _scale);
            _height = (int)Math.Round(DesignHeight * _scale);

            var input = new GdiplusStartupInput { Version = 1 };

            if (GdiplusStartup(out gdiplus, ref input, 0) != 0)
            {
                gdiplus = 0;
            }

            if (gdiplus != 0 && Embedded($"D47.Splash.splash-{ImageFor(_scale)}.png") is { } png)
            {
                image = Decode(png);
            }

            if (image == 0)
            {
                return;
            }

            LoadFont();
            bitmap = image;
            BuildSurface(bitmap);

            var instance = GetModuleHandleW(null);
            var window = new WndClassEx
            {
                Size = (uint)Marshal.SizeOf<WndClassEx>(),
                WndProc = Marshal.GetFunctionPointerForDelegate(_proc),
                Instance = instance,
                Cursor = LoadCursorW(0, IdcArrow),
                ClassName = ClassName,
            };

            RegisterClassExW(ref window);

            var x = area.Monitor.Left + (((area.Monitor.Right - area.Monitor.Left) - _width) / 2);
            var y = area.Monitor.Top + (((area.Monitor.Bottom - area.Monitor.Top) - _height) / 2);

            var hwnd = CreateWindowExW(
                WsExToolWindow | WsExNoActivate, ClassName, "d47", WsPopup, x, y, _width, _height, 0, 0, instance, 0);

            if (hwnd == 0)
            {
                return;
            }

            lock (_gate)
            {
                _hwnd = hwnd;

                if (_closed)
                {
                    DestroyWindow(hwnd);
                    return;
                }
            }

            ShowWindow(hwnd, SwShowNoActivate);
            UpdateWindow(hwnd);

            while (GetMessageW(out var message, 0, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessageW(ref message);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or ArgumentException)
        {
            // A splash that cannot be drawn is skipped; startup does not depend on it.
        }
        finally
        {
            if (_surface != 0)
            {
                DeleteObject(_surface);
            }

            if (_font != 0)
            {
                DeleteObject(_font);
            }

            if (_fontResource != 0)
            {
                RemoveFontMemResourceEx(_fontResource);
            }

            if (gdiplus != 0)
            {
                GdiplusShutdown(gdiplus);
            }
        }
    }

    private static nint Decode(byte[] png)
    {
        var handle = GCHandle.Alloc(png, GCHandleType.Pinned);

        try
        {
            var stream = SHCreateMemStream(handle.AddrOfPinnedObject(), (uint)png.Length);

            if (stream == 0)
            {
                return 0;
            }

            try
            {
                return GdipCreateBitmapFromStream(stream, out var bitmap) == 0 ? bitmap : 0;
            }
            finally
            {
                Marshal.Release(stream);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private void LoadFont()
    {
        if (Embedded("D47.Splash.JetBrainsMono-Regular.ttf") is { } ttf)
        {
            var handle = GCHandle.Alloc(ttf, GCHandleType.Pinned);

            try
            {
                uint count = 0;
                _fontResource = AddFontMemResourceEx(handle.AddrOfPinnedObject(), (uint)ttf.Length, 0, ref count);
            }
            finally
            {
                handle.Free();
            }
        }

        _font = CreateFontW(
            -(int)Math.Round(VersionSize * _scale), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, FontFace);
    }

    private void BuildSurface(nint bitmap)
    {
        var screen = GetDC(0);

        try
        {
            if (GdipCreateHBITMAPFromBitmap(bitmap, out var source, 0) != 0)
            {
                GdipDisposeImage(bitmap);
                return;
            }

            GdipDisposeImage(bitmap);

            var from = CreateCompatibleDC(screen);
            var to = CreateCompatibleDC(screen);
            _surface = CreateCompatibleBitmap(screen, _width, _height);
            var oldFrom = SelectObject(from, source);
            var oldTo = SelectObject(to, _surface);

            SetStretchBltMode(to, Halftone);
            var sourceScale = ImageFor(_scale) switch { "100" => 1, "150" => 1.5, _ => 2 };
            StretchBlt(
                to, 0, 0, _width, _height, from, 0, 0,
                (int)(DesignWidth * sourceScale), (int)(DesignHeight * sourceScale), SrcCopy);

            SetBkMode(to, Transparent);
            SetTextColor(to, VersionColour);

            var oldFont = _font == 0 ? 0 : SelectObject(to, _font);
            var line = new Rect
            {
                Left = 0,
                Top = (int)Math.Round(VersionTop * _scale),
                Right = _width,
                Bottom = (int)Math.Round((VersionTop + VersionLine) * _scale),
            };

            DrawTextW(to, _version, -1, ref line, DtCenter | DtVCenter | DtSingleLine | DtNoPrefix);

            if (oldFont != 0)
            {
                SelectObject(to, oldFont);
            }

            SelectObject(from, oldFrom);
            SelectObject(to, oldTo);
            DeleteDC(from);
            DeleteDC(to);
            DeleteObject(source);
        }
        finally
        {
            ReleaseDC(0, screen);
        }
    }

    private nint Handle(nint hwnd, uint message, nint wParam, nint lParam)
    {
        switch (message)
        {
            case WmEraseBkgnd:
                return 1;

            case WmPaint:
                var hdc = BeginPaint(hwnd, out var paint);

                if (_surface != 0)
                {
                    var memory = CreateCompatibleDC(hdc);
                    var old = SelectObject(memory, _surface);
                    BitBlt(hdc, 0, 0, _width, _height, memory, 0, 0, SrcCopy);
                    SelectObject(memory, old);
                    DeleteDC(memory);
                }

                EndPaint(hwnd, ref paint);

                return 0;

            case WmClose:
                DestroyWindow(hwnd);

                return 0;

            case WmDestroy:
                PostQuitMessage(0);

                return 0;
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }
}
