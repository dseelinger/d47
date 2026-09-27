using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>
/// A text field's own error or warning: the <see cref="ErrorClass"/> or <see cref="WarningClass"/>
/// outline, and <see cref="TextProperty"/> drawn under the box by the <c>TextBox</c> theme. The next
/// edit that changes the text clears it.
/// </summary>
public static class FieldMessage
{
    public const string ErrorClass = "error";

    public const string WarningClass = "warning";

    /// <summary>Draws the field as the design's search field.</summary>
    public const string SearchClass = "search";

    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Text", typeof(FieldMessage));

    private static readonly AttachedProperty<string?> ShownOverProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("ShownOver", typeof(FieldMessage));

    public static string? GetText(TextBox box) => box.GetValue(TextProperty);

    public static void SetText(TextBox box, string? value) => box.SetValue(TextProperty, value);

    /// <summary>Outlines <paramref name="box"/> in Red and writes <paramref name="text"/> under it.</summary>
    public static void ShowError(TextBox box, string text) => Show(box, text, ErrorClass);

    /// <summary>Outlines <paramref name="box"/> in Warn and writes <paramref name="text"/> under it.</summary>
    public static void ShowWarning(TextBox box, string text) => Show(box, text, WarningClass);

    /// <summary>Returns <paramref name="box"/> to its ordinary outline with no message.</summary>
    public static void Clear(TextBox box)
    {
        box.TextChanged -= OnEdited;
        box.Classes.Remove(ErrorClass);
        box.Classes.Remove(WarningClass);
        box.ClearValue(TextProperty);
        box.ClearValue(ShownOverProperty);
    }

    private static void Show(TextBox box, string text, string level)
    {
        Clear(box);
        box.Classes.Add(level);
        SetText(box, text);
        box.SetValue(ShownOverProperty, box.Text ?? string.Empty);
        box.TextChanged += OnEdited;
    }

    private static void OnEdited(object? sender, TextChangedEventArgs e)
    {
        // TextChanged is raised in a later dispatcher job, so a change made before the message was shown can raise it afterwards.
        if (sender is TextBox box && (box.Text ?? string.Empty) != box.GetValue(ShownOverProperty))
        {
            Clear(box);
        }
    }
}
