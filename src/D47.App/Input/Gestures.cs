using Avalonia.Input;

namespace D47.App.Input;

/// <summary>Turns a stored gesture into the keys as they are printed on a keyboard.</summary>
public static class Gestures
{
    /// <summary>What is printed on the key, for the keys whose name is not.</summary>
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
            // A hand-edited settings file can hold anything.
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

    /// <summary>One key, as it is printed on it.</summary>
    public static string Describe(Key key) => Printed.GetValueOrDefault(key, key.ToString());
}
