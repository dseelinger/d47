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

/// <summary>
/// A hull, drawn large, wherever a page wants one
/// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
/// <para>
/// <b>A control rather than a block of <c>ItemPage</c>, because Ship Details is not the only place
/// it goes.</b> The Commander asked for it modular on the day it was specified: planning a hull you
/// do not own wants the same picture, and so will anything else that ends up being about one ship.
/// Everything it needs is a hull symbol.
/// </para>
/// <para>
/// <b>Three sizes, asked for by mark</b> (the Commander's amendment, 2026-09-04). Half the pane
/// with the page's own words in the other half, which is where it opens; the width of the pane,
/// with the words under it; and the whole window. The first two are where it sits on a page and
/// the third is an act — leaving it puts the page back the size it was.
/// </para>
/// <para>
/// <b>The whole window is a picture you can get into.</b> The wheel zooms at the pointer, dragging
/// pans, a double click fits it again, Escape puts it back. Zoom stops at one image pixel to one
/// screen pixel, which is the whole of what rendering at 3840x2160 buys: on a 4K monitor that is
/// the entire ship, and on a 1080p monitor the canopy fills the screen.
/// </para>
/// <para>
/// <b>Absent is the ordinary state, not a failure.</b> The 4K picture is fetched rather than
/// shipped, so a hull whose art has not arrived — or a Commander who has turned fetching off, or
/// one with no network — gets a page that reads exactly as it did before this existed: the words
/// alone, no marks, no gap. The control is built either way, which is what lets it fill itself in
/// when a fetch lands rather than needing the page rebuilt around it.
/// </para>
/// <para>
/// <b>The desktop window only.</b> The expansion is an overlay on this window's own overlay layer,
/// and Ships has been drawn here alone since Phase 39, so nothing is being withheld from a surface
/// that would otherwise have it.
/// </para>
/// </summary>
internal sealed class HullPicture : Grid
{
    /// <summary>
    /// How big the picture is drawn in a page, kept for the session rather than per page.
    /// <para>
    /// <b>The size a Commander last chose is the size they want on the next ship.</b> Resetting to
    /// half on every drill would make the choice something to make again on every hull. Not in
    /// <c>ViewState</c> because it is not a page's state — it is how somebody is reading right
    /// now, and a fresh launch opening at half is the right default rather than a lost setting.
    /// </para>
    /// </summary>
    private static HullPictureSize _size = HullPictureSize.Beside;

    private readonly string? _hull;
    private readonly Control? _beside;
    private readonly Image _fitted = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Top,
    };

    /// <summary>
    /// The marks, held clear of the scroller's right edge.
    /// <para>
    /// <b>The last one sat under the scrollbar and could not be pressed</b> (reported
    /// 2026-09-04): the page scrolls, so the bar is drawn over the content's right edge, and
    /// reaching for the mark nearest the edge is exactly what a Commander does. Sixteen is the
    /// bar's own width with room either side, so the whole row stays hittable whether the bar is
    /// there or not.
    /// </para>
    /// </summary>
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

        // The fetch lands on a background thread and this is the page it was started for, so the
        // picture is put in where the Commander is already looking rather than on their next visit.
        ShipArtStore.Arrived += Landed;
        DetachedFromVisualTree += (_, _) => ShipArtStore.Arrived -= Landed;

        Show();
    }

    /// <summary>
    /// The picture for a hull with the page's own words beside it, and the ask that fetches the
    /// picture if it is not here yet.
    /// <para>
    /// One call so a page cannot do half of it: a page that showed the picture without asking
    /// would show it to nobody who had not already been sent the file by hand.
    /// </para>
    /// <para>
    /// <b>The words go through here rather than round it</b> because where they belong depends on
    /// how big the picture is — beside it at half width, under it at full width, and on their own
    /// when there is no picture at all. A page that laid them out itself would have to know all
    /// three, and would get the third one wrong on every hull nobody has rendered yet.
    /// </para>
    /// </summary>
    /// <param name="beside">
    /// What the page would otherwise have drawn where this goes. Null for a caller that has
    /// nothing to put beside a hull.
    /// </param>
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

    /// <summary>
    /// Draws the picture and the words at the chosen size.
    /// <para>
    /// <b>Two equal columns rather than a measured half</b>, so the split holds at every pane
    /// width the drill can produce and at every rung of the zoom ladder without a number in it.
    /// </para>
    /// <para>
    /// <b>The words wrap under the picture rather than round it.</b> Avalonia has no float: an
    /// image in a paragraph is an inline that text goes around the outside of, not one it flows
    /// past. What is achievable is the half that matters — a ship's own figures sit beside its
    /// picture, and the slot list below runs the full width of the pane, which is where the
    /// wrapping would have happened anyway.
    /// </para>
    /// </summary>
    private void Lay()
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();

        if (_picture is null)
        {
            // No picture is the ordinary state for a hull nothing has rendered, and the page has
            // to read exactly as it did before this control existed.
            if (_beside is { } alone)
            {
                Children.Add(alone);
            }

            return;
        }

        // The column is built once and moved between the layouts, never rebuilt. A fresh wrapper
        // each time throws: the frame would still be a child of the one before it, and a control
        // belongs to exactly one logical tree.
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

    /// <summary>
    /// The three sizes, as marks rather than words. Half and full are where the picture sits on
    /// the page; the third is the whole window, which is an act rather than a state — leaving it
    /// puts the page back the size it was.
    /// </summary>
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
            Glyphs.Expand, "The whole window, with zoom", showing: false, Expand));
    }

    private static Button Step(string glyph, string said, bool showing, Action pressed)
    {
        var button = new Button
        {
            Content = Glyphs.Draw(
                glyph, showing ? ThemeManager.AccentKey : ThemeManager.TextMutedKey, size: 13),
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

/// <summary>
/// The expanded picture: one hull over the whole window, zoomable and pannable.
/// <para>
/// <b>On the window's overlay layer rather than in a second window.</b> A popup cannot exist in
/// the VR path at all, which is the rule <c>ItemPage</c> already records for its chooser, and a
/// separate window would need the zoom, the theme and the placement kept in step with the one it
/// is covering. The overlay layer is the whole client area of the window the panel is in, which is
/// what "the whole main window, over the panel" asked for.
/// </para>
/// </summary>
internal sealed class HullPictureFull : Grid
{
    /// <summary>
    /// How far one wheel notch moves the zoom. A ratio rather than a step, so zooming in and back
    /// out lands where it started.
    /// </summary>
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

            // None, with the size stated: every number below is in image pixels, which is the only
            // way "one image pixel to one screen pixel" is a limit that can be written down.
            Stretch = Stretch.None,
            Width = _width,
            Height = _height,
            RenderTransformOrigin = RelativePoint.TopLeft,
            RenderTransform = new TransformGroup { Children = { _zoom, _pan } },
        };

        _stage.Children.Add(_image);
        Children.Add(_stage);

        // **Opaque, and the deepest colour the theme has rather than the page's own.** The picture
        // is a dark hull with orange lines on black: a page showing through behind it turns that
        // black into a window onto the slot list, and the panel surface behind it letterboxes a
        // render in the colour of a form. Background is the one that reads as "there is nothing
        // here but the ship" in a dark theme and as a mount in a light one.
        LoadoutPages.Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var close = LoadoutPages.Press("Close", Dismiss);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(14);
        Children.Add(close);

        // Focusable and focused, because Escape is the way out that needs no aiming and a control
        // that never took focus never sees a key.
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        // **Sized to the layer by hand, because the layer will not do it.** OverlayLayer arranges
        // each child at the size that child asked for — which is how a popup gets to be popup
        // sized — so a control that wants the window has to say so and keep saying so. Drawn and
        // looked at: without this the expanded hull was a ninety-pixel thumbnail in the corner.
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

    /// <summary>
    /// The whole picture, centred. <b>Also the floor for zooming out</b>: there is nothing to see
    /// past the edges of a render on a black ground, so shrinking below the fit only makes the
    /// ship smaller for no reason.
    /// </summary>
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
    /// One image pixel to one screen pixel, or the fit where that is already larger — a picture
    /// smaller than the window it is in has no detail left to reach.
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

        // The point under the pointer stays under the pointer, which is what makes a wheel zoom
        // feel like moving towards something rather than like the picture being replaced.
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

        // A press that moved nothing is a click on the background, and a click on the background
        // is the second way out. Measured against the picture rather than the pointer, so a drag
        // that ends where it began still counts as a drag if it went anywhere.
        if (e.GetPosition(this) == _from && !_image.Bounds.Contains(_from))
        {
            Dismiss();
        }
    }

    /// <summary>
    /// Keeps the picture where it can be seen: filling the window while it is larger than it, and
    /// centred while it is not. Without this a drag can throw a zoomed hull off the edge and leave
    /// a Commander looking at nothing, with no scrollbar to say which way it went.
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
