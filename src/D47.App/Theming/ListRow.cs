using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>
/// The list-row style class for a hand-built <see cref="Border"/> row. <see cref="Class"/> gives the
/// row its ground per state, and sets <see cref="NameBrushProperty"/> and
/// <see cref="SecondaryBrushProperty"/>, which inherit to the row's text through
/// <see cref="Name"/> and <see cref="Secondary"/>. The container spaces rows 2px apart.
/// </summary>
public static class ListRow
{
    public const string Class = "d47-row";

    /// <summary>Marks the row as the chosen one; set and cleared by the screen that owns it.</summary>
    public const string SelectedClass = "selected";

    /// <summary>Marks a list item's description line, inked Grey at rest and Brown when selected.</summary>
    public const string DetailClass = "d47-row-detail";

    public static readonly AttachedProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.RegisterAttached<Control, IBrush?>("NameBrush", typeof(ListRow), inherits: true);

    public static readonly AttachedProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.RegisterAttached<Control, IBrush?>("SecondaryBrush", typeof(ListRow), inherits: true);

    public static IBrush? GetNameBrush(Control control) => control.GetValue(NameBrushProperty);

    public static void SetNameBrush(Control control, IBrush? value) => control.SetValue(NameBrushProperty, value);

    public static IBrush? GetSecondaryBrush(Control control) => control.GetValue(SecondaryBrushProperty);

    public static void SetSecondaryBrush(Control control, IBrush? value) => control.SetValue(SecondaryBrushProperty, value);

    /// <summary>Dresses <paramref name="row"/> as a list row.</summary>
    public static Border Dress(Border row, bool selected = false)
    {
        row.Classes.Add(Class);
        row.Classes.Set(SelectedClass, selected);
        return row;
    }

    /// <summary>Dresses <paramref name="row"/> as a pressable list row.</summary>
    public static Button Dress(Button row, bool selected = false)
    {
        row.Classes.Add(Class);
        row.Classes.Set(SelectedClass, selected);
        return row;
    }

    /// <summary>Colours <paramref name="text"/> as the row's name.</summary>
    public static TextBlock Name(TextBlock text) => Follow(text, NameBrushProperty);

    /// <summary>Colours <paramref name="text"/> as the row's secondary line.</summary>
    public static TextBlock Secondary(TextBlock text) => Follow(text, SecondaryBrushProperty);

    private static TextBlock Follow(TextBlock text, AttachedProperty<IBrush?> brush)
    {
        text.Bind(TextBlock.ForegroundProperty, text.GetObservable(brush));
        return text;
    }
}
