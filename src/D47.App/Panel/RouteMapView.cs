using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A route map: a <see cref="RouteMapLayout"/> drawn in a square with a key under it (#1396).</summary>
/// <remarks>Static: no hover, click, zoom or pan. Every ink is a theme key resolved when drawn.</remarks>
public sealed class RouteMapView : Control
{
    public const double LargestSide = 360;

    public const double SmallestSide = 200;

    /// <summary>Height kept under the square for the key.</summary>
    public const double KeyHeight = 52;

    private const double Inset = 20;
    private const double KeyGap = 10;
    private const double NearestSameMark = 8;
    private const double NameOffset = 16;
    private const double EdgeMargin = 4;

    private static readonly RouteStar[] KeyOrder = [RouteStar.Neutron, RouteStar.WhiteDwarf, RouteStar.BlackHole, RouteStar.NoFuel, RouteStar.Restock];

    private RouteMapLayout? _layout;
    private string? _here;

    public RouteMapView()
    {
        IsHitTestVisible = false;

        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The square's side for a body of this height.</summary>
    internal static double SideFor(double height) =>
        double.IsInfinity(height) ? LargestSide : Math.Clamp(height - KeyHeight, SmallestSide, LargestSide);

    /// <summary>Draws <paramref name="layout"/>; <paramref name="here"/> names the Commander's system where You is drawn.</summary>
    public void Show(RouteMapLayout layout, string? here)
    {
        _layout = layout;
        _here = here;

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = SideFor(availableSize.Height);

        return new Size(side, side + KeyHeight);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_layout is not { } layout || layout.Points.Count < 2)
        {
            return;
        }

        var side = Math.Min(Bounds.Width, Bounds.Height - KeyHeight);

        if (side <= 2 * Inset)
        {
            return;
        }

        var extent = side - (2 * Inset);
        Point At(double x, double y) => new(Inset + (x * extent), Inset + ((1 - y) * extent));

        var points = layout.Points;
        var placed = points.Select(point => At(point.X, point.Y)).ToList();
        var hereIndex = points.ToList().FindIndex(point => point.Leg == RouteLeg.Here);

        context.DrawRectangle(Brush(ThemeManager.BgKey), null, new Rect(0, 0, side, side));

        var behind = placed.Take(hereIndex + 1).ToList();
        var ahead = placed.Skip(Math.Max(hereIndex, 0)).ToList();

        Line(context, behind, ThemeManager.GreyKey);
        Line(context, ahead, ThemeManager.AKey);

        var drawn = new List<(RouteStar Kind, Point At)>();

        for (var index = 0; index < points.Count; index++)
        {
            if (RouteMarks.StarOf(points[index].Marks) is not { } kind ||
                drawn.Any(mark => mark.Kind == kind && Distance(mark.At, placed[index]) <= NearestSameMark))
            {
                continue;
            }

            drawn.Add((kind, placed[index]));
            RouteMarks.Star(context, Brush, kind, placed[index], 1);
        }

        RouteMarks.Start(context, Brush, placed[0]);
        RouteMarks.Destination(context, Brush, placed[^1]);

        Point? you = hereIndex >= 0 ? placed[hereIndex]
            : layout.Commander is { } off ? At(off.X, off.Y)
            : null;

        var taken = new List<Rect>();

        if (you is { } youAt && !string.IsNullOrEmpty(_here))
        {
            taken.Add(DrawName(context, _here, youAt, ThemeManager.CyanKey, side, taken));
        }

        if (!string.IsNullOrEmpty(points[^1].System))
        {
            DrawName(context, points[^1].System, placed[^1], ThemeManager.AKey, side, taken);
        }

        if (you is { } at)
        {
            RouteMarks.You(context, Brush, at);
        }

        context.DrawRectangle(
            null,
            new Pen(Brush(ThemeManager.LineKey), 1),
            new Rect(0.5, 0.5, side - 1, side - 1));

        Key(context, drawn.Select(mark => mark.Kind).Distinct().ToList(), side);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private void Line(DrawingContext context, IReadOnlyList<Point> stops, string key)
    {
        if (stops.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();

        using (var open = geometry.Open())
        {
            open.BeginFigure(stops[0], false);

            foreach (var stop in stops.Skip(1))
            {
                open.LineTo(stop);
            }

            open.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brush(key), 2, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round), geometry);
    }

    /// <summary>Draws a name beside a mark unless it would overlap one already drawn; returns its box.</summary>
    private Rect DrawName(DrawingContext context, string name, Point at, string key, double side, List<Rect> taken)
    {
        var text = Text(name.ToUpperInvariant(), 12, key, FontWeight.Medium);
        var x = at.X + NameOffset;

        if (x + text.Width > side - EdgeMargin)
        {
            x = at.X - NameOffset - text.Width;
        }

        var box = new Rect(x, at.Y - (text.Height / 2), text.Width, text.Height);

        if (taken.Any(other => other.Intersects(box)))
        {
            return box;
        }

        context.DrawText(text, box.TopLeft);

        return box;
    }

    private void Key(DrawingContext context, IReadOnlyList<RouteStar> kinds, double side)
    {
        const double Glyph = 14;
        const double Between = 13;
        const double RowHeight = 18;

        var x = 0.0;
        var y = side + KeyGap;

        foreach (var kind in KeyOrder.Where(kinds.Contains))
        {
            var text = Text(Word(kind), 11, ThemeManager.GreyKey, FontWeight.Medium);
            var width = Glyph + 6 + text.Width;

            if (x > 0 && x + width > side)
            {
                x = 0;
                y += RowHeight;
            }

            RouteMarks.Star(context, Brush, kind, new Point(x + (Glyph / 2), y + (RowHeight / 2)), Glyph / 20);
            context.DrawText(text, new Point(x + Glyph + 6, y + ((RowHeight - text.Height) / 2)));

            x += width + Between;
        }
    }

    private static string Word(RouteStar kind) => kind switch
    {
        RouteStar.Neutron => "NEUTRON",
        RouteStar.WhiteDwarf => "WHITE DWARF",
        RouteStar.BlackHole => "BLACK HOLE",
        RouteStar.Restock => "OUT OF TRITIUM",
        _ => "NO FUEL",
    };

    private FormattedText Text(string text, double size, string key, FontWeight weight) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(Fonts.ChromeFamily), FontStyle.Normal, weight),
            size,
            Brush(key));

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush
            ? brush
            : Brushes.Transparent;
}
