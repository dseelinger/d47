using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>One row of a <see cref="Sidebar"/>: a key the caller is handed back, its label and an optional count.</summary>
public sealed record SidebarItem(string Key, string Label, string? Count = null);

/// <summary>A run of <see cref="SidebarItem"/> rows, under a heading when one is given.</summary>
public sealed record SidebarGroup(string? Heading, IReadOnlyList<SidebarItem> Items);

/// <summary>
/// The kit's <c>.d47-sidebar</c>: a 230-wide column of 44px rows, grouped under slab headings, with a
/// <c>line2</c> rule on its right and 28 before the content. Hover is Tile2; the selected row is solid A
/// with Knock text.
/// </summary>
public sealed class Sidebar : Border
{
    public const double ColumnWidth = 230;

    public const double ContentGap = 28;

    public const double ItemHeight = TypeScale.MinimumTarget;

    private const double GroupHeight = 40;

    private const double GroupGap = 16;

    private readonly Action<string>? _select;
    private readonly List<Row> _rows = [];
    private string? _selected;

    public Sidebar(IReadOnlyList<SidebarGroup> groups, string? selected = null, Action<string>? select = null, string? note = null)
    {
        _select = select;
        _selected = selected;

        Width = ColumnWidth;
        Margin = new Thickness(0, 0, ContentGap, 0);
        Padding = new Thickness(0, 0, 16, 0);
        BorderThickness = new Thickness(0, 0, 1, 0);
        Themed(this, BorderBrushProperty, ThemeManager.Line2Key);

        var column = new StackPanel { Spacing = 2 };

        if (note is not null)
        {
            var said = new TextBlock
            {
                Text = note,
                FontSize = TypeScale.Small,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            };
            Themed(said, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            column.Children.Add(said);
        }

        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];

            if (group.Heading is { } heading)
            {
                column.Children.Add(Heading(heading, g == 0 ? 0 : GroupGap));
            }

            foreach (var item in group.Items)
            {
                var row = new Row(item, group.Heading is null ? 12 : 15, this);
                _rows.Add(row);
                column.Children.Add(row.Shape);
            }
        }

        Child = column;
        Paint();
    }

    /// <summary>The key of the selected row, or null when none is.</summary>
    public string? Selected
    {
        get => _selected;
        set
        {
            if (!string.Equals(_selected, value, StringComparison.Ordinal))
            {
                _selected = value;
                Paint();
            }
        }
    }

    /// <summary>The rows, in order, for a test to read.</summary>
    internal IReadOnlyList<Border> Items => [.. _rows.Select(row => row.Shape)];

    private void Choose(string key)
    {
        Selected = key;
        _select?.Invoke(key);
    }

    private void Paint()
    {
        foreach (var row in _rows)
        {
            row.Paint(selected: string.Equals(row.Item.Key, _selected, StringComparison.Ordinal));
        }
    }

    private static Border Heading(string text, double gap)
    {
        var label = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.ControlLarge,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.ControlLarge * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var heading = new Border
        {
            Height = GroupHeight,
            Margin = new Thickness(0, gap, 0, 0),
            Padding = new Thickness(12, 0),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = label,
        };
        Themed(heading, BackgroundProperty, ThemeManager.SlabKey);
        Themed(heading, BorderBrushProperty, ThemeManager.AKey);

        return heading;
    }

    private static IDisposable Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));

    private sealed class Row
    {
        private const double ItemTracking = 0.04;

        private readonly TextBlock _label;
        private readonly TextBlock _count;
        private IDisposable? _fill;
        private IDisposable? _ink;
        private bool _selected;

        public Row(SidebarItem item, double indent, Sidebar owner)
        {
            Item = item;

            _label = new TextBlock
            {
                Text = item.Label.ToUpperInvariant(),
                FontFamily = Fonts.ChromeFamily,
                FontSize = TypeScale.Control,
                FontWeight = FontWeight.Medium,
                LetterSpacing = TypeScale.Control * ItemTracking,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            _count = new TextBlock
            {
                Text = item.Count,
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.MetaSmall,
                Opacity = 0.7,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = item.Count is not null,
            };
            _count.Bind(TextBlock.ForegroundProperty, _label.GetObservable(TextBlock.ForegroundProperty));

            var words = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(_count, 1);
            words.Children.Add(_label);
            words.Children.Add(_count);

            Shape = new Border
            {
                Height = ItemHeight,
                Padding = new Thickness(indent, 0, 12, 0),
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = words,
            };

            Shape.PointerPressed += (_, _) => owner.Choose(item.Key);
            Shape.PointerEntered += (_, _) => Paint(_selected, hovered: true);
            Shape.PointerExited += (_, _) => Paint(_selected, hovered: false);
        }

        public SidebarItem Item { get; }

        public Border Shape { get; }

        public void Paint(bool selected) => Paint(selected, Shape.IsPointerOver);

        private void Paint(bool selected, bool hovered)
        {
            _selected = selected;
            _fill?.Dispose();
            _ink?.Dispose();
            _fill = null;

            if (selected)
            {
                _fill = Themed(Shape, BackgroundProperty, ThemeManager.AKey);
                _ink = Themed(_label, TextBlock.ForegroundProperty, ThemeManager.KnockKey);
            }
            else if (hovered)
            {
                _fill = Themed(Shape, BackgroundProperty, ThemeManager.Tile2Key);
                _ink = Themed(_label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
            }
            else
            {
                Shape.Background = Brushes.Transparent;
                _ink = Themed(_label, TextBlock.ForegroundProperty, ThemeManager.AKey);
            }
        }
    }
}
