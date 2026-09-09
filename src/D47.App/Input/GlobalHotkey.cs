using System.Runtime.InteropServices;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>A key that works while Elite has the foreground.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmApp = 0x8000;

    /// <summary>Posted to the pump window to run a register/unregister on its own thread.</summary>
    private const int WmApplyBinding = WmApp + 1;

    private const int WmQuitPump = WmApp + 2;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private static readonly nint HwndMessage = -3;

    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Thread _pump;
    private readonly ManualResetEventSlim _windowReady = new();

    // Kept in fields so the GC cannot collect a delegate the OS still calls into.
    private readonly WndProc _wndProc;

    private nint _window;
    private Action? _onPressed;
    private (uint Modifiers, uint Key)? _wanted;
    private bool _registered;
    private ManualResetEventSlim? _applied;
    private bool _applyResult;
    private bool _disposed;

    private static int _instances;

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    public GlobalHotkey(ILogger logger)
    {
        _logger = logger;
        _wndProc = HandleMessage;

        // One background thread whose only job is this window's message loop.
        _pump = new Thread(RunPump) { IsBackground = true, Name = "d47-hotkey-pump" };
        _pump.Start();
    }

    /// <summary>Binds a gesture system-wide, replacing whatever was bound before.</summary>
    public bool Bind(string? gesture, Action onPressed)
    {
        if (!_windowReady.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError("The hotkey pump never came up; the silence key is not registered");
            return false;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _onPressed = onPressed;
            _wanted = null;

            if (!string.IsNullOrWhiteSpace(gesture))
            {
                KeyGesture parsed;

                try
                {
                    parsed = KeyGesture.Parse(gesture);
                }
                catch (Exception ex)
                {
                    // A hand-edited settings file can contain anything.
                    _logger.LogWarning(ex, "Could not parse the global hotkey {Gesture}", gesture);
                    return false;
                }

                var modifiers = ModNoRepeat;
                if (parsed.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= ModControl;
                if (parsed.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= ModAlt;
                if (parsed.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= ModShift;
                if (parsed.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= ModWin;

                // A bare key registered system-wide would take that key away from every other application,
                // including the game.
                if ((modifiers & ~ModNoRepeat) == 0)
                {
                    _logger.LogWarning(
                        "Refusing to bind {Gesture} globally: a system-wide key needs a modifier",
                        Gestures.Describe(gesture));
                    return false;
                }

                if (VirtualKeyOf(parsed.Key) is not { } virtualKey)
                {
                    _logger.LogWarning("{Key} has no virtual-key code D47 knows", Gestures.Describe(parsed.Key));
                    return false;
                }

                _wanted = (modifiers, virtualKey);
            }

            // RegisterHotKey binds to the calling thread, so the actual call has to happen on the pump.
            _applied = new ManualResetEventSlim();
            PostMessage(_window, WmApplyBinding, 0, 0);
        }

        var applied = _applied;

        if (applied is null || !applied.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError("The hotkey pump did not answer; the silence key state is unknown");
            return false;
        }

        lock (_gate)
        {
            _applied = null;

            if (_wanted is null)
            {
                return false;
            }

            if (_applyResult)
            {
                _logger.LogInformation("{Gesture} is bound system-wide", Gestures.Describe(gesture));
            }
            else
            {
                _logger.LogWarning(
                    "{Gesture} could not be registered system-wide; another application likely holds it",
                    Gestures.Describe(gesture));
            }

            return _applyResult;
        }
    }

    private void RunPump()
    {
        // The class and window are created on this thread, so WM_HOTKEY and our WM_APP messages are delivered
        // to this thread's queue and handled in HandleMessage.
        var className = "d47HotkeyPump-" + Environment.ProcessId + "-" + Interlocked.Increment(ref _instances);
        var wc = new WNDCLASSW { lpfnWndProc = _wndProc, lpszClassName = className };

        if (RegisterClassW(ref wc) == 0)
        {
            _logger.LogError("Could not register the hotkey window class (win32 {Error})", Marshal.GetLastWin32Error());
            _windowReady.Set();
            return;
        }

        _window = CreateWindowExW(
            0, className, "D47 hotkey sink", 0, 0, 0, 0, 0, HwndMessage, 0, 0, 0);

        if (_window == 0)
        {
            _logger.LogError("Could not create the hotkey window (win32 {Error})", Marshal.GetLastWin32Error());
            _windowReady.Set();
            return;
        }

        _windowReady.Set();

        while (GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private nint HandleMessage(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmHotkey:
            {
                Action? onPressed;

                lock (_gate)
                {
                    onPressed = _onPressed;
                }

                // Run on this thread rather than dispatched: silencing is a queue operation with no work in
                // it, and the whole point is being immediate.
                onPressed?.Invoke();
                return 0;
            }

            case WmApplyBinding:
            {
                lock (_gate)
                {
                    if (_registered)
                    {
                        UnregisterHotKey(_window, 1);
                        _registered = false;
                    }

                    _applyResult = _wanted is { } wanted
                                   && RegisterHotKey(_window, 1, wanted.Modifiers, wanted.Key);
                    _registered = _applyResult;
                    _applied?.Set();
                }

                return 0;
            }

            case WmQuitPump:
                // Explicit teardown rather than relying on thread exit to reap the window: the OS does clean
                // up eventually, but "eventually" is exactly the window in which a restarting d47 finds its
                // own key still held by its previous self.
                lock (_gate)
                {
                    if (_registered)
                    {
                        UnregisterHotKey(_window, 1);
                        _registered = false;
                    }
                }

                DestroyWindow(hWnd);
                PostQuitMessage(0);
                return 0;

            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    /// <summary>The shared table (<see cref="VirtualKeys"/>), not a copy.</summary>
    private static uint? VirtualKeyOf(Key key) => VirtualKeys.Of(key);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_window != 0)
            {
                PostMessage(_window, WmQuitPump, 0, 0);
            }
        }

        // The pump unblocks on the quit message; hotkey registrations die with the window.
        _pump.Join(TimeSpan.FromSeconds(2));
        _windowReady.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)] public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);
}
