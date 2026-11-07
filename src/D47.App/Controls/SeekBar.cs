using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// A 44px face holding a 6px track, a fill and a 4×24 position mark. Dragging raises <see cref="Dragged"/> on every
/// move and <see cref="Released"/> once on release, both with a fraction from 0 to 1; a bar that is not
/// <see cref="Draggable"/> only draws.
/// </summary>
public sealed class SeekBar : Border
{
    private readonly Border _track = new() { Height = 6, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _fill = new() { Height = 6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _mark = new() { Width = 4, Height = 24, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private double _position;
    private bool _active = true;
    private bool _dragging;

    public SeekBar()
    {
        MinHeight = TypeScale.MinimumTarget;
        Padding = new Thickness(12, 0);
        Cursor = new Cursor(StandardCursorType.Hand);

        var face = new Grid { VerticalAlignment = VerticalAlignment.Center, Height = 24 };
        face.Children.Add(_track);
        face.Children.Add(_fill);
        face.Children.Add(_mark);
        Child = face;

        face.SizeChanged += (_, _) => Place();
        Restyle();
    }

    /// <summary>Raised on each pointer move while a drag is held.</summary>
    public event Action<double>? Dragged;

    /// <summary>Raised once, when a drag is released.</summary>
    public event Action<double>? Released;

    public bool Draggable { get; set; } = true;

    /// <summary>Whether a drag is held now.</summary>
    public bool IsDragging => _dragging;

    /// <summary>0 to 1; ignored for drawing while a drag is held.</summary>
    public double Position
    {
        get => _position;
        set
        {
            if (_dragging)
            {
                return;
            }

            Show(value);
        }
    }

    /// <summary>False greys the bar out and stops it seeking.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Restyle();

            if (!value)
            {
                _dragging = false;
                Show(0);
            }
        }
    }

    public static double FractionAt(double x, double width) =>
        width <= 0 ? 0 : Math.Clamp(x / width, 0, 1);

    private void Show(double value)
    {
        _position = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
        Place();
    }

    private void Place()
    {
        var width = _track.Bounds.Width;
        _fill.Width = _position * width;
        _mark.Margin = new Thickness(_position * width - 2, 0, 0, 0);
    }

    private void Restyle()
    {
        ThemeInk.Bind(this, BackgroundProperty, _active ? ThemeManager.TileKey : ThemeManager.SlabKey);
        ThemeInk.Bind(_track, BackgroundProperty, ThemeManager.Line2Key);
        ThemeInk.Bind(_fill, BackgroundProperty, _active ? ThemeManager.AKey : ThemeManager.Grey2Key);
        ThemeInk.Bind(_mark, BackgroundProperty, _active ? ThemeManager.AKey : ThemeManager.Grey2Key);
    }

    private double FractionOf(PointerEventArgs e) => FractionAt(e.GetPosition(_track).X, _track.Bounds.Width);

    internal void Press(double fraction)
    {
        if (_active && Draggable)
        {
            _dragging = true;
            Show(fraction);
            Dragged?.Invoke(fraction);
        }
    }

    internal void Move(double fraction)
    {
        if (_dragging)
        {
            Show(fraction);
            Dragged?.Invoke(fraction);
        }
    }

    internal void Release(double fraction)
    {
        if (_dragging)
        {
            _dragging = false;
            Released?.Invoke(fraction);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_active && Draggable && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            Press(FractionOf(e));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Move(FractionOf(e));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging)
        {
            e.Pointer.Capture(null);
            Release(FractionOf(e));
        }
    }
}
