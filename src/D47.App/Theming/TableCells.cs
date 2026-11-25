using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>Cells for the fields a table adds beside its own columns.</summary>
public static class TableCells
{
    public const string Dash = "—";

    /// <summary>Makes <paramref name="text"/> wrap at word breaks instead of trimming, as a name column's text does.</summary>
    public static TextBlock Wraps(TextBlock text)
    {
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;

        return text;
    }

    /// <summary>Yes in White or No in Grey.</summary>
    public static TextBlock Flag(bool value) =>
        Typeset.Text(value ? "Yes" : "No", TypeScale.Secondary, value ? ThemeManager.WhiteKey : ThemeManager.GreyKey, vertical: VerticalAlignment.Center);

    /// <summary>A dash in Grey, for a value the row does not have.</summary>
    public static TextBlock Missing() =>
        Typeset.Text(Dash, TypeScale.Secondary, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);

    /// <summary>The text in White, wrapping at word breaks when its column is short of room, or <see cref="Missing"/> when there is none.</summary>
    public static TextBlock Text(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Missing()
            : Typeset.Text(value, TypeScale.Secondary, ThemeManager.WhiteKey, wrap: true, vertical: VerticalAlignment.Center);

    /// <summary>The number in mono, right-aligned; a Grey dash when there is none.</summary>
    public static TextBlock Number(long? value) =>
        Typeset.Mono(
            value is { } number ? number.ToString("N0", CultureInfo.InvariantCulture) : Dash,
            TypeScale.Control,
            value is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            vertical: VerticalAlignment.Center,
            horizontal: HorizontalAlignment.Right);

    /// <summary>
    /// The first three items joined by " · ", wrapping at word breaks when the column is short of room, then "+N" in mono
    /// Grey for the rest in a slot of its own; <see cref="Missing"/> when empty.
    /// </summary>
    public static Control List(IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return Missing();
        }

        var shown = Typeset.Text(
            string.Join(" · ", items.Take(3)),
            TypeScale.Secondary,
            ThemeManager.WhiteKey,
            wrap: true,
            vertical: VerticalAlignment.Center);

        if (items.Count <= 3)
        {
            return shown;
        }

        var more = Typeset.Mono(
            "+" + (items.Count - 3).ToString(CultureInfo.InvariantCulture),
            TypeScale.Control,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center);

        Grid.SetColumn(more, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { shown, more },
        };
    }
}
