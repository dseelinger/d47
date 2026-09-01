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
    /// What is printed on the key, for the keys whose name is not.
    /// <para>
    /// Keyed on the <see cref="Key"/> value rather than on its name, which matters more than it
    /// looks: several of these values have two names in the enum — <c>OemOpenBrackets</c> and
    /// <c>Oem4</c> are the same number — and <c>ToString</c> picks whichever it finds first. A
    /// table keyed on names therefore missed every key whose alias won, so binding <kbd>[</kbd>
    /// bound correctly and then displayed itself as "Oem4". Looking up the value cannot care
    /// which name was written down.
    /// </para>
    /// </summary>
    private static readonly Dictionary<Key, string> Printed = new()
    {
        [Key.OemComma] = ",",
        [Key.OemPeriod] = ".",
        [Key.OemQuestion] = "/",
        [Key.OemSemicolon] = ";",
        [Key.OemQuotes] = "'",
        [Key.OemOpenBrackets] = "[",
        [Key.OemCloseBrackets] = "]",
        [Key.OemPipe] = "\\",
        [Key.OemMinus] = "-",
        [Key.OemPlus] = "=",
        [Key.OemTilde] = "`",
        [Key.OemBackslash] = "\\",
        [Key.Return] = "Enter",
        [Key.Prior] = "Page Up",
        [Key.Next] = "Page Down",
        [Key.Escape] = "Esc",
        [Key.Space] = "Space",
    };

    public static string Describe(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return "unbound";
        }

        KeyGesture parsed;

        try
        {
            parsed = KeyGesture.Parse(gesture);
        }
        catch (Exception)
        {
            // A hand-edited settings file can hold anything. Showing it back verbatim is more
            // use than showing nothing, and the row still offers to rebind.
            return gesture;
        }

        var parts = new List<string>(4);

        // Written in the order a person says them, which is also the order KeyGesture writes.
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (parsed.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");

        parts.Add(Describe(parsed.Key));

        return string.Join("+", parts);
    }

    /// <summary>
    /// One key, as it is printed on it. The same table <see cref="Describe(string?)"/> reads, so
    /// a log line naming a single key cannot drift from a settings row naming a whole gesture —
    /// which is how "bound to Oem4" survived the first fix.
    /// </summary>
    public static string Describe(Key key) => Printed.GetValueOrDefault(key, key.ToString());
}
