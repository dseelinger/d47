using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Controls;

/// <summary>
/// A rectangle with straight-cut corners or a sheared left and right edge, instead of a Border's
/// rounded ones. <see cref="Chamfer"/> and <see cref="Skew"/> are mutually exclusive: a non-zero
/// <see cref="Skew"/> draws a parallelogram and ignores <see cref="Chamfer"/>.
/// </summary>
public sealed class ChamferedBorder : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ChamferedBorder, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ChamferedBorder, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<ChamferedBorder, Thickness>(nameof(BorderThickness));

    /// <summary>Pixels cut from each corner, ordered top-left, top-right, bottom-right, bottom-left.</summary>
    public static readonly StyledProperty<CornerRadius> ChamferProperty =
        AvaloniaProperty.Register<ChamferedBorder, CornerRadius>(nameof(Chamfer));

    /// <summary>Pixels the top edge is shifted right of the bottom edge, shearing both side edges equally.</summary>
    public static readonly StyledProperty<double> SkewProperty =
        AvaloniaProperty.Register<ChamferedBorder, double>(nameof(Skew));

    static ChamferedBorder()
    {
        AffectsRender<ChamferedBorder>(
            BackgroundProperty, BorderBrushProperty, BorderThicknessProperty, ChamferProperty, SkewProperty);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>Uniform on all sides; only <see cref="Thickness.Left"/> is read.</summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public CornerRadius Chamfer
    {
        get => GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    public double Skew
    {
        get => GetValue(SkewProperty);
        set => SetValue(SkewProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var thickness = BorderThickness.Left;
        var rect = new Rect(Bounds.Size);

        if (thickness > 0)
        {
            rect = rect.Deflate(new Thickness(thickness / 2));
        }

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var geometry = Skew != 0 ? Parallelogram(rect, Skew) : Chamfered(rect, Chamfer);

        if (Background is { } background)
        {
            context.DrawGeometry(background, null, geometry);
        }

        if (BorderBrush is { } stroke && thickness > 0)
        {
            context.DrawGeometry(null, new Pen(stroke, thickness), geometry);
        }
    }

    private static StreamGeometry Parallelogram(Rect r, double skew)
    {
        var geometry = new StreamGeometry();

        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(r.X + skew, r.Y), isFilled: true);
        ctx.LineTo(new Point(r.Right, r.Y));
        ctx.LineTo(new Point(r.Right - skew, r.Bottom));
        ctx.LineTo(new Point(r.X, r.Bottom));
        ctx.EndFigure(true);

        return geometry;
    }

    private static StreamGeometry Chamfered(Rect r, CornerRadius c)
    {
        var geometry = new StreamGeometry();

        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(r.X + c.TopLeft, r.Y), isFilled: true);
        ctx.LineTo(new Point(r.Right - c.TopRight, r.Y));
        ctx.LineTo(new Point(r.Right, r.Y + c.TopRight));
        ctx.LineTo(new Point(r.Right, r.Bottom - c.BottomRight));
        ctx.LineTo(new Point(r.Right - c.BottomRight, r.Bottom));
        ctx.LineTo(new Point(r.X + c.BottomLeft, r.Bottom));
        ctx.LineTo(new Point(r.X, r.Bottom - c.BottomLeft));
        ctx.LineTo(new Point(r.X, r.Y + c.TopLeft));
        ctx.EndFigure(true);

        return geometry;
    }
}
