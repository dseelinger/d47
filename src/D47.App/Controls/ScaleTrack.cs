using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// A <see cref="Level"/> of one segment per value above the minimum, between the words at the two ends of a scale.
/// Pressing or dragging a segment sets that value; the Left key steps down to the minimum. The word at an end is
/// white when the value is there.
/// </summary>
public sealed class ScaleTrack : ContentControl
{
    private readonly TextBlock _low;
    private readonly TextBlock _high;
    private readonly Level _bar;
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly double _step;

    /// <summary>Raised when a press, drag or key changes the value — never by <see cref="Show"/>.</summary>
    public event EventHandler? ValueChanged;

    public ScaleTrack(string low, string high, int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
        _step = 1.0 / (maximum - minimum);
        Focusable = false;

        _low = Word(low);
        _high = Word(high);

        _bar = new Level
        {
            Step = _step,
            Minimum = _step,
            Maximum = 1,
            ShowsReadout = false,
            MinWidth = Settings.SettingsView.GuardianTrackMinWidth,
            Margin = new Thickness(8, 0),
        };
        _bar.ValueChanged += (_, _) =>
        {
            Ink();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        _bar.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_bar, 1);
        Grid.SetColumn(_high, 2);
        row.Children.Add(_low);
        row.Children.Add(_bar);
        row.Children.Add(_high);
        Content = row;

        Show(minimum);
    }

    /// <summary>The value, from the minimum to the maximum.</summary>
    public int Value => _minimum + (int)Math.Round(_bar.Value / _step);

    /// <summary>Fills the track to <paramref name="value"/>.</summary>
    public void Show(int value)
    {
        _bar.Value = (Math.Clamp(value, _minimum, _maximum) - _minimum) * _step;
        Ink();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Left || Value != _minimum + 1)
        {
            return;
        }

        e.Handled = true;
        Show(_minimum);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Ink()
    {
        var value = Value;
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
