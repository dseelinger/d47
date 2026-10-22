using Avalonia;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The one star mark a route stop draws.</summary>
public enum RouteStar
{
    BlackHole,
    Neutron,
    WhiteDwarf,
    NoFuel,
    Restock,
}

/// <summary>The marks a route map and the galaxy map share; <c>ink</c> resolves a theme key to a brush.</summary>
internal static class RouteMarks
{
    /// <summary>A black hole, neutron star or white dwarf draws its own mark only; no fuel is for any other unscoopable star.</summary>
    public static RouteStar? StarOf(RouteStopMarks marks) =>
        marks.HasFlag(RouteStopMarks.BlackHole) ? RouteStar.BlackHole
        : marks.HasFlag(RouteStopMarks.Neutron) ? RouteStar.Neutron
        : marks.HasFlag(RouteStopMarks.WhiteDwarf) ? RouteStar.WhiteDwarf
        : marks.HasFlag(RouteStopMarks.NotScoopable) ? RouteStar.NoFuel
        : marks.HasFlag(RouteStopMarks.Restock) ? RouteStar.Restock
        : null;

    public static void Star(DrawingContext context, Func<string, IBrush> ink, RouteStar kind, Point at, double scale)
    {
        using var _ = context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(at.X, at.Y));
        var bg = ink(ThemeManager.BgKey);
        var white = ink(ThemeManager.WhiteKey);

        switch (kind)
        {
            case RouteStar.Neutron:
                context.DrawGeometry(bg, null, Polygon((0, -6.5), (6.5, 0), (0, 6.5), (-6.5, 0)));
                context.DrawGeometry(white, null, Polygon((0, -5), (5, 0), (0, 5), (-5, 0)));
                break;

            case RouteStar.WhiteDwarf:
                context.DrawGeometry(bg, null, Polygon((0, -6.5), (6.5, 0), (0, 6.5), (-6.5, 0)));
                context.DrawGeometry(null, new Pen(white, 1.5), Polygon((0, -5), (5, 0), (0, 5), (-5, 0)));
                break;

            case RouteStar.BlackHole:
                context.DrawRectangle(bg, null, new Rect(-6, -6, 12, 12));
                context.DrawRectangle(null, new Pen(white, 1.5), new Rect(-4.5, -4.5, 9, 9));
                context.DrawRectangle(white, null, new Rect(-1.5, -1.5, 3, 3));
                break;

            case RouteStar.Restock:
                context.DrawEllipse(bg, null, new Point(0, 0), 7, 7);
                context.DrawEllipse(null, new Pen(ink(ThemeManager.BlueKey), 2), new Point(0, 0), 5, 5);
                context.DrawEllipse(ink(ThemeManager.BlueKey), null, new Point(0, 0), 1.5, 1.5);
                break;

            default:
                context.DrawGeometry(bg, null, Polygon((0, -7.5), (7.5, 6), (-7.5, 6)));
                context.DrawGeometry(ink(ThemeManager.WarnKey), null, Polygon((0, -5.5), (5.5, 4.5), (-5.5, 4.5)));
                break;
        }
    }

    public static void Start(DrawingContext context, Func<string, IBrush> ink, Point at)
    {
        context.DrawRectangle(ink(ThemeManager.BgKey), null, new Rect(at.X - 5, at.Y - 5, 10, 10));
        context.DrawRectangle(null, new Pen(ink(ThemeManager.AKey), 2), new Rect(at.X - 3.5, at.Y - 3.5, 7, 7));
    }

    public static void Destination(DrawingContext context, Func<string, IBrush> ink, Point at)
    {
        context.DrawRectangle(ink(ThemeManager.BgKey), null, new Rect(at.X - 9, at.Y - 9, 18, 18));
        context.DrawRectangle(null, new Pen(ink(ThemeManager.AKey), 1), new Rect(at.X - 8, at.Y - 8, 16, 16));
        context.DrawRectangle(ink(ThemeManager.AKey), null, new Rect(at.X - 4, at.Y - 4, 8, 8));
    }

    public static void You(DrawingContext context, Func<string, IBrush> ink, Point at)
    {
        var cyan = ink(ThemeManager.CyanKey);
        var pen = new Pen(cyan, 1);

        context.DrawRectangle(ink(ThemeManager.BgKey), null, new Rect(at.X - 6, at.Y - 6, 12, 12));
        context.DrawRectangle(cyan, null, new Rect(at.X - 4, at.Y - 4, 8, 8));

        context.DrawLine(pen, new Point(at.X + 7, at.Y), new Point(at.X + 12, at.Y));
        context.DrawLine(pen, new Point(at.X - 7, at.Y), new Point(at.X - 12, at.Y));
        context.DrawLine(pen, new Point(at.X, at.Y + 7), new Point(at.X, at.Y + 12));
        context.DrawLine(pen, new Point(at.X, at.Y - 7), new Point(at.X, at.Y - 12));
    }

    private static StreamGeometry Polygon(params (double X, double Y)[] corners)
    {
        var geometry = new StreamGeometry();

        using var open = geometry.Open();
        open.BeginFigure(new Point(corners[0].X, corners[0].Y), true);

        foreach (var (x, y) in corners.Skip(1))
        {
            open.LineTo(new Point(x, y));
        }

        open.EndFigure(true);

        return geometry;
    }
}
