using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The Orbits section's plot: the focus at the centre and each placed child on its path, seen from above with
/// distances compressed. Display only. While its clock runs and it is visible in a window it asks for frames.
/// </summary>
internal sealed class OrbitPlot : Control
{
    public const double MostWidth = 700;

    /// <summary>The outermost mapped radius, as a fraction of half the plot's width.</summary>
    public const double Reach = 0.86;

    public static readonly StyledProperty<IBrush?> GroundProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("Ground");
    public static readonly StyledProperty<IBrush?> PathInkProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("PathInk");
    public static readonly StyledProperty<IBrush?> FrameInkProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("FrameInk");
    public static readonly StyledProperty<IBrush?> NameInkProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("NameInk");
    public static readonly StyledProperty<IBrush?> RingInkProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("RingInk");
    public static readonly StyledProperty<IBrush?> HoverInkProperty = AvaloniaProperty.Register<OrbitPlot, IBrush?>("HoverInk");

    private static readonly Typeface Chrome = new(new FontFamily(Fonts.ChromeFamily), FontStyle.Normal, FontWeight.Medium);
    private static readonly Typeface Mono = new(new FontFamily(Fonts.MonoFamily));

    private readonly OrbitLevel _level;
    private readonly OrbitClock _clock;
    private readonly string _system;
    private bool _queued;
    private int _generation;
    private int? _hover;

    static OrbitPlot() =>
        AffectsRender<OrbitPlot>(GroundProperty, PathInkProperty, FrameInkProperty, NameInkProperty, RingInkProperty, HoverInkProperty);

    public OrbitPlot(OrbitLevel level, OrbitClock clock, string system)
    {
        _level = level;
        _clock = clock;
        _system = system;
        ClipToBounds = true;

        ThemeInk.Bind(this, GroundProperty, ThemeManager.BgKey);
        ThemeInk.Bind(this, PathInkProperty, ThemeManager.LineKey);
        ThemeInk.Bind(this, FrameInkProperty, ThemeManager.Line2Key);
        ThemeInk.Bind(this, NameInkProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(this, RingInkProperty, ThemeManager.GreyKey);
        ThemeInk.Bind(this, HoverInkProperty, ThemeManager.AKey);
    }

    /// <summary>Raised on each frame drawn while running, before it is drawn.</summary>
    public event Action? Moved;

    /// <summary>Draws the child with <paramref name="bodyId"/> in the hover ink; null draws none.</summary>
    public void Highlight(int? bodyId)
    {
        _hover = bodyId;
        InvalidateVisual();
    }

    /// <summary>Asks for the next frame while the clock runs and the plot is visible in a window; further asks before it runs are folded into it.</summary>
    public void Request()
    {
        if (_queued || !_clock.Running || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        _queued = true;
        var generation = _generation;

        top.RequestAnimationFrame(_ =>
        {
            if (generation != _generation)
            {
                return;
            }

            _queued = false;

            if (!_clock.Running || !IsEffectivelyVisible)
            {
                return;
            }

            Moved?.Invoke();
            InvalidateVisual();
            Request();
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Request();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _generation++;
        _queued = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = double.IsFinite(availableSize.Width) ? Math.Min(MostWidth, availableSize.Width) : MostWidth;

        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);

        if (side <= 0)
        {
            return;
        }

        var box = new Rect(0, 0, side, side);
        var centre = box.Center;
        var radius = side / 2 * Reach;

        context.FillRectangle(GetValue(GroundProperty) ?? Brushes.Black, box);

        if (_level.Placed.Count > 0)
        {
            DrawRings(context, centre, radius);
            DrawPaths(context, centre, radius);
        }

        var white = GetValue(NameInkProperty) ?? Brushes.White;
        DrawMark(context, _level.Focus.Kind, centre, 20, white);
        var focusName = Text(OrbitLevel.ShortName(_level.Focus, _system), Chrome, 13, white);
        context.DrawText(focusName, new Point(centre.X - (focusName.Width / 2), centre.Y + 16));

        var now = _clock.Now();

        foreach (var child in _level.Placed)
        {
            if (Orrery.Offset(child, now) is not { } offset)
            {
                continue;
            }

            var at = centre + _level.Map(offset, radius);
            var ink = child.BodyId == _hover ? GetValue(HoverInkProperty) ?? white : white;

            DrawMark(context, child.Kind, at, child.Kind == OrbitNodeKind.Barycentre ? 12 : 10, ink);
            var name = Text(OrbitLevel.ShortName(child, _system), Chrome, 13, ink);
            context.DrawText(name, new Point(at.X + 12, at.Y - (name.Height / 2)));
        }

        if (GetValue(FrameInkProperty) is { } frame)
        {
            context.DrawRectangle(new Pen(frame, 1), box.Deflate(0.5));
        }
    }

    /// <summary>A planet is a filled square, a star two squares one turned 45°, a barycentre a cross of two 2px bars.</summary>
    public static void DrawMark(DrawingContext context, OrbitNodeKind kind, Point centre, double size, IBrush ink)
    {
        var square = new Rect(centre.X - (size / 2), centre.Y - (size / 2), size, size);

        switch (kind)
        {
            case OrbitNodeKind.Planet:
                context.FillRectangle(ink, square);
                break;

            case OrbitNodeKind.Star:
                context.FillRectangle(ink, square);

                using (context.PushTransform(
                    Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(Math.PI / 4) * Matrix.CreateTranslation(centre.X, centre.Y)))
                {
                    context.FillRectangle(ink, square);
                }

                break;

            default:
                context.FillRectangle(ink, new Rect(centre.X - 1, square.Top, 2, size));
                context.FillRectangle(ink, new Rect(square.Left, centre.Y - 1, size, 2));
                break;
        }
    }

    private void DrawRings(DrawingContext context, Point centre, double radius)
    {
        var ink = GetValue(FrameInkProperty) ?? Brushes.Gray;
        var pen = new Pen(ink, 1, new DashStyle([4, 6], 0));

        foreach (var au in _level.Rings)
        {
            var scaled = _level.Scaled(au, radius);
            context.DrawEllipse(null, pen, centre, scaled, scaled);

            var label = Text(OrbitLevel.RingLabel(au), Mono, 11, GetValue(RingInkProperty) ?? ink);
            context.DrawText(label, new Point(centre.X + 6, centre.Y - scaled - label.Height));
        }
    }

    private void DrawPaths(DrawingContext context, Point centre, double radius)
    {
        var pen = new Pen(GetValue(PathInkProperty) ?? Brushes.Gray, 1.5);

        foreach (var path in _level.Paths)
        {
            if (path.Count < 2)
            {
                continue;
            }

            var geometry = new StreamGeometry();

            using (var line = geometry.Open())
            {
                line.BeginFigure(centre + _level.Map(path[0], radius), false);

                for (var i = 1; i < path.Count; i++)
                {
                    line.LineTo(centre + _level.Map(path[i], radius));
                }

                line.EndFigure(true);
            }

            context.DrawGeometry(null, pen, geometry);
        }
    }

    private static FormattedText Text(string text, Typeface face, double size, IBrush ink) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, ink);
}

/// <summary>One body mark beside a list row or in the legend, drawn in the inherited text foreground.</summary>
internal sealed class OrbitMark : Control
{
    private readonly OrbitNodeKind _kind;

    static OrbitMark() => AffectsRender<OrbitMark>(TextElement.ForegroundProperty);

    public OrbitMark(OrbitNodeKind kind)
    {
        _kind = kind;
        Width = 12;
        Height = 12;
    }

    public override void Render(DrawingContext context)
    {
        if (GetValue(TextElement.ForegroundProperty) is { } ink)
        {
            OrbitPlot.DrawMark(context, _kind, new Rect(Bounds.Size).Center, _kind == OrbitNodeKind.Barycentre ? 12 : 10, ink);
        }
    }
}

/// <summary>The plot column and the list side by side, sharing the width as the design's flex rows do; stacked when narrow.</summary>
internal sealed class OrbitColumns : Avalonia.Controls.Panel
{
    public const double Gap = 24;
    private const double PlotBasis = 520;
    private const double ListBasis = 420;
    private const double ListMost = 640;

    protected override Size MeasureOverride(Size availableSize)
    {
        var (plot, list, side) = Widths(availableSize.Width);
        var (first, second) = Pair();

        first?.Measure(new Size(plot, double.PositiveInfinity));
        second?.Measure(new Size(list, double.PositiveInfinity));

        var high = first?.DesiredSize.Height ?? 0;
        var low = second?.DesiredSize.Height ?? 0;

        return side
            ? new Size(plot + Gap + list, Math.Max(high, low))
            : new Size(Math.Max(plot, list), high + Gap + low);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (plot, list, side) = Widths(finalSize.Width);
        var (first, second) = Pair();
        var high = first?.DesiredSize.Height ?? 0;

        first?.Arrange(new Rect(0, 0, plot, high));
        second?.Arrange(side
            ? new Rect(plot + Gap, 0, list, second.DesiredSize.Height)
            : new Rect(0, high + Gap, list, second.DesiredSize.Height));

        return finalSize;
    }

    private (Control? First, Control? Second) Pair() =>
        (Children.Count > 0 ? Children[0] : null, Children.Count > 1 ? Children[1] : null);

    private static (double Plot, double List, bool Side) Widths(double width)
    {
        if (!double.IsFinite(width))
        {
            return (OrbitPlot.MostWidth, ListMost, true);
        }

        if (width >= PlotBasis + Gap + ListBasis)
        {
            var plot = Math.Min(OrbitPlot.MostWidth, PlotBasis + ((width - Gap - PlotBasis - ListBasis) / 2));

            return (plot, Math.Min(ListMost, width - Gap - plot), true);
        }

        return (Math.Min(OrbitPlot.MostWidth, width), Math.Min(ListMost, width), false);
    }
}
