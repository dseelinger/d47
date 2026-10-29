using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A route map: a fitted <see cref="GalaxyMapView"/> in a framed square with a key under it (#1396).</summary>
/// <remarks>Static: no hover, click, zoom or pan. Every ink is a theme key resolved when drawn.</remarks>
public sealed class RouteMapView : Control
{
    public const double LargestSide = 360;

    public const double SmallestSide = 200;

    /// <summary>Height kept under the square for the key.</summary>
    public const double KeyHeight = 52;

    private const double Inset = 20;
    private const double KeyGap = 10;

    private static readonly RouteStar[] KeyOrder = [RouteStar.Neutron, RouteStar.WhiteDwarf, RouteStar.BlackHole, RouteStar.NoFuel, RouteStar.Restock];

    private readonly GalaxyMapView _map = new();
    private readonly Border _frame = new() { IsHitTestVisible = false, BorderThickness = new Thickness(1) };

    public RouteMapView()
    {
        IsHitTestVisible = false;

        _map.Fit(Inset);
        _map.Redrawn += _ => InvalidateVisual();
        ThemeInk.Bind(_frame, Border.BorderBrushProperty, ThemeManager.LineKey);

        VisualChildren.Add(_map);
        VisualChildren.Add(_frame);
        LogicalChildren.Add(_map);
        LogicalChildren.Add(_frame);

        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    internal GalaxyMapView Map => _map;

    /// <summary>The square's side for a body of this height.</summary>
    internal static double SideFor(double height) =>
        double.IsInfinity(height) ? LargestSide : Math.Clamp(height - KeyHeight, SmallestSide, LargestSide);

    /// <summary>
    /// Draws <paramref name="stops"/>; <paramref name="reached"/> is the index of the stop the Commander is on, or -1, and
    /// <paramref name="here"/> names the system where You is drawn.
    /// </summary>
    public void Show(IReadOnlyList<RouteMapStop> stops, int reached, StarPosition? commander, string? here)
    {
        _map.Show(new GalaxyMapScene(
            Regions: false,
            Travels: null,
            Route: stops,
            Reached: reached,
            Commander: RouteMapStop.CommanderMark(stops, reached, commander),
            Here: here));

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = SideFor(availableSize.Height);

        _map.Measure(new Size(side, side));
        _frame.Measure(new Size(side, side));

        return new Size(side, side + KeyHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var square = new Rect(0, 0, Side(finalSize), Side(finalSize));

        _map.Arrange(square);
        _frame.Arrange(square);

        return finalSize;
    }

    private static double Side(Size size) => Math.Max(0, Math.Min(size.Width, Math.Min(size.Height - KeyHeight, LargestSide)));

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var side = Side(Bounds.Size);

        if (side > 0)
        {
            Key(context, _map.Drawn.Stars, side);
        }
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
