using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// A display-only track of one segment per value above the minimum, between the words at the two ends of a scale.
/// The word at an end is white when the value is there.
/// </summary>
public sealed class ScaleTrack : ContentControl
{
    private readonly TextBlock _low;
    private readonly TextBlock _high;
    private readonly List<Border> _segments = [];
    private readonly int _minimum;
    private readonly int _maximum;

    public ScaleTrack(string low, string high, int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
        IsHitTestVisible = false;
        Focusable = false;

        _low = Word(low);
        _high = Word(high);

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Segment.Gap, VerticalAlignment = VerticalAlignment.Center };

        for (var i = minimum + 1; i <= maximum; i++)
        {
            var cell = new Border { Width = 10, Height = 14 };
            _segments.Add(cell);
            bar.Children.Add(cell);
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(_low);
        row.Children.Add(bar);
        row.Children.Add(_high);
        Content = row;

        Show(minimum);
    }

    /// <summary>Fills the track to <paramref name="value"/>.</summary>
    public void Show(int value)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            Ink(_segments[i], Border.BackgroundProperty, _minimum + 1 + i <= value ? ThemeManager.AKey : ThemeManager.LineKey);
        }

        Ink(_low, TextBlock.ForegroundProperty, value <= _minimum ? ThemeManager.WhiteKey : ThemeManager.GreyKey);
        Ink(_high, TextBlock.ForegroundProperty, value >= _maximum ? ThemeManager.WhiteKey : ThemeManager.GreyKey);
    }

    private static TextBlock Word(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = TypeScale.Small,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Ink(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
