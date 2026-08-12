using System.Runtime.InteropServices;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// A key that works while Elite has the foreground.
/// <para>
/// This is the one thing a window-scoped gesture cannot be. "Shut up" is defined by never being
/// gated — not behind a turn completing, not behind the model, and not behind d47 having focus.
/// A Commander in a headset with the game in front of them has no way to give d47 focus, so a
/// focus-scoped stop key is a stop key that does not work precisely when it is wanted
/// (list.md Phase 5).
/// </para>
/// <para>
/// <c>RegisterHotKey</c>, deliberately, and never a <c>WH_KEYBOARD_LL</c> hook. A low-level
/// keyboard hook is a global input chokepoint: a stall in d47 becomes a stall in the Commander's
/// controls, mid-fight. The same rule governs key injection (architecture.md D4), and it applies
/// at least as strongly to reading keys as to sending them.
/// </para>
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;

    [Flags]
    private enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,

        /// <summary>Stops the registration auto-repeating while the key is held.</summary>
        NoRepeat = 0x4000,
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Action> _actions = [];

    private nint _window;
    private int _nextId = 1;

    public GlobalHotkey(ILogger logger) => _logger = logger;

    /// <summary>
    /// The window whose message loop receives the notifications. Supplied once the panel exists,
    /// because a registration needs a window handle even though the key itself is system-wide.
    /// </summary>
    public void AttachTo(nint windowHandle)
    {
        lock (_gate)
        {
            _window = windowHandle;
        }
    }

    /// <summary>
    /// Binds a gesture, replacing whatever was bound before. Returns false when the combination
    /// is already taken by another application — which is worth reporting rather than swallowing,
    /// since the symptom otherwise is a key that simply does nothing.
    /// </summary>
    public bool Bind(string? gesture, Action onPressed)
    {
        lock (_gate)
        {
            UnbindAll();

            if (string.IsNullOrWhiteSpace(gesture) || _window == 0)
            {
                return false;
            }

            KeyGesture parsed;

            try
            {
                parsed = KeyGesture.Parse(gesture);
            }
            catch (Exception ex)
            {
                // A hand-edited settings file can contain anything. An unparseable gesture is
                // an unbound key, not a crash on startup.
                _logger.LogWarning(ex, "Could not parse the global hotkey {Gesture}", gesture);
                return false;
            }

            var modifiers = Modifiers.NoRepeat;
            if (parsed.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= Modifiers.Control;
            if (parsed.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= Modifiers.Alt;
            if (parsed.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= Modifiers.Shift;
            if (parsed.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= Modifiers.Win;

            // A bare key would be registered system-wide, which would take that key away from
            // every other application including the game. Refused rather than obeyed.
            if ((modifiers & ~Modifiers.NoRepeat) == 0)
            {
                _logger.LogWarning(
                    "Refusing to bind {Gesture} globally: a system-wide key needs a modifier", gesture);
                return false;
            }

            if (VirtualKeyOf(parsed.Key) is not { } virtualKey)
            {
                _logger.LogWarning("{Key} has no virtual-key code d47 knows", parsed.Key);
                return false;
            }

            var id = _nextId++;

            if (!RegisterHotKey(_window, id, (uint)modifiers, virtualKey))
            {
                _logger.LogWarning(
                    "{Gesture} could not be registered system-wide; another application likely holds it",
                    gesture);
                return false;
            }

            _actions[id] = onPressed;
            _logger.LogInformation("{Gesture} is bound system-wide", gesture);
            return true;
        }
    }

    /// <summary>
    /// Handles a window message. Returns true when it was ours.
    /// <para>
    /// The action runs on the message-loop thread on purpose: silencing is a queue operation
    /// with no work in it, so dispatching it somewhere else would add latency to the one thing
    /// whose whole point is being immediate.
    /// </para>
    /// </summary>
    public bool HandleMessage(int message, nint wParam)
    {
        if (message != WmHotkey)
        {
            return false;
        }

        Action? action;

        lock (_gate)
        {
            _actions.TryGetValue((int)wParam, out action);
        }

        action?.Invoke();
        return action is not null;
    }

    /// <summary>
    /// Avalonia's <see cref="Key"/> to the Windows virtual-key code <c>RegisterHotKey</c> wants.
    /// <para>
    /// Avalonia has no equivalent of WPF's <c>KeyInterop</c>, and the enum's numeric values are
    /// its own rather than the VK constants, so the mapping is written out. Ranges are computed
    /// because they are contiguous in both; the rest are named. A key that is missing here is
    /// reported as unbindable rather than silently registered as the wrong key — which would be
    /// the worse failure, since the Commander would be pressing something that does something
    /// else.
    /// </para>
    /// </summary>
    private static uint? VirtualKeyOf(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (uint)(0x41 + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (uint)(0x30 + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (key - Key.NumPad0)),
        >= Key.F1 and <= Key.F24 => (uint)(0x70 + (key - Key.F1)),
        Key.Space => 0x20,
        Key.Enter => 0x0D,
        Key.Escape => 0x1B,
        Key.Tab => 0x09,
        Key.Back => 0x08,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Home => 0x24,
        Key.End => 0x23,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.OemTilde => 0xC0,
        Key.OemMinus => 0xBD,
        Key.OemPlus => 0xBB,
        Key.OemOpenBrackets => 0xDB,
        Key.OemCloseBrackets => 0xDD,
        Key.OemPipe => 0xDC,
        Key.OemSemicolon => 0xBA,
        Key.OemQuotes => 0xDE,
        Key.OemComma => 0xBC,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        _ => null,
    };

    private void UnbindAll()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_window, id);
        }

        _actions.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            UnbindAll();
            _window = 0;
        }
    }
}
