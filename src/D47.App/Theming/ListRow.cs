using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>
/// The list-row style class for a hand-built <see cref="Border"/> row. <see cref="Class"/> gives the
/// row its ground per state, and sets <see cref="NameBrushProperty"/> and
/// <see cref="SecondaryBrushProperty"/>, which inherit to the row's text through <see cref="Name"/>,
/// <see cref="Sub"/>, <see cref="Aside"/>, <see cref="NameInk"/> and <see cref="SecondaryInk"/>. The
/// container spaces rows 2px apart.
/// </summary>
public static class ListRow
{
    public const string Class = "d47-row";

    /// <summary>Marks the row as the chosen one; set and cleared by the screen that owns it.</summary>
    public const string SelectedClass = "selected";

    /// <summary>Marks a list item's description line, inked Grey at rest and Brown when selected.</summary>
    public const string DetailClass = "d47-row-detail";

    public const double NameSize = 15;

    public const double SubSize = 12;

    public const double AsideSize = 13;

    public const double HeadSize = 13;

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

    /// <summary>The row's name: Saira 15/600, upper case, tracked 0.03em, in the name ink.</summary>
    public static TextBlock Name(TextBlock text) => Set(text, NameSize, FontWeight.SemiBold, 0.03, NameBrushProperty);

    /// <summary>The row's sub line: Saira 12/500, upper case, tracked 0.04em, in the secondary ink.</summary>
    public static TextBlock Sub(TextBlock text) => Set(text, SubSize, FontWeight.Medium, 0.04, SecondaryBrushProperty);

    /// <summary>A figure or state at the row's right: Saira 13/500, upper case, in the secondary ink.</summary>
    public static TextBlock Aside(TextBlock text) => Set(text, AsideSize, FontWeight.Medium, 0, SecondaryBrushProperty);

    /// <summary>
    /// Colours <paramref name="text"/> in the name ink and leaves its type alone — for a sentence, which stays
    /// in Sintony and its own case.
    /// </summary>
    public static TextBlock NameInk(TextBlock text) => Follow(text, NameBrushProperty);

    /// <summary>Colours <paramref name="text"/> in the secondary ink and leaves its type alone.</summary>
    public static TextBlock SecondaryInk(TextBlock text) => Follow(text, SecondaryBrushProperty);

    /// <summary>
    /// A list's group head: Saira 13/600, upper case, tracked 0.08em, in A over a 1px Line rule, with 2px
    /// between the rule and the first row.
    /// </summary>
    public static Border Head(string text)
    {
        var label = new SelectableTextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = HeadSize,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = HeadSize * 0.08,
            TextWrapping = TextWrapping.NoWrap,
        };

        label.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));

        var head = new Border
        {
            Padding = new Thickness(0, 10, 0, 6),
            Margin = new Thickness(0, 0, 0, 2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = label,
        };

        head.Bind(Border.BorderBrushProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.LineKey));

        return head;
    }

    /// <summary>
    /// Sets the face, size, weight and tracking and upper-cases the text and runs already on the block. Text set
    /// afterwards is the caller's to upper-case.
    /// </summary>
    private static TextBlock Set(TextBlock text, double size, FontWeight weight, double tracking, AttachedProperty<IBrush?> brush)
    {
        text.FontFamily = Fonts.ChromeFamily;
        text.FontSize = size;
        text.FontWeight = weight;
        text.LetterSpacing = size * tracking;

        if (text.Inlines is { Count: > 0 } inlines)
        {
            foreach (var run in inlines.OfType<Run>())
            {
                run.Text = run.Text?.ToUpperInvariant();
            }
        }
        else
        {
            text.Text = text.Text?.ToUpperInvariant();
        }

        return Follow(text, brush);
    }

    private static TextBlock Follow(TextBlock text, AttachedProperty<IBrush?> brush)
    {
        text.Bind(TextBlock.ForegroundProperty, text.GetObservable(brush));
        return text;
    }
}
