using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>A picture with a square frame on it. Dragging inside the frame moves it; dragging a corner resizes it.</summary>
internal sealed class CropSurface : Canvas
{
    /// <summary>The side of a corner handle, and the margin that keeps one at a picture corner inside the surface.</summary>
    public const double Handle = TypeScale.MinimumTarget;

    private const double Inset = Handle / 2;

    private const double LongestPicture = 852;

    private const double TallestPicture = 520;

    private static readonly CropCorner[] Corners = [CropCorner.TopLeft, CropCorner.TopRight, CropCorner.BottomLeft, CropCorner.BottomRight];

    private readonly int _width;
    private readonly int _height;
    private readonly double _scale;
    private readonly Border _frame = new() { BorderThickness = new Thickness(1), Background = Brushes.Transparent };
    private readonly Border[] _handles = new Border[4];
    private readonly Border[] _dim = new Border[4];
    private CropSquare _square;
    private Drag? _drag;

    public CropSurface(Bitmap picture, CropSquare square)
    {
        _width = picture.PixelSize.Width;
        _height = picture.PixelSize.Height;
        _scale = Math.Min(LongestPicture / _width, TallestPicture / _height);
        _square = square.Fit(_width, _height);

        Width = (_width * _scale) + Handle;
        Height = (_height * _scale) + Handle;
        Background = Brushes.Transparent;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;

        var image = new Image
        {
            Name = "CropPicture",
            Source = picture,
            Stretch = Stretch.Fill,
            Width = _width * _scale,
            Height = _height * _scale,
        };
        SetLeft(image, Inset);
        SetTop(image, Inset);
        Children.Add(image);

        for (var i = 0; i < _dim.Length; i++)
        {
            _dim[i] = new Border { Background = new SolidColorBrush(Colors.Black, 0.62), IsHitTestVisible = false };
            Children.Add(_dim[i]);
        }

        _frame.Name = "CropFrame";
        _frame.IsHitTestVisible = false;
        ThemeInk.Bind(_frame, Border.BorderBrushProperty, ThemeManager.AKey);
        Children.Add(_frame);

        for (var i = 0; i < _handles.Length; i++)
        {
            var mark = new Border { Width = 12, Height = 12, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ThemeInk.Bind(mark, Border.BackgroundProperty, ThemeManager.AKey);
            _handles[i] = new Border { Name = "CropHandle." + Corners[i], Width = Handle, Height = Handle, Child = mark, IsHitTestVisible = false };
            ThemeInk.Bind(_handles[i], Border.BackgroundProperty, ThemeManager.TileKey);
            Children.Add(_handles[i]);
        }

        Place();
    }

    public int PictureWidth => _width;

    public int PictureHeight => _height;

    /// <summary>Source pixels per drawn pixel is the reciprocal.</summary>
    public double Scale => _scale;

    public CropSquare Square => _square;

    public event Action<CropSquare>? SquareChanged;

    /// <summary>The surface point of a source pixel position.</summary>
    public Point ToSurface(double x, double y) => new((x * _scale) + Inset, (y * _scale) + Inset);

    /// <summary>Starts a drag at <paramref name="at"/>; false when it is outside the frame and its handles.</summary>
    public bool Press(Point at)
    {
        CropCorner? corner = null;

        for (var i = 0; i < Corners.Length; i++)
        {
            if (HandleBox(Corners[i]).Contains(at))
            {
                corner = Corners[i];
                break;
            }
        }

        if (corner is null && !FrameBox().Contains(at))
        {
            return false;
        }

        _drag = new Drag(at, _square, corner);
        Highlight(corner);

        return true;
    }

    public void DragTo(Point at)
    {
        if (_drag is not { } drag)
        {
            return;
        }

        var dx = (int)Math.Round((at.X - drag.From.X) / _scale);
        var dy = (int)Math.Round((at.Y - drag.From.Y) / _scale);
        var next = drag.Corner is { } corner
            ? drag.Start.Resize(corner, dx, dy, _width, _height)
            : drag.Start.Move(dx, dy, _width, _height);

        if (next == _square)
        {
            return;
        }

        _square = next;
        Place();
        SquareChanged?.Invoke(_square);
    }

    public void Release()
    {
        _drag = null;
        Highlight(null, active: false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Press(e.GetPosition(this)))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        DragTo(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Release();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Release();
    }

    /// <summary>Draws the frame in the active ink and the dragged corner as a filled handle, or puts both back.</summary>
    private void Highlight(CropCorner? corner, bool active = true)
    {
        ThemeInk.Bind(_frame, Border.BorderBrushProperty, active && corner is null ? ThemeManager.CyanKey : ThemeManager.AKey);

        for (var i = 0; i < _handles.Length; i++)
        {
            var on = active && corner == Corners[i];
            ThemeInk.Bind(_handles[i], Border.BackgroundProperty, on ? ThemeManager.AKey : ThemeManager.TileKey);
            ThemeInk.Bind((Border)_handles[i].Child!, Border.BackgroundProperty, on ? ThemeManager.KnockKey : ThemeManager.AKey);
        }
    }

    private Rect FrameBox()
    {
        var origin = ToSurface(_square.X, _square.Y);
        var side = _square.Side * _scale;

        return new Rect(origin.X, origin.Y, side, side);
    }

    private Rect HandleBox(CropCorner corner)
    {
        var frame = FrameBox();
        var x = corner is CropCorner.TopLeft or CropCorner.BottomLeft ? frame.Left : frame.Right;
        var y = corner is CropCorner.TopLeft or CropCorner.TopRight ? frame.Top : frame.Bottom;

        return new Rect(x - Inset, y - Inset, Handle, Handle);
    }

    private void Place()
    {
        var frame = FrameBox();
        var picture = new Rect(Inset, Inset, _width * _scale, _height * _scale);

        SetBox(_frame, frame);
        SetBox(_dim[0], new Rect(picture.Left, picture.Top, picture.Width, frame.Top - picture.Top));
        SetBox(_dim[1], new Rect(picture.Left, frame.Bottom, picture.Width, picture.Bottom - frame.Bottom));
        SetBox(_dim[2], new Rect(picture.Left, frame.Top, frame.Left - picture.Left, frame.Height));
        SetBox(_dim[3], new Rect(frame.Right, frame.Top, picture.Right - frame.Right, frame.Height));

        for (var i = 0; i < _handles.Length; i++)
        {
            SetBox(_handles[i], HandleBox(Corners[i]));
        }
    }

    private static void SetBox(Border border, Rect box)
    {
        SetLeft(border, box.Left);
        SetTop(border, box.Top);
        border.Width = Math.Max(0, box.Width);
        border.Height = Math.Max(0, box.Height);
    }

    private readonly record struct Drag(Point From, CropSquare Start, CropCorner? Corner);
}
