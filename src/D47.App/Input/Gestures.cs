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

    /// <summary>
    /// A gesture the Commander typed, as the string a captured one would have stored
    /// (<a href="https://github.com/dseelinger/d47/issues/221">#221</a>).
    /// <para>
    /// <b>Typing is the only road to twelve of these keys.</b> F13 upward are what HOTAS software
    /// hands out — VoiceAttack, TARGET, VIRPIL — precisely because no keyboard has them and
    /// nothing else binds them. A Commander cannot press F23 to bind it; there is no F23 to
    /// press, so the capture control is the wrong instrument for the one range that matters most
    /// on a stick and throttle. The support was already there: <c>VirtualKeys</c> has mapped
    /// F1–F24 all along and push-to-talk polls exactly that code, so this was an input-method gap
    /// rather than a capability one.
    /// </para>
    /// <para>
    /// <b>It stores what a press would store</b>, which is the property the whole thing turns on:
    /// the value goes through <see cref="KeyGesture"/> and out through its <c>ToString</c>, the
    /// same call the capture makes, so a typed <c>f23</c> and a pressed F23 are the same bytes in
    /// the settings file. Anything else and the row would show one thing and a later read find
    /// another.
    /// </para>
    /// <para>
    /// <b>An unknown name is refused loudly rather than stored.</b> A gesture no key can ever
    /// match is a push-to-talk that silently never opens the microphone, which is the worst
    /// failure this row has.
    /// </para>
    /// </summary>
    /// <param name="stored">The value to write, or null when nothing could be read.</param>
    /// <param name="refusal">Why not, in a sentence for the row's message line.</param>
    public static bool TryType(string? typed, out string? stored, out string? refusal)
    {
        stored = null;
        refusal = null;

        if (string.IsNullOrWhiteSpace(typed))
        {
            refusal = "Type a key's name — F23, or Ctrl+F13.";
            return false;
        }

        var parts = typed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            refusal = "Type a key's name — F23, or Ctrl+F13.";
            return false;
        }

        var modifiers = KeyModifiers.None;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (Modifier(parts[i]) is not { } held)
            {
                refusal = $"\"{parts[i]}\" is not a modifier. Ctrl, Shift, Alt and Win are.";
                return false;
            }

            modifiers |= held;
        }

        if (!TryKey(parts[^1], out var key))
        {
            // The one refusal worth its own sentence, because the ask names keys that do not
            // exist: Win32 defines VK_F1 through VK_F24 and stops, so Avalonia's enum does too.
            // Software that appears to send F25 on Windows is sending something else.
            refusal = Beyond(parts[^1]) is { } past
                ? $"Windows has F1 to F24 and no more, so there is no {past} to bind."
                : $"\"{parts[^1]}\" is not a key I know. Try a name like F23, or the character on the key.";

            return false;
        }

        stored = new KeyGesture(key, modifiers).ToString();
        return true;
    }

    private static KeyModifiers? Modifier(string word) => word.ToLowerInvariant() switch
    {
        "ctrl" or "control" => KeyModifiers.Control,
        "shift" => KeyModifiers.Shift,
        "alt" => KeyModifiers.Alt,
        "win" or "meta" or "cmd" or "super" => KeyModifiers.Meta,
        _ => null,
    };

    /// <summary>
    /// One key by the name a Commander would write. <b>The printed table is read backwards
    /// first</b>, so what the row displays is what can be typed back into it — "Esc" and "Page
    /// Up" go in as readily as they come out, and neither is the enum's own spelling.
    /// </summary>
    private static bool TryKey(string word, out Key key)
    {
        foreach (var printed in Printed)
        {
            if (string.Equals(printed.Value, word, StringComparison.OrdinalIgnoreCase)
                || string.Equals(printed.Value.Replace(" ", string.Empty), word.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                key = printed.Key;
                return true;
            }
        }

        // **Digits are refused before the enum sees them.** `Enum.TryParse` reads "13" as the
        // Key whose number is 13, so a Commander typing a bare number would bind whatever that
        // happens to be rather than being told they typed nothing recognisable.
        if (word.All(char.IsAsciiDigit))
        {
            key = Key.None;
            return false;
        }

        return Enum.TryParse(word, ignoreCase: true, out key) && key != Key.None;
    }

    /// <summary>The F-key past the end of the range, where that is what was typed. Null otherwise.</summary>
    private static string? Beyond(string word) =>
        word.Length > 1
        && (word[0] is 'f' or 'F')
        && int.TryParse(word[1..], out var number)
        && number > 24
            ? $"F{number}"
            : null;
}
