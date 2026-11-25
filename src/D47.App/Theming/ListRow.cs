using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

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

    /// <summary>Marks a row that takes keyboard focus and opens on a press, Enter or Space; set by <see cref="Opens"/>.</summary>
    public const string OpensClass = "d47-row-opens";

    /// <summary>Marks the row as the chosen one; set and cleared by the screen that owns it.</summary>
    public const string SelectedClass = "selected";

    /// <summary>A pressable row drawn in the disabled inks; it stays pressable. Not set together with <see cref="SelectedClass"/>.</summary>
    public const string DimClass = "dim";

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

    /// <summary>
    /// Makes <paramref name="target"/> a keyboard-focusable row that runs <paramref name="open"/> on a press, and on
    /// Enter or Space with no modifiers while it has focus.
    /// </summary>
    public static T Opens<T>(T target, Action open)
        where T : Control
    {
        target.Focusable = true;
        target.Cursor = new Cursor(StandardCursorType.Hand);
        target.Classes.Add(OpensClass);

        target.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            open();
        };

        target.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                open();
            }
        };

        return target;
    }

    /// <summary>How long the platform allows between the two presses of a double-click.</summary>
    public static TimeSpan DoubleClickTime =>
        Application.Current?.PlatformSettings?.GetDoubleTapTime(PointerType.Mouse) is { } time && time > TimeSpan.Zero
            ? time
            : TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Runs <paramref name="act"/> on the second left press of a double-click on <paramref name="row"/>. Counted from
    /// the presses, not <c>DoubleTapped</c>, because a first click that redraws the list puts the second press on a
    /// new control: the first press is remembered in <paramref name="presses"/>, which the page owns, by
    /// <paramref name="key"/>. A press with Control held, or one whose first press was on another row, does nothing.
    /// </summary>
    public static void OnDoubleClick(Control row, RowPresses presses, string key, Action act)
    {
        row.AddHandler(
            InputElement.PointerPressedEvent,
            (_, args) =>
            {
                if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                if (args.ClickCount == 1)
                {
                    presses.Key = key;
                    return;
                }

                if (args.ClickCount != 2
                    || args.KeyModifiers.HasFlag(KeyModifiers.Control)
                    || !string.Equals(presses.Key, key, StringComparison.Ordinal))
                {
                    return;
                }

                presses.Key = null;
                act();
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    /// <summary>True for a visible, enabled control that takes keyboard focus and is dressed as a row.</summary>
    internal static bool IsFocusable(Control control) =>
        control.Focusable
        && control.IsEffectivelyVisible
        && control.IsEffectivelyEnabled
        && (control.Classes.Contains(Class) || control.Classes.Contains(OpensClass));

    /// <summary>The focusable rows under <paramref name="pane"/>, in tree order.</summary>
    internal static List<Control> Within(Control pane) =>
        pane.GetVisualDescendants().OfType<Control>().Where(IsFocusable).ToList();

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

        ThemeInk.Bind(label, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var head = new Border
        {
            Padding = new Thickness(0, 10, 0, 6),
            Margin = new Thickness(0, 0, 0, 2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = label,
        };

        ThemeInk.Bind(head, Border.BorderBrushProperty, ThemeManager.LineKey);

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

/// <summary>The key of the row last pressed once, kept by the page whose rows <see cref="ListRow.OnDoubleClick"/> watches.</summary>
public sealed class RowPresses
{
    internal string? Key { get; set; }
}
