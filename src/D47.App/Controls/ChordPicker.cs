using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Theming;
using D47.Core.Input;

namespace D47.App.Controls;

/// <summary>
/// Two <see cref="InlinePicker"/>s that compose a <see cref="KeyChord"/> from closed lists: the modifiers held,
/// then the key. Raises <see cref="Changed"/> with the whole chord each time either is picked.
/// </summary>
public sealed class ChordPicker : ContentControl
{
    /// <summary>The chord a newly chosen Key chord starts at.</summary>
    public static readonly KeyChord Default = new("Key_V", ChordModifiers.Ctrl);

    /// <summary>The name the modifiers button is announced by.</summary>
    public const string ModifiersLabel = "Chord modifiers";

    /// <summary>The name the key button is announced by.</summary>
    public const string KeyLabel = "Chord key";

    private static readonly (string Heading, Func<string, bool> Holds)[] Groups =
    [
        ("Letters", name => name.Length == 1 && char.IsAsciiLetter(name[0])),
        ("Digits", name => name.Length == 1 && char.IsAsciiDigit(name[0])),
        ("Function keys", name => name.Length > 1 && name[0] == 'F' && char.IsAsciiDigit(name[1])),
        ("Numpad", name => name.StartsWith("Numpad_", StringComparison.Ordinal)),
        ("Navigation", name => name is "Insert" or "Delete" or "Home" or "End" or "PageUp" or "PageDown"
            or "UpArrow" or "DownArrow" or "LeftArrow" or "RightArrow"),
    ];

    private static readonly List<InlinePickerEntry> KeyEntries = BuildKeyEntries();

    private static readonly List<InlinePickerEntry> ModifierEntries =
    [
        .. Enumerable.Range(0, 16).Select(bits =>
            new InlinePickerOption(bits.ToString(), KeyChord.DescribeModifiers((ChordModifiers)bits), ThemeManager.WhiteKey)),
    ];

    /// <summary>Raised with the chord as it now stands.</summary>
    public event EventHandler<KeyChord>? Changed;

    private readonly InlinePicker _modifiers = new() { Label = ModifiersLabel, ListMaxHeight = 400 };
    private readonly InlinePicker _key = new() { Label = KeyLabel, ListMaxHeight = 400 };
    private KeyChord _chord = Default;

    public ChordPicker()
    {
        _modifiers.Picked += (_, id) => Take(_chord with { Modifiers = (ChordModifiers)int.Parse(id) });
        _key.Picked += (_, id) => Take(_chord with { Key = id });

        Content = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Top,
            Children = { _modifiers, _key },
        };

        Draw();
    }

    /// <summary>The chord shown.</summary>
    public KeyChord Chord
    {
        get => _chord;
        set
        {
            _chord = value;
            Draw();
        }
    }

    private void Take(KeyChord chord)
    {
        _chord = chord;
        Draw();
        Changed?.Invoke(this, chord);
    }

    private void Draw()
    {
        var bits = ((int)_chord.Modifiers).ToString();

        _modifiers.Show(ModifierEntries, bits, KeyChord.DescribeModifiers(_chord.Modifiers), ThemeManager.WhiteKey, null);
        _key.Show(KeyEntries, _chord.Key, KeyChord.NameOf(_chord.Key), ThemeManager.WhiteKey, null);
    }

    private static List<InlinePickerEntry> BuildKeyEntries()
    {
        var entries = new List<InlinePickerEntry>();
        var left = KeyChord.KeySymbols.ToList();

        foreach (var (heading, holds) in Groups)
        {
            AddGroup(entries, heading, left.Where(symbol => holds(KeyChord.NameOf(symbol))).ToList());
            left.RemoveAll(symbol => holds(KeyChord.NameOf(symbol)));
        }

        AddGroup(entries, "Other", left);

        return entries;
    }

    private static void AddGroup(List<InlinePickerEntry> entries, string heading, List<string> symbols)
    {
        entries.Add(new InlinePickerHeading(heading));
        entries.AddRange(symbols.Select(symbol =>
            new InlinePickerOption(symbol, KeyChord.NameOf(symbol), ThemeManager.WhiteKey)));
    }
}
