using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>
/// A settable 0–1 value drawn as a bar of segments 2px apart, one per <see cref="Step"/>, with the value
/// in mono beside it. Clicking segment <c>n</c> sets <c>n × Step</c>, and dragging across the bar sets
/// each segment it passes; segments below <see cref="Minimum"/> are dim and set <see cref="Minimum"/>.
/// Left and Right step by one segment.
/// </summary>
public sealed class Level : ContentControl
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Maximum), 1);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Step), 0.05);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Level, double>(nameof(Value));

    public static readonly StyledProperty<bool> MutedProperty =
        AvaloniaProperty.Register<Level, bool>(nameof(Muted));

    public static readonly StyledProperty<bool> ShowsReadoutProperty =
        AvaloniaProperty.Register<Level, bool>(nameof(ShowsReadout), true);

    public static readonly StyledProperty<bool> OnTileProperty =
        AvaloniaProperty.Register<Level, bool>(nameof(OnTile));

    /// <summary>The width a level is drawn at on a settings row, bar and readout together.</summary>
    public const double CompactWidth = 280;

    private const double SegmentHeight = 16;

    /// <summary>Raised when a click or a key moves the value — never by setting <see cref="Value"/> directly.</summary>
    public event EventHandler? ValueChanged;

    private readonly Grid _bar = new()
    {
        ColumnSpacing = Segment.Gap,
        Height = SegmentHeight,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TextBlock _readout = new()
    {
        Name = "LevelReadout",
        FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
        FontSize = Theming.TypeScale.Body,
        MinWidth = 44,
        Margin = new Thickness(12, 0, 0, 0),
        TextAlignment = TextAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly List<Border> _segments = [];
    private readonly List<IDisposable?> _inks = [];

    public Level()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        _readout.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.WhiteKey));

        // Transparent rather than null, so the gaps and the height around the segments take the click too.
        var target = new Border
        {
            Background = Brushes.Transparent,
            MinHeight = Theming.TypeScale.MinimumTarget,
            Child = _bar,
        };
        target.PointerPressed += (_, e) =>
        {
            var x = e.GetPosition(_bar).X;
            e.Handled = true;
            Focus();
            e.Pointer.Capture(target);
            Set(ValueAt(SegmentAt(x, _bar.Bounds.Width, _segments.Count), Step, Minimum, Maximum));
        };
        target.PointerMoved += (_, e) =>
        {
            if (!e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var dragged = ValueAt(SegmentAt(e.GetPosition(_bar).X, _bar.Bounds.Width, _segments.Count), Step, Minimum, Maximum);

            if (Math.Abs(dragged - Value) > 1e-9)
            {
                Set(dragged);
            }
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(target, 0);
        Grid.SetColumn(_readout, 1);
        row.Children.Add(target);
        row.Children.Add(_readout);

        Content = row;

        KeyDown += OnKeyDown;

        Rebuild();
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

    /// <summary>What one segment is worth.</summary>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Lit segments in Grey2 rather than A: the value is kept but not in use.</summary>
    public bool Muted
    {
        get => GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }

    /// <summary>Whether the value is drawn in mono beside the bar.</summary>
    public bool ShowsReadout
    {
        get => GetValue(ShowsReadoutProperty);
        set => SetValue(ShowsReadoutProperty, value);
    }

    /// <summary>Unlit segments in Bg rather than Tile, for a bar drawn on a Tile ground.</summary>
    public bool OnTile
    {
        get => GetValue(OnTileProperty);
        set => SetValue(OnTileProperty, value);
    }

    /// <summary>Which segment, counted from 1, a point <paramref name="x"/> along a bar falls in.</summary>
    public static int SegmentAt(double x, double width, int count) =>
        count <= 0 || width <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(x / width * count), 1, count);

    /// <summary>The value clicking segment <paramref name="segment"/>, counted from 1, sets.</summary>
    public static double ValueAt(int segment, double step, double minimum, double maximum) =>
        Math.Clamp(Math.Round(segment * step, 6), minimum, maximum);

    /// <summary>The value one key press in <paramref name="direction"/> moves to.</summary>
    public static double Stepped(double value, int direction, double step, double minimum, double maximum) =>
        Math.Clamp(Math.Round(value + direction * step, 6), minimum, maximum);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MaximumProperty || change.Property == StepProperty)
        {
            Rebuild();
        }
        else if (change.Property == MinimumProperty || change.Property == ValueProperty
                 || change.Property == MutedProperty || change.Property == OnTileProperty)
        {
            Sync();
        }
        else if (change.Property == ShowsReadoutProperty)
        {
            _readout.IsVisible = ShowsReadout;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var direction = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            _ => 0,
        };

        if (direction != 0)
        {
            e.Handled = true;
            Set(Stepped(Value, direction, Step, Minimum, Maximum));
        }
    }

    private void Set(double value)
    {
        Value = value;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Rebuild()
    {
        var count = Step > 0 ? (int)Math.Round(Maximum / Step) : 0;

        _bar.Children.Clear();
        _bar.ColumnDefinitions.Clear();
        _segments.Clear();
        _inks.ForEach(ink => ink?.Dispose());
        _inks.Clear();

        for (var i = 0; i < count; i++)
        {
            var segment = new Border();
            _bar.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(segment, i);
            _bar.Children.Add(segment);
            _segments.Add(segment);
            _inks.Add(null);
        }

        Sync();
    }

    private void Sync()
    {
        const double tolerance = 1e-9;

        for (var i = 0; i < _segments.Count; i++)
        {
            var worth = (i + 1) * Step;
            var filled = worth <= Value + tolerance;
            var below = worth < Minimum - tolerance;
            var lit = Muted ? Theming.ThemeManager.Grey2Key : Theming.ThemeManager.AKey;
            var unlit = OnTile ? Theming.ThemeManager.BgKey : Theming.ThemeManager.TileKey;

            _inks[i]?.Dispose();
            _inks[i] = _segments[i].Bind(
                Border.BackgroundProperty,
                Application.Current!.Resources.GetResourceObservable(
                    filled ? lit : unlit));
            _segments[i].Opacity = below && !filled ? 0.4 : 1;
        }

        _readout.Text = Value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
