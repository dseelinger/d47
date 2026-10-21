namespace D47.Core.Input;

[Flags]
public enum ChordModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}

/// <summary>One key and the modifiers held with it, sent outside Elite's bindings.</summary>
/// <param name="Key">An <see cref="EliteKeys"/> keyboard symbol with the <c>Key_</c> prefix.</param>
/// <param name="Modifiers">Sent as the left-hand modifier keys.</param>
public sealed record KeyChord(string Key, ChordModifiers Modifiers = ChordModifiers.None)
{
    private const string Prefix = "Key_";

    private static readonly (ChordModifiers Flag, string Symbol, string Name)[] ModifierKeys =
    [
        (ChordModifiers.Ctrl, "Key_LeftControl", "Ctrl"),
        (ChordModifiers.Shift, "Key_LeftShift", "Shift"),
        (ChordModifiers.Alt, "Key_LeftAlt", "Alt"),
        (ChordModifiers.Win, "Key_LeftWin", "Win"),
    ];

    private static readonly HashSet<string> ModifierSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "Key_LeftShift", "Key_RightShift", "Key_LeftControl", "Key_RightControl",
        "Key_LeftAlt", "Key_RightAlt", "Key_LeftWin", "Key_RightWin",
    };

    /// <summary>Modifiers in the order Ctrl, Shift, Alt, Win, then the key without its prefix.</summary>
    public string Describe()
    {
        var parts = ModifierKeys.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Name).ToList();
        parts.Add(KeyName(Key));

        return string.Join('+', parts);
    }

    /// <summary>The modifiers in <see cref="Describe"/>'s order and spelling, or "None".</summary>
    public static string DescribeModifiers(ChordModifiers modifiers) =>
        modifiers == ChordModifiers.None
            ? "None"
            : string.Join('+', ModifierKeys.Where(m => modifiers.HasFlag(m.Flag)).Select(m => m.Name));

    /// <summary>Every keyboard symbol a chord may use as its key: all of <see cref="EliteKeys"/>' keys but the modifiers.</summary>
    public static IReadOnlyList<string> KeySymbols { get; } =
        [.. EliteKeys.KeyboardSymbols.Where(symbol => !ModifierSymbols.Contains(symbol))];

    /// <summary>A key symbol without its <c>Key_</c> prefix.</summary>
    public static string NameOf(string key) => KeyName(key);

    /// <summary>Modifiers down, the key tapped, modifiers up in reverse; empty for a chord with a problem.</summary>
    public IReadOnlyList<InputStep> Steps()
    {
        if (Problem(this) is not null)
        {
            return [];
        }

        var binding = new EliteBinding(string.Empty, string.Empty, "Keyboard", Key)
        {
            Modifiers = [.. ModifierKeys.Where(m => Modifiers.HasFlag(m.Flag)).Select(m => m.Symbol)],
        };

        return InputSequence.Tap(binding);
    }

    /// <summary>A sentence naming the chord and what is wrong with it, or null when it can be sent.</summary>
    public static string? Problem(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);

        var name = chord.Describe();

        if (ModifierSymbols.Contains(chord.Key?.Trim() ?? string.Empty))
        {
            return $"{name} has a modifier as its key; a modifier belongs in the modifiers.";
        }

        if (chord.Key is null ||
            !chord.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            EliteKeys.Resolve(chord.Key).Kind != EliteInputKind.Keyboard)
        {
            return $"{name} is not a keyboard key d47 knows.";
        }

        if (chord.Modifiers == ChordModifiers.Alt && IsKey(chord, "F4"))
        {
            return $"{name} is refused: it closes the window in front, Elite included.";
        }

        if (chord.Modifiers == ChordModifiers.Win && IsKey(chord, "L"))
        {
            return $"{name} is refused: it locks Windows, which in a headset leaves the Commander looking at nothing.";
        }

        return null;
    }

    private static bool IsKey(KeyChord chord, string name) =>
        string.Equals(KeyName(chord.Key), name, StringComparison.OrdinalIgnoreCase);

    private static string KeyName(string? key)
    {
        var trimmed = key?.Trim() ?? string.Empty;

        return trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? trimmed[Prefix.Length..] : trimmed;
    }
}
