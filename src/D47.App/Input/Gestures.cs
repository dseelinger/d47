using Avalonia.Input;

namespace D47.App.Input;

/// <summary>
/// Turns a stored gesture into the keys as they are printed on a keyboard.
/// <para>
/// Gestures are stored in Avalonia's own spelling, because that is what round-trips through
/// <see cref="KeyGesture.Parse"/> and therefore what a hand-edited settings file can be checked
/// against. That spelling names keys after the Windows virtual-key constants, so the settings
/// shortcut every other application calls <c>Ctrl+,</c> stores as <c>Ctrl+OemComma</c>. Showing
/// a Commander the constant would be showing them our implementation.
/// </para>
/// </summary>
public static class Gestures
{
    /// <summary>
    /// The keys whose stored name is not what is printed on the key. Only the ones a binding is
    /// plausibly made of — this is a display courtesy, not a keyboard-layout database, and a key
    /// that is missing here still shows a name a person can recognise.
    /// </summary>
    private static readonly Dictionary<string, string> Printed = new(StringComparer.Ordinal)
    {
        ["OemComma"] = ",",
        ["OemPeriod"] = ".",
        ["OemQuestion"] = "/",
        ["OemSemicolon"] = ";",
        ["OemQuotes"] = "'",
        ["OemOpenBrackets"] = "[",
        ["OemCloseBrackets"] = "]",
        ["OemPipe"] = "\\",
        ["OemMinus"] = "-",
        ["OemPlus"] = "=",
        ["OemTilde"] = "`",
        ["Return"] = "Enter",
        ["Prior"] = "Page Up",
        ["Next"] = "Page Down",
        ["Escape"] = "Esc",
    };

    public static string Describe(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return "unbound";
        }

        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return string.Join("+", parts.Select(part => Printed.GetValueOrDefault(part, part)));
    }
}
