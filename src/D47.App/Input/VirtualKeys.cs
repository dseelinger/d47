using Avalonia.Input;

namespace D47.App.Input;

/// <summary>
/// Avalonia's <see cref="Key"/> to the Windows virtual-key codes the input APIs want.
/// <para>
/// Written out rather than cast, because Avalonia's enum values are its own and are not the VK
/// constants. A key missing here is reported as unbindable rather than silently registered as
/// the wrong key — which is the worse failure, since the Commander would then be pressing
/// something that does something else entirely.
/// </para>
/// <para>
/// Shared by the silence key, which registers a system-wide hotkey, and by push-to-talk, which
/// polls one code from the tick loop. Two callers with different mechanisms and one table: a
/// second copy is a second thing to be wrong.
/// </para>
/// </summary>
public static class VirtualKeys
{
    public const uint Shift = 0x10;
    public const uint Control = 0x11;
    public const uint Alt = 0x12;
    public const uint Windows = 0x5B;

    public static uint? Of(Key key) => key switch
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
        Key.CapsLock => 0x14,

        // The side-specific modifiers. They are here for push-to-talk, which is polled and
        // therefore reads one code rather than registering a gesture: a Commander flying with
        // both hands on a stick and throttle has a spare shift under a thumb and nothing else
        // spare, and right shift is the default because of it. Sided codes rather than the
        // generic 0x10/0x11/0x12, so holding the left one does not open the microphone.
        Key.LeftShift => 0xA0,
        Key.RightShift => 0xA1,
        Key.LeftCtrl => 0xA2,
        Key.RightCtrl => 0xA3,
        Key.LeftAlt => 0xA4,
        Key.RightAlt => 0xA5,
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
}
