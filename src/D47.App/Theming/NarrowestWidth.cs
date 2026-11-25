using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>
/// How narrow an already measured table cell can be drawn with its wrapping text broken at word breaks. Text that
/// wraps counts its longest word; anything this cannot see inside counts its measured width, so it never narrows.
/// </summary>
internal static class NarrowestWidth
{
    private const int MostRemembered = 4096;

    [ThreadStatic]
    private static Dictionary<(Face, string), double>? words;

    private static Dictionary<(Face, string), double> Words => words ??= [];

    /// <summary>The narrowest width of <paramref name="control"/>, margin included; never more than its measured width.</summary>
    public static double Of(Control control)
    {
        if (!control.IsVisible)
        {
            return 0;
        }

        var natural = control.DesiredSize.Width;

        if (!double.IsNaN(control.Width))
        {
            return natural;
        }

        var inner = Inside(control);
        var margin = control.Margin.Left + control.Margin.Right;

        return Math.Min(natural, Math.Max(inner, control.MinWidth) + margin);
    }

    private static double Inside(Control control) => control switch
    {
        TextBlock text => Text(text),
        Border border => Child(border.Child) + border.Padding.Left + border.Padding.Right
            + border.BorderThickness.Left + border.BorderThickness.Right,
        Decorator decorator when decorator.GetType() == typeof(Decorator) =>
            Child(decorator.Child) + decorator.Padding.Left + decorator.Padding.Right,
        ContentControl { Content: Control content } holder => Child(content) + holder.Padding.Left + holder.Padding.Right
            + holder.BorderThickness.Left + holder.BorderThickness.Right,
        StackPanel { Orientation: Avalonia.Layout.Orientation.Horizontal } across => Measured(across),
        StackPanel stack => stack.Children.Where(child => child.IsVisible).Select(Of).DefaultIfEmpty(0).Max(),
        WrapPanel { Orientation: Avalonia.Layout.Orientation.Horizontal } wrap => wrap.Children.Where(child => child.IsVisible).Select(Of).DefaultIfEmpty(0).Max(),
        DockPanel dock => Dock(dock),
        Grid grid when grid.GetType() == typeof(Grid) => Columns(grid),
        Avalonia.Controls.Panel panel when panel.GetType() == typeof(Avalonia.Controls.Panel) => panel.Children.Select(Of).DefaultIfEmpty(0).Max(),
        _ => Measured(control),
    };

    private static double Measured(Control control) =>
        control.DesiredSize.Width - control.Margin.Left - control.Margin.Right;

    private static double Child(Control? child) => child is null ? 0 : Of(child);

    private static double Text(TextBlock text)
    {
        if (text.TextWrapping == TextWrapping.NoWrap)
        {
            return Measured(text);
        }

        List<(Face Face, string Text)> pieces;

        if (text.Inlines is { Count: > 0 } inlines)
        {
            if (!inlines.All(inline => inline is Run or LineBreak))
            {
                return Measured(text);
            }

            pieces = [.. inlines.Select(inline => inline is Run run ? (FaceOf(run, text), run.Text ?? string.Empty) : (FaceOf(text), " "))];
        }
        else if (string.IsNullOrEmpty(text.Text))
        {
            return Measured(text);
        }
        else
        {
            pieces = [(FaceOf(text), text.Text)];
        }

        var longest = 0.0;
        var current = 0.0;

        foreach (var (face, piece) in pieces)
        {
            var parts = piece.Split((char[]?)null);

            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    longest = Math.Max(longest, current);
                    current = 0;
                }

                if (parts[i].Length > 0)
                {
                    current += Word(face, parts[i]);
                }
            }
        }

        return Math.Max(longest, current) + text.Padding.Left + text.Padding.Right;
    }

    private static Face FaceOf(TextBlock text) =>
        new(text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle, text.FontStretch, text.LetterSpacing);

    private static Face FaceOf(Run run, TextBlock text) =>
        new(run.FontFamily, run.FontSize, run.FontWeight, run.FontStyle, run.FontStretch, text.LetterSpacing);

    private static double Word(Face face, string word)
    {
        var key = (face, word);

        if (Words.TryGetValue(key, out var known))
        {
            return known;
        }

        var probe = new TextBlock
        {
            Text = word,
            FontFamily = face.Family,
            FontSize = face.Size,
            FontWeight = face.Weight,
            FontStyle = face.Style,
            FontStretch = face.Stretch,
            LetterSpacing = face.Spacing,
            TextWrapping = TextWrapping.NoWrap,
        };

        probe.Measure(Size.Infinity);

        if (Words.Count >= MostRemembered)
        {
            Words.Clear();
        }

        return Words[key] = Math.Ceiling(probe.DesiredSize.Width);
    }

    private static double Dock(DockPanel dock)
    {
        var shown = dock.Children.Where(child => child.IsVisible).ToList();
        var across = 0.0;
        var down = 0.0;

        for (var i = 0; i < shown.Count; i++)
        {
            var fills = dock.LastChildFill && i == shown.Count - 1;
            var side = DockPanel.GetDock(shown[i]);

            if (!fills && side is Avalonia.Controls.Dock.Left or Avalonia.Controls.Dock.Right)
            {
                across += Of(shown[i]);
            }
            else
            {
                down = Math.Max(down, Of(shown[i]));
            }
        }

        return across + down;
    }

    private static double Columns(Grid grid)
    {
        var shown = grid.Children.Where(child => child.IsVisible).ToList();
        var count = grid.ColumnDefinitions.Count;

        if (count <= 1)
        {
            return shown.Select(Of).DefaultIfEmpty(0).Max();
        }

        var total = grid.ColumnSpacing * (count - 1);

        for (var column = 0; column < count; column++)
        {
            var definition = grid.ColumnDefinitions[column];
            var cells = shown.Where(child => Grid.GetColumn(child) == column && Grid.GetColumnSpan(child) == 1).ToList();

            total += definition.Width.GridUnitType switch
            {
                GridUnitType.Pixel => definition.Width.Value,
                GridUnitType.Star => cells.Select(Of).DefaultIfEmpty(0).Max(),
                _ => cells.Select(cell => cell.DesiredSize.Width).DefaultIfEmpty(0).Max(),
            };
        }

        return total;
    }

    private readonly record struct Face(FontFamily Family, double Size, FontWeight Weight, FontStyle Style, FontStretch Stretch, double Spacing);
}
