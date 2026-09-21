using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>
/// A settable slider, paired with the readout the handoff always shows beside it (#352). Wraps
/// <see cref="Slider"/> for its drag, arrow-key and Page Up/Down handling rather than
/// reimplementing them.
/// </summary>
public sealed class Level : ContentControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Value));

    /// <summary>Raised when the slider moves the value — never by setting <see cref="Value"/> directly.</summary>
    public event EventHandler? ValueChanged;

    private readonly Slider _slider;
    private readonly TextBlock _readout;
    private bool _syncing;

    public Level()
    {
        Focusable = true;

        _slider = new Slider
        {
            SmallChange = 1,
            LargeChange = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _slider.ValueChanged += OnSliderValueChanged;

        _readout = new TextBlock
        {
            FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
            FontSize = Theming.TypeScale.Subheading,
            MinWidth = 52,
            Margin = new Thickness(14, 0, 0, 0),
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _readout.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.AccentInkKey));

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_slider, 0);
        Grid.SetColumn(_readout, 1);
        row.Children.Add(_slider);
        row.Children.Add(_readout);

        Content = row;

        GotFocus += (_, _) => _slider.Focus();

        _slider.Minimum = Minimum;
        _slider.Maximum = Maximum;
        _slider.Value = Value;
        Sync();
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_syncing)
        {
            return;
        }

        if (change.Property == MinimumProperty)
        {
            _slider.Minimum = Minimum;
        }
        else if (change.Property == MaximumProperty)
        {
            _slider.Maximum = Maximum;
        }
        else if (change.Property == ValueProperty)
        {
            _slider.Value = Value;
            Sync();
        }
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _syncing = true;
        Value = _slider.Value;
        _syncing = false;

        Sync();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Sync() => _readout.Text = Value.ToString("0", CultureInfo.InvariantCulture);
}
