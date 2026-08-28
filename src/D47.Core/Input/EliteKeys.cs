namespace D47.Core.Input;

/// <summary>
/// What kind of control a bindings entry names. The distinction is the whole point of
/// <see cref="EliteKeys"/>: d47 can press a key or a mouse button and cannot press anything
/// else, and the difference has to be reportable rather than discovered as silence.
/// </summary>
public enum EliteInputKind
{
    /// <summary>A symbol d47 does not recognise at all.</summary>
    Unknown,

    Keyboard,

    Mouse,

    /// <summary>A stick, throttle, gamepad or hand controller button.</summary>
    Device,

    /// <summary>An axis. Analog, so there is no press to send even on a device d47 could reach.</summary>
    Axis,
}

/// <summary>
/// One resolved control: what kind it is, and the code to send if d47 can send it.
/// </summary>
/// <param name="Kind">What sort of control the symbol named.</param>
/// <param name="Code">
/// A virtual-key code for <see cref="EliteInputKind.Keyboard"/>, a one-based button number for
/// <see cref="EliteInputKind.Mouse"/>, and meaningless otherwise.
/// </param>
public readonly record struct EliteKey(EliteInputKind Kind, uint Code)
{
    public static readonly EliteKey None = new(EliteInputKind.Unknown, 0);

    /// <summary>Whether d47 has a way to send this at all.</summary>
    public bool IsPressable => Kind is EliteInputKind.Keyboard or EliteInputKind.Mouse;
}

/// <summary>
/// Elite's key symbols to something d47 can press (Phase 10, "Know which actions the
/// Commander can actually reach").
/// <para>
/// Written out rather than derived, for the same reason <c>VirtualKeys</c> in the app is:
/// Elite's symbols are its own vocabulary and a wrong guess presses a different key, which is
/// a worse failure than pressing nothing. A symbol that is absent here is reported as
/// unreachable.
/// </para>
/// <para>
/// <b>Lookup is case-insensitive because Elite's own files are inconsistent.</b> The shipped
/// presets contain <c>Key_BackSlash</c> and <c>Key_backslash</c> — the same key, spelled two
/// ways, in files Frontier ships. An ordinal comparison would resolve one and silently drop
/// the other.
/// </para>
/// </summary>
public static class EliteKeys
{
    /// <summary>
    /// The device name Elite writes for a slot with nothing in it. Its <c>Key</c> is empty, so
    /// nothing here would match anyway; naming it keeps the check readable at the call site.
    /// </summary>
    public const string NoDevice = "{NoDevice}";

    private static readonly Dictionary<string, uint> KeyboardCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters and digits. Elite writes the bare character, and the virtual-key codes for
        // both runs are the ASCII values of the uppercase letter and the digit.
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45, ["F"] = 0x46,
        ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A, ["K"] = 0x4B, ["L"] = 0x4C,
        ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F, ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52,
        ["S"] = 0x53, ["T"] = 0x54, ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58,
        ["Y"] = 0x59, ["Z"] = 0x5A,

        ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
        ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,

        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74,
        ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79,
        ["F11"] = 0x7A, ["F12"] = 0x7B, ["F13"] = 0x7C, ["F14"] = 0x7D, ["F15"] = 0x7E,
        ["F16"] = 0x7F, ["F17"] = 0x80, ["F18"] = 0x81, ["F19"] = 0x82, ["F20"] = 0x83,

        ["Numpad_0"] = 0x60, ["Numpad_1"] = 0x61, ["Numpad_2"] = 0x62, ["Numpad_3"] = 0x63,
        ["Numpad_4"] = 0x64, ["Numpad_5"] = 0x65, ["Numpad_6"] = 0x66, ["Numpad_7"] = 0x67,
        ["Numpad_8"] = 0x68, ["Numpad_9"] = 0x69,
        ["Numpad_Multiply"] = 0x6A, ["Numpad_Add"] = 0x6B, ["Numpad_Subtract"] = 0x6D,
        ["Numpad_Decimal"] = 0x6E, ["Numpad_Divide"] = 0x6F,

        // The numpad's Enter shares Enter's virtual-key code and is told apart only by the
        // extended-key bit, which the injector applies from the symbol rather than the code.
        ["Numpad_Enter"] = 0x0D,

        ["Escape"] = 0x1B, ["Tab"] = 0x09, ["CapsLock"] = 0x14, ["Space"] = 0x20,
        ["Enter"] = 0x0D, ["Backspace"] = 0x08,

        ["LeftShift"] = 0xA0, ["RightShift"] = 0xA1,
        ["LeftControl"] = 0xA2, ["RightControl"] = 0xA3,
        ["LeftAlt"] = 0xA4, ["RightAlt"] = 0xA5,
        ["LeftWin"] = 0x5B, ["RightWin"] = 0x5C,

        ["Minus"] = 0xBD, ["Equals"] = 0xBB,
        ["LeftBracket"] = 0xDB, ["RightBracket"] = 0xDD,
        ["BackSlash"] = 0xDC, ["SemiColon"] = 0xBA, ["Apostrophe"] = 0xDE,
        ["Grave"] = 0xC0, ["Comma"] = 0xBC, ["Period"] = 0xBE, ["Slash"] = 0xBF,

        ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Home"] = 0x24, ["End"] = 0x23,
        ["PageUp"] = 0x21, ["PageDown"] = 0x22,

        ["UpArrow"] = 0x26, ["DownArrow"] = 0x28,
        ["LeftArrow"] = 0x25, ["RightArrow"] = 0x27,

        ["PrintScreen"] = 0x2C, ["ScrollLock"] = 0x91, ["Pause"] = 0x13, ["NumLock"] = 0x90,
    };

    /// <summary>
    /// Symbols whose scancode needs the extended-key prefix. Windows tells these apart from
    /// their non-extended twins by that bit alone, and since d47 sends scancodes rather than
    /// virtual keys (architecture.md D4), getting it wrong sends the numpad's key instead of
    /// the one the Commander bound.
    /// </summary>
    private static readonly HashSet<string> Extended = new(StringComparer.OrdinalIgnoreCase)
    {
        "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
        "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        "Numpad_Divide", "Numpad_Enter", "RightAlt", "RightControl",
        "NumLock", "PrintScreen",
    };

    /// <summary>
    /// Resolves one symbol as the bindings file spells it — <c>Key_W</c>, <c>Mouse_1</c>,
    /// <c>Joy_3</c>, <c>Pos_Joy_RXAxis</c>.
    /// </summary>
    public static EliteKey Resolve(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return EliteKey.None;
        }

        var name = symbol.Trim();

        // An analog control carries a direction prefix. Checked before anything else because
        // "Neg_Mouse_XAxis" starts with the mouse prefix and is emphatically not a button.
        if (name.StartsWith("Pos_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Neg_", StringComparison.OrdinalIgnoreCase))
        {
            return new EliteKey(EliteInputKind.Axis, 0);
        }

        if (name.EndsWith("Axis", StringComparison.OrdinalIgnoreCase))
        {
            return new EliteKey(EliteInputKind.Axis, 0);
        }

        if (name.StartsWith("Key_", StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardCodes.TryGetValue(name[4..], out var code)
                ? new EliteKey(EliteInputKind.Keyboard, code)
                : EliteKey.None;
        }

        if (name.StartsWith("Mouse_", StringComparison.OrdinalIgnoreCase))
        {
            // Elite numbers mouse buttons from one. A symbol past what SendInput can express
            // is unknown rather than clamped: pressing the wrong button is worse than saying
            // so, and this is the branch a twelve-button gaming mouse lands in.
            return uint.TryParse(name[6..], out var button) && button is >= 1 and <= 5
                ? new EliteKey(EliteInputKind.Mouse, button)
                : EliteKey.None;
        }

        // Everything with a device prefix d47 has no way to press: sticks, gamepads, hand
        // controllers. Named as a category so the reason can say which, rather than "unknown".
        if (name.StartsWith("Joy_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Pad_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("GamePad_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("OculusTouch_", StringComparison.OrdinalIgnoreCase))
        {
            return new EliteKey(EliteInputKind.Device, 0);
        }

        return EliteKey.None;
    }

    /// <summary>Whether a keyboard symbol needs the extended-key bit on its scancode.</summary>
    public static bool IsExtended(string? symbol) =>
        symbol is not null &&
        symbol.StartsWith("Key_", StringComparison.OrdinalIgnoreCase) &&
        Extended.Contains(symbol[4..]);

    /// <summary>
    /// How a Commander would say the device a symbol belongs to, for the spoken reason an
    /// unreachable action gives back.
    /// </summary>
    public static string DeviceName(string? symbol) => Resolve(symbol).Kind switch
    {
        EliteInputKind.Keyboard => "the keyboard",
        EliteInputKind.Mouse => "the mouse",
        EliteInputKind.Axis => "an axis",
        EliteInputKind.Device when symbol!.StartsWith("Joy_", StringComparison.OrdinalIgnoreCase) => "your joystick",
        EliteInputKind.Device => "your controller",
        _ => "a control D47 does not recognise",
    };
}
