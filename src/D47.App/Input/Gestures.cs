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
        [Key.Scroll] = "Scroll Lock",
        [Key.D0] = "0",
        [Key.D1] = "1",
        [Key.D2] = "2",
        [Key.D3] = "3",
        [Key.D4] = "4",
        [Key.D5] = "5",
        [Key.D6] = "6",
        [Key.D7] = "7",
        [Key.D8] = "8",
        [Key.D9] = "9",
        [Key.NumPad0] = "Num 0",
        [Key.NumPad1] = "Num 1",
        [Key.NumPad2] = "Num 2",
        [Key.NumPad3] = "Num 3",
        [Key.NumPad4] = "Num 4",
        [Key.NumPad5] = "Num 5",
        [Key.NumPad6] = "Num 6",
        [Key.NumPad7] = "Num 7",
        [Key.NumPad8] = "Num 8",
        [Key.NumPad9] = "Num 9",
    };

    /// <summary>What each of <paramref name="row"/>'s bound keys holds now, as Settings prints it, aligned by index; null where a slot holds nothing.</summary>
    public static IReadOnlyList<string?> BoundAs(D47.Core.Configuration.SettingsService settings, D47.Core.Capabilities.SettingRow row) =>
        [.. row.BoundKeys.Select(key =>
        {
            var stored = settings.Read(key);

            if (string.IsNullOrWhiteSpace(stored))
            {
                return null;
            }

            return (settings.Find(key)?.Kind ?? D47.Core.Capabilities.SettingKind.Hotkey) == D47.Core.Capabilities.SettingKind.HotasButton
                ? D47.Core.Hotas.HotasButton.Parse(stored)?.Describe() ?? stored
                : Describe(stored);
        })];

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
