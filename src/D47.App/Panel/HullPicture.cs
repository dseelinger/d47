using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>A hull, drawn large, wherever a page wants one (#289).</summary>
internal sealed class HullPicture : Grid
{
    /// <summary>How big the picture is drawn in a page, kept for the session rather than per page.</summary>
    private static HullPictureSize _size = HullPictureSize.Beside;

    private readonly string? _hull;
    private readonly Control? _beside;
    private readonly Image _fitted = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>The marks, held clear of the scroller's right edge.</summary>
    private readonly StackPanel _marks = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 2,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 0, 16, 0),
    };

    private readonly Border _frame;
    private readonly StackPanel _column;

    private Bitmap? _picture;

    internal HullPicture(string? hull, Control? beside)
    {
        _hull = hull;
        _beside = beside;

        _frame = new Border
        {
            Child = _fitted,
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        _column = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 12),
            Children = { _marks, _frame },
        };

        ToolTip.SetTip(
            _frame, "Click to fill the window. The wheel zooms, dragging moves it, Escape returns.");

        _frame.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(_frame).Properties.IsLeftButtonPressed)
            {
                Expand();
            }
        };

        // The fetch lands on a background thread and this is the page it was started for, so the picture is
        // put in where the Commander is already looking rather than on their next visit.
        ShipArtStore.Arrived += Landed;
        DetachedFromVisualTree += (_, _) => ShipArtStore.Arrived -= Landed;

        Show();
    }

    /// <summary>
    /// The picture for a hull with the page's own words beside it, and the ask that fetches the picture
    /// if it is not here yet.
    /// </summary>
    /// <param name="beside">What the page would otherwise have drawn where this goes.</param>
    internal static HullPicture For(string? hull, Control? beside = null)
    {
        ShipArtStore.Want(hull);

        return new HullPicture(hull, beside);
    }

    private void Landed(string symbol) => Dispatcher.UIThread.Post(() =>
    {
        if (string.Equals(symbol, ShipArt.Symbol(_hull), StringComparison.Ordinal))
        {
            Show();
        }
    });

    private void Show()
    {
        _picture = ShipArt.Close4K(_hull);
        _fitted.Source = _picture;

        Lay();
    }

    /// <summary>Draws the picture and the words at the chosen size.</summary>
    private void Lay()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        if (_picture is null)
        {
            // No picture is the ordinary state for a hull nothing has rendered, and the page has to read
            // exactly as it did before this control existed.
            if (_beside is { } alone)
            {
                Children.Add(alone);
            }

            return;
        }

        // The column is built once and moved between the layouts, never rebuilt.
        Marks();

        var picture = _column;

        if (_size == HullPictureSize.Beside && _beside is not null)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            _beside.Margin = new Thickness(0, 0, 12, 0);

            Grid.SetColumn(_beside, 0);
            Grid.SetColumn(picture, 1);

            Children.Add(_beside);
            Children.Add(picture);

            return;
        }

        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid.SetRow(picture, 0);
        Children.Add(picture);

        if (_beside is { } under)
        {
            under.Margin = new Thickness(0);

            Grid.SetRow(under, 1);
            Children.Add(under);
        }
    }

    /// <summary>The three sizes, as marks rather than words.</summary>
    private void Marks()
    {
        _marks.Children.Clear();

        _marks.Children.Add(Step(
            Glyphs.PictureBeside,
            "Half the pane, with the figures beside it",
            _size == HullPictureSize.Beside,
            () => Resize(HullPictureSize.Beside)));

        _marks.Children.Add(Step(
            Glyphs.PictureWide,
            "The width of the pane",
            _size == HullPictureSize.Wide,
            () => Resize(HullPictureSize.Wide)));

        _marks.Children.Add(Step(
            Glyphs.Expand, "The whole window, with zoom", showing: false, Expand, strokeThickness: 0.8));
    }

    private static Button Step(
        string glyph, string said, bool showing, Action pressed, double strokeThickness = 2)
    {
        var button = new Button
        {
            Content = Glyphs.Draw(
                glyph, showing ? ThemeManager.AccentKey : ThemeManager.TextMutedKey, size: 13,
                strokeThickness: strokeThickness),
            Padding = new Thickness(6, 2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(showing ? 1 : 0),
        };

        LoadoutPages.Themed(button, Button.BorderBrushProperty, ThemeManager.AccentKey);
        ToolTip.SetTip(button, said);
        button.Click += (_, _) => pressed();

        return button;
    }

    private void Resize(HullPictureSize size)
    {
        _size = size;

        Lay();
    }

    private void Expand()
    {
        if (_picture is not { } picture || OverlayLayer.GetOverlayLayer(this) is not { } layer)
        {
            return;
        }

        layer.Children.Add(new HullPictureFull(picture, layer));
    }
}

/// <summary>How big a hull's picture is drawn in a page.</summary>
internal enum HullPictureSize
{
    /// <summary>Half the pane, on the right, with the page's own words in the other half.</summary>
    Beside,

    /// <summary>The width of the pane, with the words under it.</summary>
    Wide,
}

/// <summary>The expanded picture: one hull over the whole window, zoomable and pannable.</summary>
internal sealed class HullPictureFull : Grid
{
    /// <summary>How far one wheel notch moves the zoom.</summary>
    private const double Notch = 1.25;

    private readonly OverlayLayer _layer;
    private readonly Canvas _stage = new() { ClipToBounds = true };
    private readonly Image _image;
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly TranslateTransform _pan = new();
    private readonly double _width;
    private readonly double _height;

    private double _scale = 1;
    private double _fit = 1;
    private Point _from;
    private bool _dragging;

    internal HullPictureFull(Bitmap picture, OverlayLayer layer)
    {
        _layer = layer;
        _width = picture.PixelSize.Width;
        _height = picture.PixelSize.Height;

        _image = new Image
        {
            Source = picture,

            // None, with the size stated: every number below is in image pixels, which is the only way "one
            // image pixel to one screen pixel" is a limit that can be written down.
            Stretch = Stretch.None,
            Width = _width,
            Height = _height,
            RenderTransformOrigin = RelativePoint.TopLeft,
            RenderTransform = new TransformGroup { Children = { _zoom, _pan } },
        };

        _stage.Children.Add(_image);
        Children.Add(_stage);

        // **Opaque, and the deepest colour the theme has rather than the page's own.** The picture is a dark
        // hull with orange lines on black: a page showing through behind it turns that black into a window
        // onto the slot list, and the panel surface behind it letterboxes a render in the colour of a form.
        LoadoutPages.Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var close = LoadoutPages.Press("Close", Dismiss);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(14);
        Children.Add(close);

        // Focusable and focused, because Escape is the way out that needs no aiming and a control that never
        // took focus never sees a key.
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        // **Sized to the layer by hand, because the layer will not do it.** OverlayLayer arranges each child
        // at the size that child asked for — which is how a popup gets to be popup sized — so a control that
        // wants the window has to say so and keep saying so.
        Fill();
        layer.SizeChanged += Filled;
        DetachedFromVisualTree += (_, _) => layer.SizeChanged -= Filled;

        SizeChanged += (_, _) => Fit(keepZoom: _scale > _fit);
        PointerWheelChanged += Wheeled;
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += Released;
        DoubleTapped += (_, _) => Fit(keepZoom: false);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Dismiss();
                e.Handled = true;
            }
        };

        AttachedToVisualTree += (_, _) =>
        {
            Fit(keepZoom: false);
            Focus();
        };
    }

    private void Dismiss() => _layer.Children.Remove(this);

    private void Filled(object? sender, SizeChangedEventArgs e) => Fill();

    private void Fill()
    {
        Width = _layer.Bounds.Width;
        Height = _layer.Bounds.Height;
    }

    /// <summary>The whole picture, centred.</summary>
    private void Fit(bool keepZoom)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _fit = Math.Min(width / _width, height / _height);

        if (!keepZoom || _scale < _fit)
        {
            _scale = _fit;
            _pan.X = (width - (_width * _scale)) / 2;
            _pan.Y = (height - (_height * _scale)) / 2;
        }

        _zoom.ScaleX = _scale;
        _zoom.ScaleY = _scale;
        Hold();
    }

    /// <summary>
    /// One image pixel to one screen pixel, or the fit where that is already larger — a picture smaller
    /// than the window it is in has no detail left to reach.
    /// </summary>
    private double Most => Math.Max(1, _fit);

    private void Wheeled(object? sender, PointerWheelEventArgs e)
    {
        var at = e.GetPosition(_stage);
        var was = _scale;
        var now = Math.Clamp(_scale * Math.Pow(Notch, e.Delta.Y), _fit, Most);

        if (Math.Abs(now - was) < 0.0001)
        {
            return;
        }

        // The point under the pointer stays under the pointer, which is what makes a wheel zoom feel like
        // moving towards something rather than like the picture being replaced.
        _pan.X = at.X - ((at.X - _pan.X) * now / was);
        _pan.Y = at.Y - ((at.Y - _pan.Y) * now / was);

        _scale = now;
        _zoom.ScaleX = now;
        _zoom.ScaleY = now;

        Hold();
        e.Handled = true;
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _from = e.GetPosition(this);
        _dragging = true;
        e.Pointer.Capture(this);
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var at = e.GetPosition(this);

        _pan.X += at.X - _from.X;
        _pan.Y += at.Y - _from.Y;
        _from = at;

        Hold();
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);

        // A press that moved nothing is a click on the background, and a click on the background is the
        // second way out.
        if (e.GetPosition(this) == _from && !_image.Bounds.Contains(_from))
        {
            Dismiss();
        }
    }

    /// <summary>
    /// Keeps the picture where it can be seen: filling the window while it is larger than it, and
    /// centred while it is not.
    /// </summary>
    private void Hold()
    {
        _pan.X = Edge(Bounds.Width, _width * _scale, _pan.X);
        _pan.Y = Edge(Bounds.Height, _height * _scale, _pan.Y);
    }

    private static double Edge(double window, double picture, double offset) =>
        picture <= window
            ? (window - picture) / 2
            : Math.Clamp(offset, window - picture, 0);
}
